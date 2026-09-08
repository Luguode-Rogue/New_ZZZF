using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using IoPath = System.IO.Path;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// REV7 native photo capture.
    /// The manager is a process singleton; native SceneView/Camera/RenderTarget are process-lived.
    /// The live Mission.Scene is only rebound at mission start and is never released by this class.
    /// Capture uses the already-proven Texture.SaveToFile path, but fixes the render lifecycle:
    /// force resource loading, prevent auto-disable, quick exposure, clear task, always-valid RT,
    /// and save before disabling the view.
    /// </summary>
    public sealed class TerrainPhotographerV7
    {
        public static readonly TerrainPhotographerV7 Instance = new TerrainPhotographerV7();

        private const int PhotoRevision = 7;
        private const int PhotoSize = 2048;
        private const int PublishSize = 1024;
        private const int SettleFrames = 45;
        private const int ExposeFrames = 15;
        private const int MaxWaitFrames = 1800;
        private const int CacheLimit = 8;

        private enum Stage
        {
            Idle,
            Warming,
            Exposing,
            Reading,
            Done
        }

        private sealed class CachedPhoto
        {
            public byte[] Rgba;
            public int Width;
            public int Height;
            public float WorldW;
            public float WorldH;
        }

        private struct PixelStats
        {
            public double Avg;
            public double Variance;
            public double NonBlack;
            public int Min;
            public int Max;
        }

        private static readonly Dictionary<int, CachedPhoto> PhotoCache =
            new Dictionary<int, CachedPhoto>();

        private static SceneView _view;
        private static Camera _camera;
        private static Texture _target;
        private static string _targetSize;

        private Stage _stage = Stage.Idle;
        private int _waitFrames;
        private int _settleFrames;
        private int _exposeFrames;
        private bool _saveIssued;
        private string _savePath;
        private string _stablePath;
        private long _lastLength = -1;
        private TerrainCache _cache;
        private Mission _mission;
        private Stopwatch _watch;
        private bool _failed;

        private TerrainPhotographerV7() { }

        public bool IsCompleted { get { return _stage == Stage.Done; } }
        public bool IsActive
        {
            get
            {
                return _stage == Stage.Warming ||
                       _stage == Stage.Exposing ||
                       _stage == Stage.Reading;
            }
        }
        public bool Failed { get { return _failed; } }

        public void Start(Mission mission, TerrainCache cache)
        {
            ResetSession();

            if (mission == null || mission.Scene == null || cache == null || !cache.IsBaked)
                return;

            _mission = mission;
            _cache = cache;
            _watch = Stopwatch.StartNew();

            CachedPhoto cached;
            if (PhotoCache.TryGetValue(cache.BakeSignature, out cached) &&
                cached != null &&
                Math.Abs(cached.WorldW - cache.WorldW) < 1f &&
                Math.Abs(cached.WorldH - cache.WorldH) < 1f)
            {
                cache.ApplyPhotoPixels(cached.Rgba, cached.Width, cached.Height);
                _stage = Stage.Done;
                TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision +
                    " cache hit signature=" + cache.BakeSignature);
                return;
            }

            try
            {
                _savePath = IoPath.Combine(
                    IoPath.GetTempPath(),
                    "TMapPhotoNative_REV" + PhotoRevision + "_" +
                    cache.BakeSignature + ".png");
                TryDelete(_savePath);

                EnsureRenderObjects();
                ConfigureRenderState();

                _stage = Stage.Warming;
                TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision +
                    " START scene=" + mission.Scene.GetHashCode() +
                    " sceneName=" + (string.IsNullOrEmpty(mission.SceneName) ? "<empty>" : mission.SceneName) +
                    " target=" + PhotoSize + "x" + GetPhotoHeight(cache) +
                    " rt=" + (_target == null ? "null" : _target.Width + "x" + _target.Height) +
                    " save=" + _savePath);
            }
            catch (Exception ex)
            {
                _failed = true;
                _stage = Stage.Idle;
                TacticalMapLog.Error("[PhotoNative] REV=" + PhotoRevision + " start failed.", ex);
                DisableView();
            }
        }

        public bool Tick()
        {
            try
            {
                switch (_stage)
                {
                    case Stage.Warming:
                        return TickWarming();
                    case Stage.Exposing:
                        return TickExposing();
                    case Stage.Reading:
                        return TickReading();
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _failed = true;
                TacticalMapLog.Error("[PhotoNative] REV=" + PhotoRevision + " tick failed.", ex);
                DisableView();
                _stage = Stage.Idle;
                return false;
            }
        }

        public void OnMissionEnd()
        {
            DisableView();
            _stage = Stage.Idle;
            _mission = null;
            _cache = null;
            _saveIssued = false;
            _savePath = null;
            _stablePath = null;
            _lastLength = -1;
            TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " mission end; shared renderer retained.");
        }

        private bool TickWarming()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("ReadyToRender timeout");

            if (_view == null || !_view.ReadyToRender())
            {
                _settleFrames = 0;
                return false;
            }

            if (++_settleFrames < SettleFrames)
                return false;

            _stage = Stage.Exposing;
            _waitFrames = 0;
            _exposeFrames = 0;
            TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision +
                " view ready CheckSceneReady=" + SafeCheckSceneReady() +
                " expose begin.");
            return false;
        }

        private bool TickExposing()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("expose timeout");

            if (_view == null || !_view.ReadyToRender())
                return false;

            if (++_exposeFrames < ExposeFrames)
                return false;

            if (!_saveIssued)
            {
                if (_target == null)
                    return Fail("render target is null");

                try
                {
                    // Keep the rendered pixels valid after the render pass and save the RT directly.
                    _target.SetTextureAsAlwaysValid();
                    _target.SaveToFile(_savePath);
                    _saveIssued = true;
                    TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision +
                        " SaveToFile issued while view enabled. rtValid=" + SafeValid(_target) +
                        " isRT=" + SafeIsRenderTarget(_target));
                }
                catch (Exception ex)
                {
                    return Fail("SaveToFile threw: " + ex.GetType().Name);
                }
            }

            // Only stop after SaveToFile has received the rendered target.
            DisableView();
            _stage = Stage.Reading;
            _waitFrames = 0;
            return false;
        }

        private bool TickReading()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("PNG timeout; saveIssued=" + _saveIssued + " path=" + _savePath);

            if (!_saveIssued || string.IsNullOrEmpty(_savePath) || !File.Exists(_savePath))
                return false;

            long len = new FileInfo(_savePath).Length;
            if (len <= 0)
                return false;

            if (_stablePath != _savePath || _lastLength != len)
            {
                _stablePath = _savePath;
                _lastLength = len;
                return false;
            }

            if (!TryApplyAndValidate(_savePath))
                return Fail("PNG exists but validation failed");

            TryDelete(_savePath);
            _stage = Stage.Done;
            TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision +
                " DONE elapsed=" + (_watch == null ? -1 : _watch.ElapsedMilliseconds) + "ms");
            return true;
        }

        private void EnsureRenderObjects()
        {
            Scene scene = _mission.Scene;
            int texW = PhotoSize;
            int texH = GetPhotoHeight(_cache);

            if (_view == null)
            {
                _view = SceneView.CreateSceneView();
                if (_view == null)
                    throw new InvalidOperationException("SceneView.CreateSceneView returned null.");
            }

            _view.SetScene(scene);

            string sizeKey = texW + "x" + texH;
            if (_target == null || _targetSize != sizeKey)
            {
                _target = Texture.CreateRenderTarget(
                    "TMapPhotoNative_REV" + PhotoRevision,
                    texW,
                    texH,
                    false,
                    false,
                    false,
                    true);
                _targetSize = sizeKey;
                if (_target == null)
                    throw new InvalidOperationException("Texture.CreateRenderTarget returned null.");
            }

            _view.SetRenderTarget(_target);

            if (_camera == null)
            {
                _camera = Camera.CreateCamera();
                if (_camera == null)
                    throw new InvalidOperationException("Camera.CreateCamera returned null.");
            }

            float halfW = _cache.WorldW * 0.5f + 4f;
            float halfH = _cache.WorldH * 0.5f + 4f;
            float centerX = _cache.OriginX + _cache.WorldW * 0.5f;
            float centerY = _cache.OriginY + _cache.WorldH * 0.5f;
            float camZ = _cache.MaxH + 300f;
            float far = Math.Max(4000f, camZ - _cache.MinH + 512f);

            _camera.SetViewVolume(false, -halfW, halfW, -halfH, halfH, 1f, far);
            _camera.LookAt(
                new Vec3(centerX, centerY, camZ),
                new Vec3(centerX, centerY, 0f),
                new Vec3(0f, 1f, 0f));
            _view.SetCamera(_camera);
        }

        private void ConfigureRenderState()
        {
            if (_view == null || _mission == null || _mission.Scene == null)
                return;

            Scene scene = _mission.Scene;
            scene.ForceLoadResources(true);
            scene.EnsurePostfxSystem();
            scene.SetDofMode(false);
            scene.SetMotionBlurMode(false);

            _view.SetAutoDepthTargetCreation(true);
            _view.SetRenderOnDemand(false);
            _view.SetRenderWithPostfx(false);
            _view.SetSceneUsesSkybox(true);
            _view.SetSceneUsesShadows(true);
            _view.SetSceneUsesContour(false);
            _view.SetClearGbuffer(true);
            _view.DoNotClear(false);
            _view.SetClearAndDisableAfterSucessfullRender(false);
            _view.SetDoQuickExposure(true);
            _view.SetResolutionScaling(false);
            _view.AddClearTask(false);
            _view.SetEnable(true);
        }

        private bool TryApplyAndValidate(string path)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(path))
                {
                    if (bmp.Width <= 0 || bmp.Height <= 0)
                        return false;

                    Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        int stride = Math.Abs(data.Stride);
                        byte[] raw = new byte[bmp.Width * bmp.Height * 4];
                        for (int y = 0; y < bmp.Height; y++)
                        {
                            Marshal.Copy(
                                IntPtr.Add(data.Scan0, y * stride),
                                raw,
                                y * bmp.Width * 4,
                                Math.Min(stride, bmp.Width * 4));
                        }

                        PixelStats stats = Analyze(raw);
                        TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision +
                            " PNG=" + bmp.Width + "x" + bmp.Height +
                            " avg=" + stats.Avg.ToString("0.0") +
                            " variance=" + stats.Variance.ToString("0.0") +
                            " nonBlack=" + (stats.NonBlack * 100.0).ToString("0.0") + "%" +
                            " min=" + stats.Min + " max=" + stats.Max);

                        if (stats.NonBlack < 0.01 || stats.Variance < 2.0 || stats.Max < 8)
                            return false;

                        Apply(raw, bmp.Width, bmp.Height);
                        return true;
                    }
                    finally
                    {
                        bmp.UnlockBits(data);
                    }
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] REV=" + PhotoRevision + " PNG read failed.", ex);
                return false;
            }
        }

        private static PixelStats Analyze(byte[] raw)
        {
            long sum = 0;
            double sum2 = 0;
            int count = 0;
            int nonBlack = 0;
            int min = 255;
            int max = 0;

            for (int i = 0; i + 3 < raw.Length; i += 4)
            {
                int r = raw[i];
                int g = raw[i + 1];
                int b = raw[i + 2];
                int lum = (r * 299 + g * 587 + b * 114) / 1000;
                sum += lum;
                sum2 += (double)lum * lum;
                if (lum >= 8) nonBlack++;
                if (lum < min) min = lum;
                if (lum > max) max = lum;
                count++;
            }

            if (count == 0)
                return new PixelStats();

            double avg = (double)sum / count;
            return new PixelStats
            {
                Avg = avg,
                Variance = Math.Max(0.0, sum2 / count - avg * avg),
                NonBlack = (double)nonBlack / count,
                Min = min,
                Max = max
            };
        }

        private void Apply(byte[] raw, int photoW, int photoH)
        {
            int outW = PublishSize;
            int outH = Math.Max(1, (int)Math.Round(outW * (double)_cache.WorldH / _cache.WorldW));
            bool swapRB = TacticalSettings.Instance.PhotoMapSwapRedBlue;
            byte[] output = new byte[outW * outH * 4];

            using (Bitmap source = new Bitmap(photoW, photoH, PixelFormat.Format32bppArgb))
            {
                Rectangle sourceRect = new Rectangle(0, 0, photoW, photoH);
                BitmapData sourceData = source.LockBits(sourceRect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = Math.Abs(sourceData.Stride);
                    for (int y = 0; y < photoH; y++)
                    {
                        Marshal.Copy(raw, y * photoW * 4, IntPtr.Add(sourceData.Scan0, y * stride), Math.Min(stride, photoW * 4));
                    }
                }
                finally
                {
                    source.UnlockBits(sourceData);
                }

                using (Bitmap resized = new Bitmap(outW, outH, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.Clear(Color.Transparent);
                        g.DrawImage(source, new Rectangle(0, 0, outW, outH));
                    }

                    BitmapData resizedData = resized.LockBits(
                        new Rectangle(0, 0, outW, outH),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);
                    try
                    {
                        int stride = Math.Abs(resizedData.Stride);
                        for (int y = 0; y < outH; y++)
                        {
                            byte[] row = new byte[outW * 4];
                            Marshal.Copy(IntPtr.Add(resizedData.Scan0, y * stride), row, 0, Math.Min(stride, row.Length));
                            int dstRow = (outH - 1 - y) * outW * 4;
                            for (int x = 0; x < outW; x++)
                            {
                                int s = x * 4;
                                int d = dstRow + s;
                                output[d] = swapRB ? row[s + 2] : row[s];
                                output[d + 1] = row[s + 1];
                                output[d + 2] = swapRB ? row[s] : row[s + 2];
                                output[d + 3] = 255;
                            }
                        }
                    }
                    finally
                    {
                        resized.UnlockBits(resizedData);
                    }
                }
            }

            if (PhotoCache.Count >= CacheLimit)
                PhotoCache.Clear();

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

        private bool Fail(string reason)
        {
            TacticalMapLog.Warn("[PhotoNative] REV=" + PhotoRevision +
                " FAILED stage=" + _stage +
                " reason=" + reason +
                " wait=" + _waitFrames +
                " settle=" + _settleFrames +
                " expose=" + _exposeFrames);
            _failed = true;
            DisableView();
            _stage = Stage.Idle;
            return false;
        }

        private void ResetSession()
        {
            DisableView();
            _stage = Stage.Idle;
            _waitFrames = 0;
            _settleFrames = 0;
            _exposeFrames = 0;
            _saveIssued = false;
            _savePath = null;
            _stablePath = null;
            _lastLength = -1;
            _cache = null;
            _mission = null;
            _watch = null;
            _failed = false;
        }

        private static void DisableView()
        {
            try
            {
                if (_view != null)
                    _view.SetEnable(false);
            }
            catch { }
        }

        private bool SafeCheckSceneReady()
        {
            try { return _view != null && _view.CheckSceneReadyToRender(); }
            catch { return false; }
        }

        private static bool SafeValid(Texture texture)
        {
            try { return texture != null && texture.IsValid; }
            catch { return false; }
        }

        private static bool SafeIsRenderTarget(Texture texture)
        {
            try { return texture != null && texture.IsRenderTarget; }
            catch { return false; }
        }

        private static int GetPhotoHeight(TerrainCache cache)
        {
            if (cache == null || cache.WorldW <= 0f || cache.WorldH <= 0f)
                return PhotoSize;
            return Math.Max(64, (int)Math.Round(PhotoSize * (double)cache.WorldH / cache.WorldW));
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
