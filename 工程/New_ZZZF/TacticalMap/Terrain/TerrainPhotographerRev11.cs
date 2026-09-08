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
    /// REV12: completely isolated terrain capture.
    /// It creates its own Scene, Camera, Tableau RenderTarget and TableauView.
    /// It never registers Mission.Scene with ThumbnailCreatorView and never touches
    /// ThumbnailRenderRequest or the game's shared thumbnail callback pipeline.
    /// The live Mission.Scene is used only to obtain the scene name and bake metadata.
    /// Agents are therefore not part of the captured photo.
    /// </summary>
    public sealed class TerrainPhotographerRev11
    {
        public static readonly TerrainPhotographerRev11 Instance = new TerrainPhotographerRev11();

        private const int Revision = 12;
        private const int PhotoWidth = 1024;
        private const int MaxWaitFrames = 600;
        private const int WarmupFrames = 8;
        private const int MaxCacheEntries = 8;

        private enum Stage
        {
            Idle,
            WaitingRender,
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
        private Camera _photoCamera;
        private Texture _renderTarget;
        private TableauView _tableauView;

        private int _waitFrames;
        private bool _tableauPainted;
        private bool _saveIssued;
        private bool _failed;
        private string _savePath;
        private string _stablePath;
        private long _stableLength = -1;

        private TerrainPhotographerRev11() { }

        public bool IsCompleted { get { return _stage == Stage.Done; } }
        public bool IsActive { get { return _stage == Stage.WaitingRender || _stage == Stage.Reading; } }
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
                TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " cache hit signature=" + cache.BakeSignature);
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

                _stage = Stage.WaitingRender;
                TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                    " START isolatedScene=" + _photoScene.GetHashCode() +
                    " liveScene=" + mission.Scene.GetHashCode() +
                    " sceneName=" + mission.SceneName +
                    " target=" + PhotoWidth + "x" + GetTargetHeight());
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
                    case Stage.WaitingRender:
                        return TickWaitingRender();
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
                " isolated scene loaded pointer=" + _photoScene.Pointer);
        }

        private void CreatePrivateRenderView()
        {
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

            int width = PhotoWidth;
            int height = GetTargetHeight();
            _tableauPainted = false;

            _renderTarget = TableauView.AddTableau(
                "TacticalMapTerrainPhotoREV12",
                new RenderTargetComponent.TextureUpdateEventHandler(OnTableauPaintNeeded),
                this,
                width,
                height);
            if (_renderTarget == null)
                throw new InvalidOperationException("TableauView.AddTableau returned null.");

            _tableauView = _renderTarget.TableauView;
            if (_tableauView == null)
                throw new InvalidOperationException("RenderTarget.TableauView returned null.");

            _tableauView.SetAutoDepthTargetCreation(true);
            _tableauView.SetScene(_photoScene);
            _tableauView.SetCamera(_photoCamera);
            _tableauView.SetSceneUsesSkybox(false);
            _tableauView.SetSceneUsesShadows(false);
            _tableauView.SetRenderWithPostfx(false);
            _tableauView.SetClearColor(0U);
            _tableauView.SetDeleteAfterRendering(false);
            _tableauView.SetContinuousRendering(true);
            _tableauView.SetDoNotRenderThisFrame(false);
            _tableauView.SetEnable(true);

            TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                " isolated tableau created target=" + width + "x" + height +
                " scene=" + _photoScene.Pointer + " view=" + _tableauView.Pointer);
        }

        private void OnTableauPaintNeeded(Texture sender, EventArgs e)
        {
            if (_stage == Stage.WaitingRender && sender == _renderTarget)
            {
                if (!_tableauPainted)
                    TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " isolated tableau paint requested.");
                _tableauPainted = true;
            }
        }

        private bool TickWaitingRender()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("isolated tableau render timeout; paintRequested=" + _tableauPainted);

            if (_photoScene == null || _tableauView == null || _renderTarget == null)
                return Fail("isolated render objects disappeared");

            // Do not gate the capture on Scene.IsLoadingFinished(). A manually created
            // off-screen Scene can remain in that state even while its Tableau is being
            // rendered. The render lifecycle is driven by the private TableauView itself.
            try { _photoScene.Tick(0.1f); } catch { }
            try { _tableauView.SetDoNotRenderThisFrame(false); } catch { }

            if (!_tableauPainted || _waitFrames < WarmupFrames)
                return false;

            if (!_saveIssued)
            {
                try
                {
                    _renderTarget.SetTextureAsAlwaysValid();
                    _renderTarget.SaveToFile(_savePath, false);
                    _saveIssued = true;
                }
                catch (Exception ex)
                {
                    return Fail("isolated render target save failed: " + ex.Message);
                }

                _tableauView.SetContinuousRendering(false);
                _tableauView.SetEnable(false);
                TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                    " isolated render saved=" + _savePath +
                    " warmup=" + _waitFrames +
                    " paintRequested=" + _tableauPainted);
            }

            StopPrivateRenderer();
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

        private void StopPrivateRenderer()
        {
            if (_tableauView != null)
            {
                try { _tableauView.SetEnable(false); } catch { }
                try { _tableauView.SetContinuousRendering(false); } catch { }
                try { _tableauView.ClearAll(false, false); } catch { }
            }

            if (_renderTarget != null)
            {
                try { _renderTarget.Release(); } catch { }
            }

            if (_photoCamera != null)
            {
                try { _photoCamera.ReleaseCameraEntity(); } catch { }
            }

            _tableauView = null;
            _renderTarget = null;
            _photoCamera = null;
            _photoScene = null;
            _tableauPainted = false;
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
            _tableauPainted = false;
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
