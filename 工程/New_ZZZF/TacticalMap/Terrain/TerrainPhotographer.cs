using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
    /// Native terrain photo capture.
    ///
    /// Rendering path:
    ///   Mission.Scene -> SceneView -> RenderTarget
    ///   -> SceneView native SaveFinalResultToDisk -> PNG -> CPU pixels
    ///
    /// The renderer is deliberately independent from HTMLUI and TerrainCache. TerrainCache only
    /// supplies world bounds and receives the final RGBA buffer through ApplyPhotoPixels().
    ///
    /// Important design rules:
    /// 1. Reuse one process-level SceneView/Camera/RenderTarget to avoid per-battle native teardown.
    /// 2. Never call GetPixelData() directly on a RenderTarget.
    /// 3. Let SceneView own the save operation so the exported image corresponds to the rendered view.
    /// 4. Treat file existence + pixel statistics as success criteria; a zero/black PNG is failure.
    /// 5. Use an orthographic camera whose rectangle is exactly derived from battle bounds.
    /// </summary>
    public sealed class TerrainPhotographer
    {
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

        private const int PhotoSize = 2048;
        private const int PublishSize = 1024;
        private const int SettleFrames = 30;
        private const int ExposeFrames = 6;
        private const int MaxWaitFrames = 3600;
        private const int CacheLimit = 8;

        private static readonly Dictionary<int, CachedPhoto> PhotoCache =
            new Dictionary<int, CachedPhoto>();

        // Native renderer lifetime is process-wide by design.
        private static SceneView _sharedView;
        private static Camera _sharedCamera;
        private static Texture _sharedTarget;
        private static string _sharedTargetKey;

        private Stage _stage = Stage.Idle;
        private int _waitFrames;
        private int _settleFrames;
        private int _exposeFrames;
        private bool _saveRequested;
        private long _lastLength = -1;
        private string _stablePath;
        private string _savePath;
        private TerrainCache _cache;
        private Mission _mission;
        private Stopwatch _watch;
        private bool _completedOnce;
        private bool _failed;

        public bool IsCompleted => _stage == Stage.Done;
        public bool IsActive => _stage == Stage.Warming || _stage == Stage.Exposing || _stage == Stage.Reading;
        public bool Failed => _failed;

        public void Start(Mission mission, TerrainCache cache)
        {
            ResetState();

            if (mission == null || mission.Scene == null || cache == null || !cache.IsBaked)
                return;

            try
            {
                _mission = mission;
                _cache = cache;
                _watch = Stopwatch.StartNew();

                CachedPhoto cached;
                if (PhotoCache.TryGetValue(cache.BakeSignature, out cached)
                    && cached != null
                    && Math.Abs(cached.WorldW - cache.WorldW) < 1f
                    && Math.Abs(cached.WorldH - cache.WorldH) < 1f)
                {
                    cache.ApplyPhotoPixels(cached.Rgba, cached.Width, cached.Height);
                    _stage = Stage.Done;
                    _completedOnce = true;
                    TacticalMapLog.Info("[PhotoNative] cache hit signature=" + cache.BakeSignature);
                    return;
                }

                string fileName = "TMapPhotoNative_" + cache.BakeSignature + ".png";
                _savePath = IoPath.Combine(IoPath.GetTempPath(), fileName);
                TryDelete(_savePath);

                EnsureRenderObjects();
                ConfigureExport();

                _stage = Stage.Warming;
                TacticalMapLog.Info("[PhotoNative] capture requested: " +
                    "world=" + cache.WorldW.ToString("0.0") + "x" + cache.WorldH.ToString("0.0") +
                    " target=" + PhotoSize + "x" + GetPhotoHeight(cache) +
                    " path=" + _savePath);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] start failed.", ex);
                _failed = true;
                _stage = Stage.Idle;
                DisableView();
            }
        }

        public bool Tick()
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

        public void OnMissionEnd()
        {
            DisableView();
            _stage = Stage.Idle;
            _mission = null;
            _cache = null;
            _saveRequested = false;
            _stablePath = null;
            _lastLength = -1;
        }

        private bool TickWarming()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("ReadyToRender timeout", true);

            try
            {
                if (_sharedView == null || !_sharedView.ReadyToRender())
                {
                    _settleFrames = 0;
                    return false;
                }

                if (++_settleFrames < SettleFrames)
                    return false;

                _stage = Stage.Exposing;
                _waitFrames = 0;
                _exposeFrames = 0;
                TacticalMapLog.Info("[PhotoNative] view ready; expose window opened.");
                return false;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] warming poll failed.", ex);
                return Fail("warming exception: " + ex.GetType().Name, true);
            }
        }

        private bool TickExposing()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("render timeout", true);

            try
            {
                if (_sharedView == null || !_sharedView.ReadyToRender())
                    return false;

                if (++_exposeFrames < ExposeFrames)
                    return false;

                if (!_saveRequested)
                {
                    _saveRequested = true;
                    ConfigureExport();
                    TacticalMapLog.Info("[PhotoNative] export armed; stopping render after current frame window.");
                }

                DisableView();
                _stage = Stage.Reading;
                _waitFrames = 0;
                return false;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] expose failed.", ex);
                return Fail("expose exception: " + ex.GetType().Name, true);
            }
        }

        private bool TickReading()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("PNG timeout; saveRequested=" + _saveRequested, false);

            try
            {
                if (!_saveRequested || string.IsNullOrEmpty(_savePath))
                    return false;

                if (!File.Exists(_savePath))
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
                    return Fail("PNG exists but pixel validation failed", false);

                TryDelete(_savePath);
                _stage = Stage.Done;
                _completedOnce = true;
                TacticalMapLog.Info("[PhotoNative] capture complete: elapsed=" +
                    (_watch == null ? -1 : _watch.ElapsedMilliseconds) + "ms");
                return true;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] readback failed.", ex);
                return Fail("readback exception: " + ex.GetType().Name, false);
            }
        }

        private void EnsureRenderObjects()
        {
            if (_mission == null || _mission.Scene == null || _cache == null)
                throw new InvalidOperationException("mission/cache unavailable");

            Scene scene = _mission.Scene;
            int texW = PhotoSize;
            int texH = GetPhotoHeight(_cache);
            string key = scene.GetHashCode() + ":" + texW + "x" + texH;

            bool sceneSet = false;
            if (_sharedView != null)
            {
                try
                {
                    _sharedView.SetScene(scene);
                    sceneSet = true;
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Warn("[PhotoNative] existing SceneView rejected scene: " + ex.GetType().Name);
                    _sharedView = null;
                }
            }

            if (!sceneSet)
            {
                _sharedView = SceneView.CreateSceneView();
                _sharedView.SetScene(scene);
                _sharedView.SetAutoDepthTargetCreation(true);
                _sharedView.SetClearColor(4278190080u);
            }

            if (_sharedTarget == null || _sharedTargetKey != key)
            {
                _sharedTarget = Texture.CreateRenderTarget(
                    "TMapPhotoNative",
                    texW,
                    texH,
                    false,
                    false,
                    false,
                    false);
                _sharedTargetKey = key;
            }

            _sharedView.SetRenderTarget(_sharedTarget);
            _sharedView.SetAutoDepthTargetCreation(true);
            _sharedView.SetRenderOnDemand(false);
            _sharedView.SetRenderWithPostfx(false);
            _sharedView.SetSceneUsesSkybox(true);
            _sharedView.SetSceneUsesShadows(true);
            _sharedView.SetSceneUsesContour(false);
            _sharedView.SetClearGbuffer(true);
            _sharedView.DoNotClear(false);

            if (_sharedCamera == null)
                _sharedCamera = Camera.CreateCamera();

            float halfW = _cache.WorldW * 0.5f + 4f;
            float halfH = _cache.WorldH * 0.5f + 4f;
            float centerX = _cache.OriginX + _cache.WorldW * 0.5f;
            float centerY = _cache.OriginY + _cache.WorldH * 0.5f;
            float camZ = _cache.MaxH + 300f;
            float far = camZ + 4000f;

            _sharedCamera.SetViewVolume(false, -halfW, halfW, -halfH, halfH, 1f, far);
            _sharedCamera.LookAt(
                new Vec3(centerX, centerY, camZ),
                new Vec3(centerX, centerY, 0f),
                new Vec3(0f, 1f, 0f));
            _sharedView.SetCamera(_sharedCamera);
        }

        private void ConfigureExport()
        {
            if (_sharedView == null || string.IsNullOrEmpty(_savePath))
                return;

            // Native View export. This is preferable to saving the RenderTarget directly after
            // disabling the view because the engine owns the final-result capture timing.
            _sharedView.SetSaveFinalResultToDisk(true);
            _sharedView.SetFileNameToSaveResult(IoPath.GetFileNameWithoutExtension(_savePath));
            _sharedView.SetFileTypeToSave(View.TextureSaveFormat.TextureTypePng);
            _sharedView.SetFilePathToSaveResult(IoPath.GetDirectoryName(_savePath));
        }

        private bool TryApplyAndValidate(string path)
        {
            using (Bitmap bmp = new Bitmap(path))
            {
                if (bmp.Width <= 0 || bmp.Height <= 0)
                    return false;

                Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = data.Stride;
                    byte[] raw = new byte[bmp.Width * bmp.Height * 4];
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        IntPtr source = IntPtr.Add(data.Scan0, y * stride);
                        int copy = Math.Min(Math.Abs(stride), bmp.Width * 4);
                        Marshal.Copy(source, raw, y * bmp.Width * 4, copy);
                    }

                    PixelStats stats = Analyze(raw);
                    TacticalMapLog.Info("[PhotoNative] PNG validated: " + bmp.Width + "x" + bmp.Height +
                        " avgLum=" + stats.AverageLuminance.ToString("0.0") +
                        " variance=" + stats.Variance.ToString("0.0") +
                        " nonBlack=" + (stats.NonBlackRatio * 100.0).ToString("0.0") + "%" +
                        " min=" + stats.MinValue + " max=" + stats.MaxValue);

                    // Reject the classic failure mode: a valid PNG filled almost entirely with clear color.
                    if (stats.NonBlackRatio < 0.01 || stats.Variance < 2.0 || stats.MaxValue < 8)
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

        private struct PixelStats
        {
            public double AverageLuminance;
            public double Variance;
            public double NonBlackRatio;
            public int MinValue;
            public int MaxValue;
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
            double variance = Math.Max(0.0, sum2 / count - avg * avg);
            return new PixelStats
            {
                AverageLuminance = avg,
                Variance = variance,
                NonBlackRatio = (double)nonBlack / count,
                MinValue = min,
                MaxValue = max
            };
        }

        private void Apply(byte[] raw, int photoW, int photoH)
        {
            bool swapRB = TacticalSettings.Instance.PhotoMapSwapRedBlue;
            int outW = PublishSize;
            int outH = Math.Max(1, (int)Math.Round(outW * (double)_cache.WorldH / _cache.WorldW));
            byte[] output = new byte[outW * outH * 4];

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

            TacticalMapLog.Info("[PhotoNative] publish=" + outW + "x" + outH +
                " avgBefore=" + avgBefore.ToString("0.0") +
                " elapsed=" + (_watch == null ? -1 : _watch.ElapsedMilliseconds) + "ms");

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

        private bool Fail(string reason, bool disable)
        {
            if (disable)
                DisableView();
            TacticalMapLog.Warn("[PhotoNative] FAILED at " + _stage +
                ": " + reason +
                " wait=" + _waitFrames +
                " settle=" + _settleFrames +
                " expose=" + _exposeFrames +
                " elapsed=" + (_watch == null ? -1 : _watch.ElapsedMilliseconds) + "ms");
            _failed = true;
            _stage = Stage.Idle;
            return false;
        }

        private void ResetState()
        {
            DisableView();
            _stage = Stage.Idle;
            _waitFrames = 0;
            _settleFrames = 0;
            _exposeFrames = 0;
            _saveRequested = false;
            _lastLength = -1;
            _stablePath = null;
            _savePath = null;
            _cache = null;
            _mission = null;
            _watch = null;
            _completedOnce = false;
            _failed = false;
        }

        private static void DisableView()
        {
            try
            {
                if (_sharedView != null)
                    _sharedView.SetEnable(false);
            }
            catch
            {
            }
        }

        private static int GetPhotoHeight(TerrainCache cache)
        {
            if (cache == null || cache.WorldW <= 0f || cache.WorldH <= 0f)
                return PhotoSize;
            return Math.Max(64, (int)Math.Round(PhotoSize * (double)cache.WorldH / cache.WorldW));
        }

        private static int ClampInt(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        private static byte ClampByte(double v)
        {
            return (byte)(v < 0 ? 0 : (v > 255 ? 255 : (int)v));
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
