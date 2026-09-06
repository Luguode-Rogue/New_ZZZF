using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// 拍照底图落盘诊断：把每场应用到地图缓存的照片底图（即游戏内实际显示的地图）
    /// 导出为 PNG 到 Modules/New_ZZZF/Logs/PhotoMapDump/，用于离线排查底图内容问题。
    ///
    /// 清理约定（2026-09-06 用户裁决）：正常关闭游戏时清空整个导出目录；
    /// 闪退/卡死等异常退出时 ProcessExit 不会运行，文件保留作为现场证据。
    /// </summary>
    internal static class PhotoMapDump
    {
        private const string DumpDirName = "PhotoMapDump";

        private static readonly object Sync = new object();
        private static readonly List<string> Files = new List<string>();
        private static bool _exitHooked;
        private static bool _cleaned;

        /// <summary>把照片 RGBA（行 0 = 南）写成 PNG（行 0 = 北，与游戏内显示方向一致）。</summary>
        internal static void Save(byte[] rgba, int width, int height, int signature, int version)
        {
            if (rgba == null || rgba.Length == 0 || width <= 0 || height <= 0) return;
            try
            {
                string path;
                lock (Sync)
                {
                    if (_cleaned) return;
                    string dir = EnsureDirectoryLocked();
                    if (dir == null) return;
                    HookProcessExitLocked();
                    path = Path.Combine(dir,
                        "tmap_photo_sig" + signature + "_v" + version + "_" +
                        DateTime.Now.ToString("yyyyMMdd_HHmmssfff") + ".png");
                }

                WriteAtomic(SavePngFlipped(rgba, width, height), path);

                lock (Sync) Files.Add(path);
                TacticalMapLog.Info("[PhotoDump] saved: " + Path.GetFileName(path) +
                    " (" + width + "x" + height + ")");
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoDump] save failed.", ex);
            }
        }

        /// <summary>清空导出目录。幂等；正常退出（SubModule unload / ProcessExit）时调用。</summary>
        internal static void Cleanup()
        {
            lock (Sync)
            {
                if (_cleaned) return;
                _cleaned = true;
                try
                {
                    string dir = GetDumpDirectory();
                    if (dir != null && Directory.Exists(dir))
                        Directory.Delete(dir, true);
                    TacticalMapLog.Info("[PhotoDump] cleanup done (files=" + Files.Count + ").");
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Error("[PhotoDump] cleanup failed.", ex);
                }
                Files.Clear();
                _terrainServiceSignature = 0;
                _navServiceVersion = 0;
                _riskServiceVersion = 0;
            }
        }

        private static string GetDumpDirectory()
        {
            try
            {
                // 复用 TacticalMapLog 的 Logs 目录定位（assembly 上两级 → Modules/New_ZZZF/Logs）。
                string logsDir = Path.GetDirectoryName(TacticalMapLog.LogPath);
                return string.IsNullOrEmpty(logsDir) ? null : Path.Combine(logsDir, DumpDirName);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>导出目录（供注册为 HtmlUI content root，前端经虚拟主机直接加载 PNG）。</summary>
        internal static string GetDumpDirectoryForContentRoot()
        {
            lock (Sync)
            {
                return EnsureDirectoryLocked();
            }
        }

        private static string EnsureDirectoryLocked()
        {
            try
            {
                string dir = GetDumpDirectory();
                if (dir == null) return null;
                Directory.CreateDirectory(dir);
                return dir;
            }
            catch
            {
                return null;
            }
        }

        private static int _terrainServiceSignature;
        private static int _navServiceVersion;
        private static int _riskServiceVersion;

        /// <summary>
        /// 把地形照片 / NavMesh / 风险层写成固定名 PNG（terrain.png / navmesh.png / risk.png），
        /// 供前端经 content-root 虚拟主机直接以 &lt;img&gt; 加载——彻底绕开 4MB Base64 的
        /// 序列化 + ExecuteScriptAsync(数 MB JS) 通道（每次进战斗卡顿的残留大头）。
        /// 同签名/版本跳过重写；退出时随目录一起清理。
        /// </summary>
        internal static void WriteServiceImages(
            byte[] photoRgba, int photoW, int photoH, int signature,
            byte[] navRgba, int navW, int navH, int navVersion,
            byte[] riskRgba, int riskW, int riskH, int riskVersion)
        {
            if (photoRgba == null || photoW <= 0 || photoH <= 0) return;
            try
            {
                lock (Sync)
                {
                    string dir = EnsureDirectoryLocked();
                    if (dir == null) return;

                    if (_terrainServiceSignature != signature || !File.Exists(Path.Combine(dir, "terrain.png")))
                    {
                        WriteAtomic(SavePngFlipped(photoRgba, photoW, photoH), Path.Combine(dir, "terrain.png"));
                        _terrainServiceSignature = signature;
                    }

                    if (navRgba != null && navW > 0 && navH > 0 && _navServiceVersion != navVersion)
                    {
                        WriteAtomic(SavePngFlipped(navRgba, navW, navH), Path.Combine(dir, "navmesh.png"));
                        _navServiceVersion = navVersion;
                    }

                    if (riskRgba != null && riskW > 0 && riskH > 0 && _riskServiceVersion != riskVersion)
                    {
                        WriteAtomic(SavePngFlipped(riskRgba, riskW, riskH), Path.Combine(dir, "risk.png"));
                        _riskServiceVersion = riskVersion;
                    }
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoDump] service image write failed.", ex);
            }
        }

        private static void WriteAtomic(Bitmap bmp, string path)
        {
            string tmp = path + ".tmp";
            try
            {
                bmp.Save(tmp, ImageFormat.Png);
            }
            finally
            {
                bmp.Dispose();
            }
            try { File.Delete(path); } catch { }
            File.Move(tmp, path);
        }

        private static void HookProcessExitLocked()
        {
            if (_exitHooked) return;
            _exitHooked = true;
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Cleanup();
        }

        private static Bitmap SavePngFlipped(byte[] rgba, int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, width, height);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int rowBytes = width * 4;
                // PhotoBaseRGBA 行 0 = 南（TerrainCache 布局），PNG 行 0 = 北 → 翻转写出。
                for (int y = 0; y < height; y++)
                {
                    int srcY = height - 1 - y;
                    Marshal.Copy(rgba, srcY * rowBytes,
                        IntPtr.Add(data.Scan0, y * data.Stride), rowBytes);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }
    }
}
