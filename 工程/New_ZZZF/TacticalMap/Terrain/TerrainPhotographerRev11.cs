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
    /// REV12: isolated terrain capture using the same SceneView + RenderTarget path
    /// that was previously proven on the live mission scene, but with a private scene
    /// loaded from Mission.SceneName. It never touches ThumbnailCreatorView,
    /// ThumbnailRenderRequest, or the game's shared thumbnail callback pipeline.
    /// Agents in the live Mission.Scene are therefore not rendered into the photo.
    /// </summary>
    public sealed class TerrainPhotographerRev11
    {
        public static readonly TerrainPhotographerRev11 Instance = new TerrainPhotographerRev11();

        private const int Revision = 12;
        private const int PhotoWidth = 1024;
        private const int WarmupFrames = 30;
        private const int CaptureFrames = 10;
        private const int MaxWaitFrames = 1200;
        private const int MaxCacheEntries = 8;

        private enum Stage
        {
            Idle,
            Warming,
            Capturing,
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
            public double Average;
            public double Variance;
            public double NonBlack;
            public int Min;
            public int Max;
        }

        private static readonly Dictionary<int, CachedPhoto> PhotoCache =
            new Dictionary<int, CachedPhoto>();

        private Stage _stage = Stage.Idle;
        private Mission _mission;
        private TerrainCache _cache;
        private Stopwatch _watch;

        private Scene _photoScene;
        private SceneView _photoView;
        private Camera _photoCamera;
        private Texture _renderTarget;
        private string _targetSizeKey;

        private int _waitFrames;
        private int _settleFrames;
        private int _captureFrames;
        private bool _saveIssued;
        private bool _failed;
        private string _savePath;
        private string _stablePath;
        private long _stableLength = -1;

        private TerrainPhotographerRev11() { }

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

            if (string.IsNullOrEmpty(mission.SceneName))
            {
                _failed = true;
                TacticalMapLog.Error("[PhotoNative] REV=" + Revision + " Mission.SceneName is empty.", null);
                return;
            }

            _mission = mission;
            _cache = cache;
            _watch = Stopwatch.StartNew();

            CachedPhoto cached;
            if (PhotoCache.TryGetValue(cache.BakeSignature, out cached) &&
                cached != null && cached.Rgba != null &&
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
                _stablePath = null;
                _stableLength = -1;

                CreatePrivateScene();
                CreatePrivateRenderView();
                ConfigurePrivateRenderView();

                _stage = Stage.Warming;
                _waitFrames = 0;
                _settleFrames = 0;
                _captureFrames = 0;

                TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                    " START isolatedScene=" + _photoScene.GetHashCode() +
                    " liveScene=" + mission.Scene.GetHashCode() +
                    " sceneName=" + mission.SceneName +
                    " target=" + PhotoWidth + "x" + GetTargetHeight() +
                    " viewReady=" + SafeReady() +
                    " targetValid=" + SafeTargetValid());
            }
            catch (Exception ex)
            {
                _failed = true;
                _stage = Stage.Idle;
                TacticalMapLog.Error("[PhotoNative] REV=" + Revision + " start failed.", ex);
                StopPrivateRenderer();
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
                StopPrivateRenderer();
                _stage = Stage.Idle;
                return false;
            }
        }

        public void OnMissionEnd()
        {
            StopPrivateRenderer();
            CleanupFileOnly();
            _stage = Stage.Idle;
            _mission = null;
            _cache = null;
            _saveIssued = false;
            _failed = false;
            _waitFrames = 0;
            _settleFrames = 0;
            _captureFrames = 0;
            _watch = null;
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " mission end; isolated renderer destroyed.");
        }

        private void CreatePrivateScene()
        {
            _photoScene = Scene.CreateNewScene(false, true, DecalAtlasGroup.Worldmap, "TacticalMapPhotoSceneREV12");
            if (_photoScene == null)
                throw new InvalidOperationException("Scene.CreateNewScene returned null.");

            SceneInitializationData data = new SceneInitializationData(true);
            data.UsePhysicsMaterials = false;
            data.EnableFloraPhysics = false;
            data.UseTerrainMeshBlending = false;
            data.CreateOros = false;

            _photoScene.Read(_mission.SceneName, ref data, "");
            _photoScene.ForceLoadResources(true);
            _photoScene.Tick(0.1f);

            TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                " isolated scene loaded pointer=" + _photoScene.Pointer +
                " loadingFinished=" + SafeSceneLoadingFinished());
        }

        private void CreatePrivateRenderView()
        {
            if (_photoView == null)
                _photoView = SceneView.CreateSceneView();
            if (_photoView == null)
                throw new InvalidOperationException("SceneView.CreateSceneView returned null.");

            try { _photoView.SetEnable(false); } catch { }
            _photoView.SetScene(_photoScene);

            int width = PhotoWidth;
            int height = GetTargetHeight();
            string key = width + "x" + height;

            if (_renderTarget == null || _targetSizeKey != key)
            {
                if (_renderTarget != null)
                {
                    try { _renderTarget.Release(); } catch { }
                }

                _renderTarget = Texture.CreateRenderTarget(
                    "TMapPhotoNative_REV" + Revision,
                    width,
                    height,
                    false,
                    false,
                    false,
                    true);

                if (_renderTarget == null)
                    throw new InvalidOperationException("Texture.CreateRenderTarget returned null.");
                _targetSizeKey = key;
            }

            _renderTarget.SetTextureAsAlwaysValid();
            _photoView.SetRenderTarget(_renderTarget);

            _photoCamera = Camera.CreateCamera();
            if (_photoCamera == null)
                throw new InvalidOperationException("Camera.CreateCamera returned null.");

            float halfW = _cache.WorldW * 0.5f + 4f;
            float halfH = _cache.WorldH * 0.5f + 4f;
            float centerX = _cache.OriginX + _cache.WorldW * 0.5f;
            float centerY = _cache.OriginY + _cache.WorldH * 0.5f;
            float cameraZ = _cache.MaxH + 300f;
            float far = Math.Max(4000f, cameraZ - _cache.MinH + 512f);

            _photoCamera.SetViewVolume(false, -halfW, halfW, -halfH, halfH, 1f, far);
            _photoCamera.LookAt(
                new Vec3(centerX, centerY, cameraZ),
                new Vec3(centerX, centerY, 0f),
                new Vec3(0f, 1f, 0f));
            _photoView.SetCamera(_photoCamera);
        }

        private void ConfigurePrivateRenderView()
        {
            _photoView.SetScene(_photoScene);
            _photoView.SetCamera(_photoCamera);
            _photoView.SetRenderTarget(_renderTarget);
            _photoView.SetRenderOnDemand(false);
            _photoView.SetRenderWithPostfx(false);
            _photoView.SetSceneUsesSkybox(true);
            _photoView.SetSceneUsesShadows(true);
            _photoView.SetSceneUsesContour(false);
            _photoView.SetClearGbuffer(true);
            _photoView.DoNotClear(false);
            _photoView.SetClearAndDisableAfterSucessfullRender(false);
            _photoView.SetDoQuickExposure(true);
            _photoView.SetResolutionScaling(false);
            _photoView.AddClearTask(false);
            _photoView.SetEnable(true);
            _renderTarget.SetTextureAsAlwaysValid();
        }

        private bool TickWarming()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("ReadyToRender timeout; ready=" + SafeReady() +
                    " loadingFinished=" + SafeSceneLoadingFinished());

            try { _photoScene.Tick(0.1f); } catch { }

            if (!SafeReady())
                return false;

            if (++_settleFrames < WarmupFrames)
                return false;

            _stage = Stage.Capturing;
            _waitFrames = 0;
            _captureFrames = 0;
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                " view ready; capture window opened. sceneLoadingFinished=" +
                SafeSceneLoadingFinished() +
                " targetValid=" + SafeTargetValid());
            return false;
        }

        private bool TickCapturing()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("render timeout; captureFrames=" + _captureFrames);

            try { _photoScene.Tick(0.1f); } catch { }
            try { _photoView.SetDoNotRenderThisFrame(false); } catch { }

            if (!SafeReady())
                return false;

            if (++_captureFrames < CaptureFrames)
                return false;

            if (_renderTarget == null || !SafeTargetValid())
                return Fail("RenderTarget invalid before SaveToFile");

            try
            {
                _renderTarget.SetTextureAsAlwaysValid();
                TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                    " saving texture valid=" + SafeTargetValid() +
                    " ready=" + SafeReady() +
                    " loadingFinished=" + SafeSceneLoadingFinished() +
                    " file=" + _savePath);
                _renderTarget.SaveToFile(_savePath, false);
                _saveIssued = true;
            }
            catch (Exception ex)
            {
                return Fail("SaveToFile failed: " + ex.Message);
            }

            DisablePrivateView();
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

        private void DisablePrivateView()
        {
            if (_photoView != null)
            {
                try { _photoView.SetEnable(false); } catch { }
                try { _photoView.SetRenderOnDemand(false); } catch { }
            }
        }

        private void StopPrivateRenderer()
        {
            DisablePrivateView();

            if (_renderTarget != null)
            {
                try { _renderTarget.Release(); } catch { }
            }

            if (_photoCamera != null)
            {
                try { _photoCamera.ReleaseCameraEntity(); } catch { }
            }

            if (_photoView != null)
            {
                try { _photoView.SetScene(null); } catch { }
                try { _photoView.SetCamera(null); } catch { }
                try { _photoView.SetRenderTarget(null); } catch { }
            }

            _renderTarget = null;
            _photoCamera = null;
            _photoView = null;
            _photoScene = null;
            _targetSizeKey = null;
        }

        private int GetTargetHeight()
        {
            return Math.Max(64,
                (int)Math.Round(PhotoWidth * (double)_cache.WorldH /
                Math.Max(1f, _cache.WorldW)));
        }

        private bool ApplyAndValidate(string path)
        {
            using (Bitmap bmp = new Bitmap(path))
            {
                Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = Math.Abs(data.Stride);
                    byte[] raw = new byte[bmp.Width * bmp.Height * 4];
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), raw, y * bmp.Width * 4,
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

                    return ApplyPixels(raw, bmp.Width, bmp.Height);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }

        private bool ApplyPixels(byte[] raw, int sourceW, int sourceH)
        {
            int outW = PhotoWidth;
            int outH = GetTargetHeight();
            byte[] output = new byte[outW * outH * 4];
            bool swap = TacticalSettings.Instance.PhotoMapSwapRedBlue;

            for (int y = 0; y < outH; y++)
            {
                float sy = sourceH - 1f - ((y + 0.5f) * sourceH / outH - 0.5f);
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

            if (PhotoCache.Count >= MaxCacheEntries)
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
            return true;
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
                int v = (raw[i] + raw[i + 1] + raw[i + 2]) / 3;
                sum += v;
                sum2 += (double)v * v;
                count++;
                if (v > 8) nonBlack++;
                if (v < min) min = v;
                if (v > max) max = v;
            }

            if (count == 0)
                return new PixelStats { Min = 0, Max = 0 };

            double average = (double)sum / count;
            double variance = sum2 / count - average * average;
            return new PixelStats
            {
                Average = average,
                Variance = variance < 0.0 ? 0.0 : variance,
                NonBlack = (double)nonBlack / count,
                Min = min,
                Max = max
            };
        }

        private bool SafeReady()
        {
            if (_photoView == null)
                return false;
            try { return _photoView.ReadyToRender(); }
            catch { return false; }
        }

        private bool SafeTargetValid()
        {
            if (_renderTarget == null)
                return false;
            try { return _renderTarget.IsValid; }
            catch { return false; }
        }

        private bool SafeSceneLoadingFinished()
        {
            if (_photoScene == null)
                return false;
            try { return _photoScene.IsLoadingFinished(); }
            catch { return false; }
        }

        private bool Fail(string reason)
        {
            _failed = true;
            TacticalMapLog.Error("[PhotoNative] REV=" + Revision + " failed: " + reason, null);
            StopPrivateRenderer();
            _stage = Stage.Idle;
            return false;
        }

        private void ResetMissionState()
        {
            StopPrivateRenderer();
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
            _savePath = null;
            _stablePath = null;
            _stableLength = -1;
        }

        private void CleanupFileOnly()
        {
            if (!string.IsNullOrEmpty(_savePath))
                TryDelete(_savePath);
            _savePath = null;
            _stablePath = null;
            _stableLength = -1;
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
