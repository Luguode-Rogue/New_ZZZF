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
    public sealed class TerrainPhotographerRev7
    {
        public static readonly TerrainPhotographerRev7 Instance = new TerrainPhotographerRev7();

        private const int Revision = 9;
        private const int PhotoWidth = 2048;
        private const int PublishWidth = 1024;
        private const int SettleFrames = 30;
        private const int CaptureFrames = 10;
        private const int MaxWaitFrames = 1200;
        private const int MaxCacheEntries = 8;

        private enum Stage { Idle, Warming, Capturing, Reading, Done }

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
            public double Average;
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
        private static string _targetSizeKey;

        private Stage _stage = Stage.Idle;
        private Mission _mission;
        private TerrainCache _cache;
        private Stopwatch _watch;
        private int _waitFrames;
        private int _settleFrames;
        private int _captureFrames;
        private bool _saveIssued;
        private bool _failed;
        private string _savePath;
        private string _stablePath;
        private long _stableLength = -1;

        private TerrainPhotographerRev7() { }

        public bool IsCompleted { get { return _stage == Stage.Done; } }

        public bool IsActive
        {
            get
            {
                return _stage == Stage.Warming ||
                       _stage == Stage.Capturing ||
                       _stage == Stage.Reading;
            }
        }

        public bool Failed { get { return _failed; } }

        public void Start(Mission mission, TerrainCache cache)
        {
            ResetMissionState();

            if (mission == null || mission.Scene == null || cache == null || !cache.IsBaked)
                return;

            _mission = mission;
            _cache = cache;
            _watch = Stopwatch.StartNew();

            CachedPhoto cached;
            if (PhotoCache.TryGetValue(cache.BakeSignature, out cached) &&
                cached != null &&
                cached.Rgba != null &&
                Math.Abs(cached.WorldW - cache.WorldW) < 1f &&
                Math.Abs(cached.WorldH - cache.WorldH) < 1f)
            {
                cache.ApplyPhotoPixels(cached.Rgba, cached.Width, cached.Height);
                _stage = Stage.Done;
                TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                    " cache hit signature=" + cache.BakeSignature);
                return;
            }

            try
            {
                _savePath = IoPath.Combine(
                    IoPath.GetTempPath(),
                    "TMapPhotoNative_REV" + Revision + "_" + cache.BakeSignature + ".png");
                TryDelete(_savePath);
                EnsureRenderObjects();
                ConfigureRenderState();
                _stage = Stage.Warming;

                TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                    " START scene=" + mission.Scene.GetHashCode() +
                    " target=" + _target.Width + "x" + _target.Height +
                    " ready=" + SafeReady() +
                    " valid=" + SafeValid());
            }
            catch (Exception ex)
            {
                _failed = true;
                _stage = Stage.Idle;
                TacticalMapLog.Error("[PhotoNative] REV=" + Revision + " start failed.", ex);
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
                    case Stage.Capturing:
                        return TickCapturing();
                    case Stage.Reading:
                        return TickReading();
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _failed = true;
                TacticalMapLog.Error("[PhotoNative] REV=" + Revision + " tick failed.", ex);
                DisableView();
                _stage = Stage.Idle;
                return false;
            }
        }

        public void OnMissionEnd()
        {
            DisableView();
            CleanupFileOnly();
            _stage = Stage.Idle;
            _mission = null;
            _cache = null;
            _saveIssued = false;
            _failed = false;
            _nativeReady = false;
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                " mission end; singleton renderer retained.");
        }

        private bool _nativeReady;

        private bool TickWarming()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("ReadyToRender timeout");

            if (!SafeReady())
            {
                _settleFrames = 0;
                return false;
            }

            if (++_settleFrames < SettleFrames)
                return false;

            _stage = Stage.Capturing;
            _waitFrames = 0;
            _captureFrames = 0;
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                " view ready; capture window opened.");
            return false;
        }

        private bool TickCapturing()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("render timeout");

            if (!SafeReady())
                return false;

            if (++_captureFrames < CaptureFrames)
                return false;

            if (_target == null || !SafeValid())
                return Fail("RenderTarget invalid before SaveToFile");

            _target.SetTextureAsAlwaysValid();
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                " saving texture valid=" + SafeValid() + " file=" + _savePath);

            // Current Bannerlord build exposes SaveToFile(string, bool isRelativePath).
            _target.SaveToFile(_savePath, false);
            _saveIssued = true;
            DisableView();

            _stage = Stage.Reading;
            _waitFrames = 0;
            return false;
        }

        private bool TickReading()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("PNG timeout; saveIssued=" + _saveIssued);

            if (!_saveIssued || string.IsNullOrEmpty(_savePath) || !File.Exists(_savePath))
                return false;

            long length = new FileInfo(_savePath).Length;
            if (length <= 0)
                return false;

            if (_stablePath != _savePath || _stableLength != length)
            {
                _stablePath = _savePath;
                _stableLength = length;
                return false;
            }

            if (!ApplyAndValidate(_savePath))
                return Fail("PNG pixel validation failed");

            TryDelete(_savePath);
            _stage = Stage.Done;
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                " DONE elapsed=" + (_watch == null ? -1 : _watch.ElapsedMilliseconds) + "ms");
            return true;
        }

        private void EnsureRenderObjects()
        {
            if (_view == null)
                _view = SceneView.CreateSceneView();
            if (_view == null)
                throw new InvalidOperationException("SceneView.CreateSceneView returned null.");

            try { _view.SetEnable(false); } catch { }
            _view.SetScene(_mission.Scene);

            int width = PhotoWidth;
            int height = Math.Max(64,
                (int)Math.Round(PhotoWidth * (double)_cache.WorldH / Math.Max(1f, _cache.WorldW)));
            string key = width + "x" + height;

            if (_target == null || _targetSizeKey != key)
            {
                if (_target != null)
                {
                    try { _target.Release(); } catch { }
                }

                _target = Texture.CreateRenderTarget(
                    "TMapPhotoNative_REV" + Revision,
                    width,
                    height,
                    false,
                    false,
                    false,
                    true);

                if (_target == null)
                    throw new InvalidOperationException("Texture.CreateRenderTarget returned null.");
                _targetSizeKey = key;
            }

            _target.SetTextureAsAlwaysValid();
            _view.SetRenderTarget(_target);

            if (_camera == null)
                _camera = Camera.CreateCamera();
            if (_camera == null)
                throw new InvalidOperationException("Camera.CreateCamera returned null.");

            float halfW = _cache.WorldW * 0.5f + 4f;
            float halfH = _cache.WorldH * 0.5f + 4f;
            float centerX = _cache.OriginX + _cache.WorldW * 0.5f;
            float centerY = _cache.OriginY + _cache.WorldH * 0.5f;
            float cameraZ = _cache.MaxH + 300f;
            float far = Math.Max(4000f, cameraZ - _cache.MinH + 512f);

            _camera.SetViewVolume(false, -halfW, halfW, -halfH, halfH, 1f, far);
            _camera.LookAt(
                new Vec3(centerX, centerY, cameraZ),
                new Vec3(centerX, centerY, 0f),
                new Vec3(0f, 1f, 0f));
            _view.SetCamera(_camera);
            _nativeReady = true;
        }

        private void ConfigureRenderState()
        {
            Scene scene = _mission.Scene;
            scene.ForceLoadResources(true);
            _view.SetScene(scene);
            _view.SetCamera(_camera);
            _view.SetRenderTarget(_target);
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

        private bool ApplyAndValidate(string path)
        {
            using (Bitmap bmp = new Bitmap(path))
            {
                Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                BitmapData data = bmp.LockBits(
                    rect,
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);
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
                    TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                        " PNG=" + bmp.Width + "x" + bmp.Height +
                        " avg=" + stats.Average.ToString("0.0") +
                        " variance=" + stats.Variance.ToString("0.0") +
                        " nonBlack=" + (stats.NonBlack * 100.0).ToString("0.0") + "%" +
                        " min=" + stats.Min + " max=" + stats.Max);

                    if (stats.NonBlack < 0.01 || stats.Variance < 2.0 || stats.Max < 8)
                        return false;

                    byte[] output = ResizeAndOrient(raw, bmp.Width, bmp.Height);
                    int outH = Math.Max(1,
                        (int)Math.Round(PublishWidth * (double)_cache.WorldH /
                        Math.Max(1f, _cache.WorldW)));

                    if (PhotoCache.Count >= MaxCacheEntries)
                        PhotoCache.Clear();

                    PhotoCache[_cache.BakeSignature] = new CachedPhoto
                    {
                        Rgba = output,
                        Width = PublishWidth,
                        Height = outH,
                        WorldW = _cache.WorldW,
                        WorldH = _cache.WorldH
                    };

                    _cache.ApplyPhotoPixels(output, PublishWidth, outH);
                    return true;
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }

        private byte[] ResizeAndOrient(byte[] raw, int sourceW, int sourceH)
        {
            int outW = PublishWidth;
            int outH = Math.Max(1,
                (int)Math.Round(outW * (double)_cache.WorldH /
                Math.Max(1f, _cache.WorldW)));
            byte[] output = new byte[outW * outH * 4];
            bool swap = TacticalSettings.Instance.PhotoMapSwapRedBlue;

            for (int y = 0; y < outH; y++)
            {
                float sy = (y + 0.5f) * sourceH / outH - 0.5f;
                sy = sourceH - 1f - sy;
                int iy = Clamp((int)Math.Round(sy), 0, sourceH - 1);

                for (int x = 0; x < outW; x++)
                {
                    float sx = (x + 0.5f) * sourceW / outW - 0.5f;
                    int ix = Clamp((int)Math.Round(sx), 0, sourceW - 1);
                    int source = (iy * sourceW + ix) * 4;
                    int dest = (y * outW + x) * 4;

                    byte r = raw[source];
                    byte g = raw[source + 1];
                    byte b = raw[source + 2];

                    output[dest] = swap ? b : r;
                    output[dest + 1] = g;
                    output[dest + 2] = swap ? r : b;
                    output[dest + 3] = 255;
                }
            }

            return output;
        }

        private static PixelStats Analyze(byte[] raw)
        {
            long sum = 0;
            double sum2 = 0.0;
            int count = 0;
            int nonBlack = 0;
            int min = 255;
            int max = 0;

            for (int i = 0; i + 3 < raw.Length; i += 4)
            {
                int lum = (raw[i] * 299 + raw[i + 1] * 587 + raw[i + 2] * 114) / 1000;
                sum += lum;
                sum2 += (double)lum * lum;
                if (lum >= 8) nonBlack++;
                if (lum < min) min = lum;
                if (lum > max) max = lum;
                count++;
            }

            if (count == 0)
                return new PixelStats();

            double average = (double)sum / count;
            return new PixelStats
            {
                Average = average,
                Variance = Math.Max(0.0, sum2 / count - average * average),
                NonBlack = (double)nonBlack / count,
                Min = min,
                Max = max
            };
        }

        private bool SafeReady()
        {
            if (!_nativeReady || _view == null)
                return false;
            try { return _view.ReadyToRender(); }
            catch { return false; }
        }

        private bool SafeValid()
        {
            if (_target == null)
                return false;
            try { return _target.IsValid; }
            catch { return false; }
        }

        private void DisableView()
        {
            if (_view == null)
                return;
            try { _view.SetEnable(false); } catch { }
            try { _view.SetRenderOnDemand(true); } catch { }
        }

        private void CleanupFileOnly()
        {
            TryDelete(_savePath);
            _savePath = null;
            _stablePath = null;
            _stableLength = -1;
        }

        private bool Fail(string reason)
        {
            _failed = true;
            TacticalMapLog.Warn("[PhotoNative] REV=" + Revision +
                " FAILED: " + reason +
                " stage=" + _stage +
                " saveIssued=" + _saveIssued);
            DisableView();
            CleanupFileOnly();
            _stage = Stage.Idle;
            return false;
        }

        private void ResetMissionState()
        {
            DisableView();
            CleanupFileOnly();
            _stage = Stage.Idle;
            _mission = null;
            _cache = null;
            _watch = null;
            _waitFrames = 0;
            _settleFrames = 0;
            _captureFrames = 0;
            _saveIssued = false;
            _failed = false;
            _nativeReady = false;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try { File.Delete(path); } catch { }
        }
    }
}
