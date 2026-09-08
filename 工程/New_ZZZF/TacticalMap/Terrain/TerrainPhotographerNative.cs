using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using IoPath = System.IO.Path;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// Real terrain photo capture.
    /// The photographer object is a process-wide singleton, but every mission owns a fresh
    /// Scene/Texture/Camera set. Native handles are never carried from one battle to another.
    /// </summary>
    public sealed class TerrainPhotographerNative
    {
        private enum Stage { Idle, LoadingScene, WaitingCallback, WaitingFile, Done }

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

        private const int PhotoRevision = 3;
        private const int PhotoSize = 2048;
        private const int PublishSize = 1024;
        private const int CacheLimit = 8;
        private const int MaxWaitFrames = 1800;

        private static readonly TerrainPhotographerNative _instance = new TerrainPhotographerNative();
        private static readonly Dictionary<int, CachedPhoto> PhotoCache = new Dictionary<int, CachedPhoto>();

        public static TerrainPhotographerNative Instance { get { return _instance; } }

        private Texture _texture;
        private TableauView _view;
        private Camera _camera;
        private Scene _scene;
        private TerrainCache _cache;
        private string _savePath;
        private string _sizeKey;
        private bool _callbackSeen;
        private bool _saveIssued;
        private bool _nativeError;

        private Stage _stage = Stage.Idle;
        private int _waitFrames;
        private Stopwatch _watch;
        private string _instanceSavePath;
        private TerrainCache _instanceCache;
        private bool _failed;

        private TerrainPhotographerNative() { }

        public bool IsCompleted { get { return _stage == Stage.Done; } }
        public bool IsActive { get { return _stage == Stage.LoadingScene || _stage == Stage.WaitingCallback || _stage == Stage.WaitingFile; } }
        public bool Failed { get { return _failed; } }

        public void Start(Mission mission, TerrainCache cache)
        {
            Reset();
            if (mission == null || mission.Scene == null || cache == null || !cache.IsBaked)
                return;

            _instanceCache = cache;
            _watch = Stopwatch.StartNew();

            CachedPhoto cached;
            if (PhotoCache.TryGetValue(cache.BakeSignature, out cached) && cached != null
                && Math.Abs(cached.WorldW - cache.WorldW) < 1f
                && Math.Abs(cached.WorldH - cache.WorldH) < 1f)
            {
                cache.ApplyPhotoPixels(cached.Rgba, cached.Width, cached.Height);
                _stage = Stage.Done;
                TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " cache hit signature=" + cache.BakeSignature);
                return;
            }

            try
            {
                _instanceSavePath = IoPath.Combine(IoPath.GetTempPath(),
                    "TMapPhotoNative_" + cache.BakeSignature + ".png");
                TryDelete(_instanceSavePath);

                CreateMissionScene(mission);
                EnsureObjects();

                _stage = Stage.LoadingScene;
                TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision
                    + " START sourceScene=" + mission.Scene.GetHashCode()
                    + " photoScene=" + _scene.GetHashCode()
                    + " sceneName=" + (mission.SceneName ?? "")
                    + " world=" + cache.WorldW.ToString("0.0") + "x" + cache.WorldH.ToString("0.0")
                    + " target=" + PhotoSize + "x" + GetPhotoHeight(cache)
                    + " path=" + _instanceSavePath);
            }
            catch (Exception ex)
            {
                _failed = true;
                _stage = Stage.Idle;
                TacticalMapLog.Error("[PhotoNative] REV=" + PhotoRevision + " START failed.", ex);
                ReleaseNativeResources();
            }
        }

        public bool Tick()
        {
            if (_stage == Stage.LoadingScene)
            {
                if (_scene == null)
                    return Fail("photo scene is null while loading", true);

                if (++_waitFrames > MaxWaitFrames)
                    return Fail("photo scene loading timeout", true);

                try
                {
                    if (!_scene.IsLoadingFinished())
                        return false;

                    Configure();
                    _view.SetEnable(true);
                    _stage = Stage.WaitingCallback;
                    _waitFrames = 0;
                    TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " photo scene loaded; tableau enabled.");
                }
                catch (Exception ex)
                {
                    return Fail("photo scene render setup failed: " + ex.GetType().Name, true);
                }
                return false;
            }

            if (_stage == Stage.WaitingCallback)
            {
                if (++_waitFrames > MaxWaitFrames)
                    return Fail("texture update callback timeout", true);
                if (_nativeError)
                    return Fail("native tableau error", true);
                if (!_callbackSeen)
                    return false;
                if (!_saveIssued)
                    return Fail("callback arrived but SaveToFile was not issued", true);
                _stage = Stage.WaitingFile;
                _waitFrames = 0;
                TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " render callback confirmed; waiting for PNG.");
                return false;
            }

            if (_stage == Stage.WaitingFile)
            {
                if (++_waitFrames > MaxWaitFrames)
                    return Fail("PNG timeout", false);
                if (string.IsNullOrEmpty(_instanceSavePath) || !File.Exists(_instanceSavePath))
                    return false;

                long len = new FileInfo(_instanceSavePath).Length;
                if (len < 64)
                    return false;

                try
                {
                    if (!ApplyAndValidate(_instanceSavePath))
                        return Fail("PNG pixel validation failed", false);

                    TryDelete(_instanceSavePath);
                    _stage = Stage.Done;
                    TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " DONE elapsed=" +
                        (_watch == null ? -1 : _watch.ElapsedMilliseconds) + "ms");
                    return true;
                }
                catch (Exception ex)
                {
                    return Fail("PNG read failed: " + ex.GetType().Name, false);
                }
            }

            return false;
        }

        public void OnMissionEnd()
        {
            ReleaseNativeResources();
            _stage = Stage.Idle;
            _waitFrames = 0;
            _instanceCache = null;
            _instanceSavePath = null;
            _watch = null;
            _failed = false;
            TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " mission resources released; singleton reset.");
        }

        private void CreateMissionScene(Mission mission)
        {
            ReleaseNativeResources();

            // Keep the offscreen renderer out of the live Mission.Scene. Re-entering a battle
            // must never leave the Tableau pointing at a scene owned by the previous Mission.
            _scene = Scene.CreateNewScene(false, false);
            if (_scene == null)
                throw new InvalidOperationException("CreateNewScene returned null");

            _scene.SetOwnerThread();
            _scene.Read(mission.SceneName);

            if (!string.IsNullOrEmpty(mission.SceneLevels))
            {
                var levels = new List<string>();
                string[] parts = mission.SceneLevels.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    string level = parts[i].Trim();
                    if (level.Length > 0) levels.Add(level);
                }
                if (levels.Count > 0)
                    _scene.SetActiveVisibilityLevels(levels);
            }

            _scene.SetDefaultLighting();
            _scene.CalculateEffectiveLighting();
            _scene.ForceLoadResources(true);
        }

        private void EnsureObjects()
        {
            int w = PhotoSize;
            int h = GetPhotoHeight(_instanceCache);
            string key = w + "x" + h;

            // A tableau is always bound to the current singleton session. Recreate it when
            // the resolution changes or when the previous native handle has been released.
            if (_texture == null || _sizeKey != key || _texture.TableauView == null)
            {
                ReleaseTableauOnly();
                _texture = TableauView.AddTableau(
                    "TMapTerrainPhoto",
                    new RenderTargetComponent.TextureUpdateEventHandler(OnTextureUpdated),
                    _scene,
                    w,
                    h);
                if (_texture == null || _texture.TableauView == null)
                    throw new InvalidOperationException("AddTableau returned null");
                _view = _texture.TableauView;
                _sizeKey = key;
            }
            else
            {
                _view = _texture.TableauView;
            }

            if (_camera == null)
                _camera = Camera.CreateCamera();
            if (_camera == null)
                throw new InvalidOperationException("CreateCamera returned null");

            _view.SetEnable(false);
            _view.SetScene(_scene);

            float halfW = _instanceCache.WorldW * 0.5f + 4f;
            float halfH = _instanceCache.WorldH * 0.5f + 4f;
            float cx = _instanceCache.OriginX + _instanceCache.WorldW * 0.5f;
            float cy = _instanceCache.OriginY + _instanceCache.WorldH * 0.5f;
            float z = _instanceCache.MaxH + 300f;
            float far = z + 4000f;

            _camera.SetViewVolume(false, -halfW, halfW, -halfH, halfH, 1f, far);
            _camera.LookAt(new Vec3(cx, cy, z), new Vec3(cx, cy, 0f), new Vec3(0f, 1f, 0f));
            _view.SetCamera(_camera);
        }

        private void Configure()
        {
            _view.SetScene(_scene);
            _view.SetCamera(_camera);
            _view.SetSceneUsesSkybox(true);
            _view.SetSceneUsesShadows(true);
            _view.SetSceneUsesContour(false);
            _view.SetClearGbuffer(true);
            _view.DoNotClear(false);
            _view.SetRenderWithPostfx(false);
            _view.SetDeleteAfterRendering(false);
            _view.SetDoNotRenderThisFrame(false);
            _view.SetClearColor(4278190080u);
            _view.SetContinuousRendering(true);
        }

        private void OnTextureUpdated(Texture sender, EventArgs e)
        {
            try
            {
                // Ignore callbacks from a texture belonging to an older mission/session.
                if (sender == null || !ReferenceEquals(sender, _texture) || _view == null || _scene == null || _cache == null)
                    return;

                _callbackSeen = true;
                TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " TEXTURE UPDATE callback photoScene=" + _scene.GetHashCode());

                if (!_saveIssued && !string.IsNullOrEmpty(_savePath))
                {
                    try
                    {
                        sender.SaveToFile(_savePath, false);
                        _saveIssued = true;
                        _view.SetContinuousRendering(false);
                        _view.SetEnable(false);
                        TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " CALLBACK SaveToFile issued: " + _savePath);
                    }
                    catch (Exception ex)
                    {
                        _nativeError = true;
                        TacticalMapLog.Error("[PhotoNative] REV=" + PhotoRevision + " CALLBACK SaveToFile failed.", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _nativeError = true;
                TacticalMapLog.Error("[PhotoNative] REV=" + PhotoRevision + " CALLBACK failed.", ex);
            }
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
                        IntPtr src = IntPtr.Add(data.Scan0, y * stride);
                        Marshal.Copy(src, raw, y * bmp.Width * 4, Math.Min(stride, bmp.Width * 4));
                    }

                    PixelStats s = Analyze(raw);
                    TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " PNG=" + bmp.Width + "x" + bmp.Height
                        + " avg=" + s.Avg.ToString("0.0")
                        + " variance=" + s.Variance.ToString("0.0")
                        + " nonBlack=" + (s.NonBlack * 100.0).ToString("0.0") + "%"
                        + " min=" + s.Min + " max=" + s.Max);

                    if (s.NonBlack < 0.01 || s.Variance < 2.0 || s.Max < 8)
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
                int lum = (raw[i] * 299 + raw[i + 1] * 587 + raw[i + 2] * 114) / 1000;
                sum += lum;
                sum2 += (double)lum * lum;
                if (lum >= 8) nonBlack++;
                if (lum < min) min = lum;
                if (lum > max) max = lum;
                count++;
            }

            if (count == 0) return new PixelStats();
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
            bool swapRB = TacticalSettings.Instance.PhotoMapSwapRedBlue;
            int outW = PublishSize;
            int outH = Math.Max(1, (int)Math.Round(outW * (double)_instanceCache.WorldH / _instanceCache.WorldW));
            byte[] output = new byte[outW * outH * 4];

            for (int r = 0; r < outH; r++)
            {
                float fySouth = (r + 0.5f) * photoH / outH - 0.5f;
                float fy = photoH - 1f - fySouth;
                int y0 = (int)Math.Floor(fy);
                float ty = fy - y0;
                int y0c = Clamp(y0, 0, photoH - 1);
                int y1c = Clamp(y0 + 1, 0, photoH - 1);

                for (int c = 0; c < outW; c++)
                {
                    float fx = (c + 0.5f) * photoW / outW - 0.5f;
                    int x0 = (int)Math.Floor(fx);
                    float tx = fx - x0;
                    int x0c = Clamp(x0, 0, photoW - 1);
                    int x1c = Clamp(x0 + 1, 0, photoW - 1);

                    int i00 = (y0c * photoW + x0c) * 4;
                    int i01 = (y0c * photoW + x1c) * 4;
                    int i10 = (y1c * photoW + x0c) * 4;
                    int i11 = (y1c * photoW + x1c) * 4;

                    float r0 = raw[i00] + (raw[i01] - raw[i00]) * tx;
                    float r1 = raw[i10] + (raw[i11] - raw[i10]) * tx;
                    float g0 = raw[i00 + 1] + (raw[i01 + 1] - raw[i00 + 1]) * tx;
                    float g1 = raw[i10 + 1] + (raw[i11 + 1] - raw[i10 + 1]) * tx;
                    float b0 = raw[i00 + 2] + (raw[i01 + 2] - raw[i00 + 2]) * tx;
                    float b1 = raw[i10 + 2] + (raw[i11 + 2] - raw[i10 + 2]) * tx;

                    int dst = (r * outW + c) * 4;
                    output[dst] = ClampByte(swapRB ? b0 + (b1 - b0) * ty : r0 + (r1 - r0) * ty);
                    output[dst + 1] = ClampByte(g0 + (g1 - g0) * ty);
                    output[dst + 2] = ClampByte(swapRB ? r0 + (r1 - r0) * ty : b0 + (b1 - b0) * ty);
                    output[dst + 3] = 255;
                }
            }

            if (PhotoCache.Count >= CacheLimit) PhotoCache.Clear();
            PhotoCache[_instanceCache.BakeSignature] = new CachedPhoto
            {
                Rgba = output,
                Width = outW,
                Height = outH,
                WorldW = _instanceCache.WorldW,
                WorldH = _instanceCache.WorldH
            };
            _instanceCache.ApplyPhotoPixels(output, outW, outH);
        }

        private void Reset()
        {
            // A reset is only allowed to tear down resources belonging to the current singleton
            // session; it never creates or retains native handles across missions.
            ReleaseNativeResources();
            _stage = Stage.Idle;
            _waitFrames = 0;
            _instanceSavePath = null;
            _instanceCache = null;
            _watch = null;
            _failed = false;
            _callbackSeen = false;
            _saveIssued = false;
            _nativeError = false;
            _savePath = null;
            _cache = null;
        }

        private bool Fail(string reason, bool disable)
        {
            TacticalMapLog.Warn("[PhotoNative] REV=" + PhotoRevision + " FAILED: " + reason
                + " | callback=" + _callbackSeen + " save=" + _saveIssued
                + " wait=" + _waitFrames);
            if (disable) Disable();
            _failed = true;
            _stage = Stage.Idle;
            return false;
        }

        private void Disable()
        {
            try
            {
                if (_view != null)
                {
                    _view.SetContinuousRendering(false);
                    _view.SetEnable(false);
                }
            }
            catch { }
        }

        private void ReleaseTableauOnly()
        {
            try
            {
                if (_view != null)
                {
                    _view.SetContinuousRendering(false);
                    _view.SetEnable(false);
                }
            }
            catch { }

            try { if (_texture != null) _texture.Release(); } catch { }
            _texture = null;
            _view = null;
            _sizeKey = null;
        }

        private void ReleaseNativeResources()
        {
            Disable();
            ReleaseTableauOnly();

            try
            {
                if (_camera != null)
                    _camera.ReleaseCamera();
            }
            catch { }
            _camera = null;

            try
            {
                if (_scene != null)
                    _scene.ClearAll();
            }
            catch { }
            _scene = null;
        }

        private static int GetPhotoHeight(TerrainCache cache)
        {
            return Math.Max(64, (int)Math.Round(PhotoSize * (double)cache.WorldH / Math.Max(1.0, cache.WorldW)));
        }

        private static int Clamp(int v, int min, int max)
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
            try { File.Delete(path); } catch { }
        }
    }
}
