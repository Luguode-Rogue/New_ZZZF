using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using IoPath = System.IO.Path;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// 拍照式地形底图 v2（2026-09-06 重新设计，替代 TerrainPhotoCapture v6）。
    ///
    /// 与旧方案的根本差异——渲染对象生命周期：
    /// - 旧 v6：每场 new SceneView/RT/Camera → 结束时销毁（Cleanup/Shutdown/延迟/孤儿……）
    ///   销毁时机反复污染引擎状态，多次进出战斗触发卡死/卡顿（四轮实测）。
    /// - 新 v2：三个渲染对象全部进程级单例，**只创建一次，永不销毁**。
    ///   每场仅做 SetScene(新场景) → 拍摄 → SetEnable(false)。
    ///   不存在任何 Cleanup 路径，从设计上消灭销毁时机问题。
    ///
    /// 兜底：若引擎在旧场景卸载时连带销毁了共享 view（SetScene 抛异常），
    /// 在新场景已加载的安全期重建一次 view（RT/Camera 不挂场景，继续复用）。
    ///
    /// 回读：Texture.SaveToFile 落盘轮询（GetPixelData 对 RT 是 AccessViolation，唯一路线）。
    /// 发布：TerrainCache.ApplyPhotoPixels → photoReady / 图片直载 / PhotoDump 链路全复用。
    /// </summary>
    public sealed class TerrainPhotographer
    {
        private enum Stage { Idle, Warming, Exposing, Reading, Done }

        private sealed class CachedPhoto
        {
            public byte[] Rgba;
            public int Width;
            public int Height;
            public float WorldW;
            public float WorldH;
        }

        /// <summary>跨场照片缓存：键 = 场景签名。同场景退出再进直接复用，零渲染零落盘。</summary>
        private static readonly Dictionary<int, CachedPhoto> PhotoCache =
            new Dictionary<int, CachedPhoto>();
        private const int CacheLimit = 8;

        private const int PhotoSize = 2048;       // 渲染目标长边
        private const int PublishSize = 1024;     // 发布到 HTML 的底图长边
        private const int SettleFrames = 30;      // 视图就绪后的 mip 级联缓冲（~0.5s；原 60 偏冗余）
        private const int ExposeFrames = 5;       // 渲染窗口（~0.08s；渲染管线 1-2 帧即写满 RT，原 15 冗余）
        private const int MaxWaitFrames = 3600;   // 各阶段总超时（~60s）

        // ---- 进程级渲染对象：只创建一次，永不销毁 ----
        private static SceneView _sharedView;
        private static Camera _sharedCamera;
        private static Texture _sharedTarget;

        private Stage _stage = Stage.Idle;
        private int _waitFrames;
        private int _settleFrames;
        private int _exposeFrames;
        private string _savePath;
        private bool _triedSave;
        private long _lastLen = -1;
        private string _stablePath;
        private TerrainCache _cache;
        private Mission _mission;
        private Stopwatch _watch;

        public bool IsCompleted => _stage == Stage.Done;
        public bool IsActive => _stage == Stage.Warming || _stage == Stage.Exposing || _stage == Stage.Reading;
        public bool Failed => _failed;
        private bool _completedOnce;
        private bool _failed;

        /// <summary>请求一次俯视拍照。缓存必须已就绪（IsBaked）。同场景签名命中则零渲染直接发布。</summary>
        public void Start(Mission mission, TerrainCache cache)
        {
            _stage = Stage.Idle;
            _waitFrames = 0;
            _settleFrames = 0;
            _exposeFrames = 0;
            _triedSave = false;
            _lastLen = -1;
            _stablePath = null;

            if (mission == null || mission.Scene == null || cache == null || !cache.IsBaked)
                return;

            try
            {
                _mission = mission;
                _cache = cache;
                _watch = Stopwatch.StartNew();

                // 缓存命中：零渲染、零落盘
                if (PhotoCache.TryGetValue(cache.BakeSignature, out var cached)
                    && cached != null
                    && Math.Abs(cached.WorldW - cache.WorldW) < 1f
                    && Math.Abs(cached.WorldH - cache.WorldH) < 1f)
                {
                    cache.ApplyPhotoPixels(cached.Rgba, cached.Width, cached.Height);
                    _stage = Stage.Done;
                    _completedOnce = true;
                    TacticalMapLog.Info("[PhotoV2] cache hit (signature=" + cache.BakeSignature + "); skipped rendering.");
                    return;
                }

                _savePath = IoPath.Combine(IoPath.GetTempPath(), "TMapPhotoV2.png");
                try { File.Delete(_savePath); } catch { }

                EnsureSharedRenderObjects();
                _stage = Stage.Warming;
                TacticalMapLog.Info("[PhotoV2] capture requested. halfW=" +
                    (cache.WorldW * 0.5f + 4f).ToString("0.0") +
                    " halfH=" + (cache.WorldH * 0.5f + 4f).ToString("0.0"));
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoV2] start failed.", ex);
                _stage = Stage.Idle;
                _failed = true;
            }
        }

        /// <summary>每帧驱动。返回 true 表示本帧刚完成照片应用（触发重发布）。</summary>
        public bool Tick()
        {
            switch (_stage)
            {
                case Stage.Warming: return TickWarming();
                case Stage.Exposing: return TickExposing();
                case Stage.Reading: return TickReading();
                default: return false;
            }
        }

        private bool TickWarming()
        {
            if (++_waitFrames > MaxWaitFrames)
            {
                DisableView();
                TacticalMapLog.Warn("[PhotoV2] FAILED: ReadyToRender timeout (" + MaxWaitFrames + " frames).");
                _stage = Stage.Idle;
                _failed = true;
                return false;
            }

            try
            {
                if (_sharedView == null || !_sharedView.ReadyToRender())
                {
                    _settleFrames = 0;
                    return false;
                }
                if (++_settleFrames < SettleFrames) return false;

                _stage = Stage.Exposing;
                _waitFrames = 0;
                _exposeFrames = 0;
                TacticalMapLog.Info("[PhotoV2] expose window opened (settle=" + SettleFrames + ").");
                return false;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoV2] warming poll failed.", ex);
                DisableView();
                _stage = Stage.Idle;
                _failed = true;
                return false;
            }
        }

        private bool TickExposing()
        {
            if (++_waitFrames > MaxWaitFrames)
            {
                DisableView();
                TacticalMapLog.Warn("[PhotoV2] FAILED: expose timeout.");
                _stage = Stage.Idle;
                _failed = true;
                return false;
            }

            try
            {
                if (_sharedView == null || !_sharedView.ReadyToRender()) return false;
                if (++_exposeFrames < ExposeFrames) return false;

                // 渲染内容已进入纹理：停用视图（不销毁），请求落盘
                DisableView();
                try
                {
                    _sharedTarget.SaveToFile(_savePath, false);
                    _triedSave = true;
                    TacticalMapLog.Info("[PhotoV2] SaveToFile requested: " + _savePath);
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Warn("[PhotoV2] SaveToFile threw: " + ex.GetType().Name);
                }
                _stage = Stage.Reading;
                _waitFrames = 0;
                return false;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoV2] expose poll failed.", ex);
                DisableView();
                _stage = Stage.Idle;
                _failed = true;
                return false;
            }
        }

        private bool TickReading()
        {
            if (++_waitFrames > MaxWaitFrames)
            {
                TacticalMapLog.Warn("[PhotoV2] FAILED: PNG never stabilized (saveFileRequested=" + _triedSave + ").");
                _stage = Stage.Idle;
                _failed = true;
                return false;
            }

            try
            {
                if (!_triedSave) return false;
                if (!File.Exists(_savePath)) return false;
                long len = new FileInfo(_savePath).Length;
                if (len <= 0) return false;
                if (_stablePath != _savePath || _lastLen != len)
                {
                    _stablePath = _savePath;
                    _lastLen = len;
                    return false; // 大小连续两帧一致才认为写完
                }

                ApplyFromPng(_savePath);
                try { File.Delete(_savePath); } catch { }
                _stage = Stage.Done;
                _completedOnce = true;
                return true;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoV2] disk route failed.", ex);
                _stage = Stage.Idle;
                _failed = true;
                return false;
            }
        }

        /// <summary>Mission 结束：仅停用视图。无任何销毁——渲染对象随进程存活。</summary>
        public void OnMissionEnd()
        {
            DisableView();
            _stage = Stage.Idle;
        }

        private static void DisableView()
        {
            try { if (_sharedView != null) _sharedView.SetEnable(false); } catch { }
        }

        /// <summary>
        /// 确保三个渲染对象就绪并指向当前场景。view 复用失败（旧场景卸载时被引擎连带销毁）
        /// 则在新场景已加载的安全期重建；RT/Camera 不挂场景，永远复用。
        /// </summary>
        private void EnsureSharedRenderObjects()
        {
            Scene scene = _mission.Scene;
            float camZ = _cache.MaxH + 300f;
            float far = camZ + 2000f;
            float halfW = _cache.WorldW * 0.5f + 4f;
            float halfH = _cache.WorldH * 0.5f + 4f;
            float centerX = _cache.OriginX + _cache.WorldW * 0.5f;
            float centerY = _cache.OriginY + _cache.WorldH * 0.5f;
            int texW = PhotoSize;
            int texH = Math.Max(64, (int)Math.Round(PhotoSize * (double)_cache.WorldH / _cache.WorldW));

            bool viewUsable = false;
            if (_sharedView != null)
            {
                try
                {
                    _sharedView.SetScene(scene);
                    viewUsable = true;
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Warn("[PhotoV2] shared view unusable (scene swap threw " + ex.GetType().Name + "); recreating view.");
                    _sharedView = null;
                }
            }
            if (!viewUsable)
            {
                _sharedView = SceneView.CreateSceneView();
                _sharedView.SetScene(scene);
                _sharedView.SetAutoDepthTargetCreation(true);
                _sharedView.SetClearColor(4278190080u); // 不透明黑
                // 严禁对共享战斗场景的第二视图开启 postfx/快速曝光（v6 实测：双视图 postfx
                // 渲染竞争损坏引擎状态）。亮度由软件自动感光承担。
            }

            if (_sharedTarget == null || _sharedTarget.Width != texW || _sharedTarget.Height != texH)
                _sharedTarget = Texture.CreateRenderTarget("TMapPhotoV2", texW, texH, false, false, false, false);
            _sharedView.SetRenderTarget(_sharedTarget);

            if (_sharedCamera == null)
                _sharedCamera = Camera.CreateCamera();
            _sharedCamera.SetViewVolume(false, -halfW, halfW, -halfH, halfH, 1f, far);
            _sharedCamera.LookAt(new Vec3(centerX, centerY, camZ), new Vec3(centerX, centerY, 0f), new Vec3(0f, 1f, 0f));
            _sharedView.SetCamera(_sharedCamera);

            _sharedView.SetEnable(true);
        }

        private void ApplyFromPng(string path)
        {
            using (var bmp = new System.Drawing.Bitmap(path))
            {
                int w = bmp.Width, h = bmp.Height;
                var rect = new System.Drawing.Rectangle(0, 0, w, h);
                var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    int stride = data.Stride;
                    byte[] raw = new byte[w * h * 4];
                    for (int y = 0; y < h; y++)
                        System.Runtime.InteropServices.Marshal.Copy(
                            System.IntPtr.Add(data.Scan0, y * stride), raw, y * w * 4, Math.Min(stride, w * 4));
                    Apply(raw, w, h);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }

        private void Apply(byte[] raw, int photoW, int photoH)
        {
            bool swapRB = TacticalSettings.Instance.PhotoMapSwapRedBlue;
            int outW = PublishSize;
            int outH = Math.Max(1, (int)Math.Round(outW * (double)_cache.WorldH / _cache.WorldW));
            var output = new byte[outW * outH * 4];

            // 双线性下采样：照片行 0 = 北；发布缓冲行 0 = 南（与 TerrainCache 栅格布局一致）
            for (int r = 0; r < outH; r++)
            {
                float fySouth = (r + 0.5f) * photoH / outH - 0.5f;
                float fy = photoH - 1f - fySouth;
                int y0 = (int)Math.Floor(fy);
                float ty = fy - y0;
                int y0c = ClampInt(y0, 0, photoH - 1);
                int y1c = ClampInt(y0 + 1, 0, photoH - 1);

                for (int c = 0; c < outW; c++)
                {
                    float fx = (c + 0.5f) * photoW / outW - 0.5f;
                    int x0 = (int)Math.Floor(fx);
                    float tx = fx - x0;
                    int x0c = ClampInt(x0, 0, photoW - 1);
                    int x1c = ClampInt(x0 + 1, 0, photoW - 1);

                    int i00 = (y0c * photoW + x0c) * 4;
                    int i01 = (y0c * photoW + x1c) * 4;
                    int i10 = (y1c * photoW + x0c) * 4;
                    int i11 = (y1c * photoW + x1c) * 4;
                    if (i11 + 3 >= raw.Length) continue;

                    float r0 = raw[i00] + (raw[i01] - raw[i00]) * tx;
                    float r1 = raw[i10] + (raw[i11] - raw[i10]) * tx;
                    float g0 = raw[i00 + 1] + (raw[i01 + 1] - raw[i00 + 1]) * tx;
                    float g1 = raw[i10 + 1] + (raw[i11 + 1] - raw[i10 + 1]) * tx;
                    float b0 = raw[i00 + 2] + (raw[i01 + 2] - raw[i00 + 2]) * tx;
                    float b1 = raw[i10 + 2] + (raw[i11 + 2] - raw[i10 + 2]) * tx;

                    float rr = r0 + (r1 - r0) * ty;
                    float gg = g0 + (g1 - g0) * ty;
                    float bb = b0 + (b1 - b0) * ty;

                    int dst = (r * outW + c) * 4;
                    output[dst + 3] = 255;
                    output[dst] = ClampByte(swapRB ? bb : rr);
                    output[dst + 1] = ClampByte(gg);
                    output[dst + 2] = ClampByte(swapRB ? rr : bb);
                }
            }

            // 自动感光：仅曝光失败的暗图拉伸（无 postfx 时代 avg<55）
            int counted = 0;
            double avgBefore = 0;
            for (int i = 0; i < output.Length; i += 4)
            {
                avgBefore += (output[i] * 299 + output[i + 1] * 587 + output[i + 2] * 114) / 1000;
                counted++;
            }
            if (counted > 0)
            {
                avgBefore /= counted;
                if (avgBefore < 55.0)
                {
                    for (int i = 0; i < output.Length; i += 4)
                    {
                        int lum = (output[i] * 299 + output[i + 1] * 587 + output[i + 2] * 114) / 1000;
                        if (lum < 6) continue;
                        int mapped = Math.Min(215, Math.Max(25, 25 + (lum - 5) * 215 / 250));
                        double factor = mapped / (double)Math.Max(1, lum);
                        output[i] = ClampByte(output[i] * factor);
                        output[i + 1] = ClampByte(output[i + 1] * factor);
                        output[i + 2] = ClampByte(output[i + 2] * factor);
                    }
                }
            }

            TacticalMapLog.Info("[PhotoV2] photo stats: avgBefore=" + avgBefore.ToString("0.0") +
                " publish=" + outW + "x" + outH +
                " elapsed=" + (_watch?.ElapsedMilliseconds ?? -1) + "ms");

            if (PhotoCache.Count >= CacheLimit) PhotoCache.Clear();
            PhotoCache[_cache.BakeSignature] = new CachedPhoto
            {
                Rgba = output,
                Width = outW,
                Height = outH,
                WorldW = _cache.WorldW,
                WorldH = _cache.WorldH
            };

            _cache.ApplyPhotoPixels(output, outW, outH);
        }

        private static int ClampInt(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        private static byte ClampByte(double v)
        {
            return (byte)(v < 0 ? 0 : (v > 255 ? 255 : (int)v));
        }
    }
}
