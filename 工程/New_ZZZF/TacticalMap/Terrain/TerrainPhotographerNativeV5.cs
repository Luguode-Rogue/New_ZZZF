using System;
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
    /// REV=5 experiment: use the already loaded Mission.Scene directly.
    /// The photographer is a process-wide singleton; Tableau/Camera are recreated per mission.
    /// </summary>
    public sealed class TerrainPhotographerNativeV5
    {
        private enum Stage { Idle, WaitingCallback, WaitingFile, Done }

        private const int PhotoRevision = 5;
        private const int PhotoSize = 2048;
        private const int PublishSize = 1024;
        private const int MaxWaitFrames = 600;

        private static readonly TerrainPhotographerNativeV5 _instance = new TerrainPhotographerNativeV5();
        public static TerrainPhotographerNativeV5 Instance { get { return _instance; } }

        private Texture _texture;
        private TableauView _view;
        private Camera _camera;
        private Scene _scene;
        private TerrainCache _cache;
        private string _savePath;
        private TerrainCache _instanceCache;
        private string _instanceSavePath;
        private bool _callbackSeen;
        private bool _saveIssued;
        private bool _nativeError;
        private int _waitFrames;
        private Stage _stage = Stage.Idle;
        private Stopwatch _watch;
        private bool _failed;

        private TerrainPhotographerNativeV5() { }

        public bool IsCompleted { get { return _stage == Stage.Done; } }
        public bool IsActive { get { return _stage == Stage.WaitingCallback || _stage == Stage.WaitingFile; } }
        public bool Failed { get { return _failed; } }

        public void Start(Mission mission, TerrainCache cache)
        {
            ResetState();
            if (mission == null || mission.Scene == null || cache == null || !cache.IsBaked)
                return;

            _instanceCache = cache;
            _scene = mission.Scene;
            _watch = Stopwatch.StartNew();
            _instanceSavePath = IoPath.Combine(IoPath.GetTempPath(), "TMapPhotoNative_REV5_" + cache.BakeSignature + ".png");
            _savePath = _instanceSavePath;
            TryDelete(_savePath);

            try
            {
                CreateObjects(cache);
                Configure();
                _view.SetEnable(true);
                _stage = Stage.WaitingCallback;
                TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision
                    + " START liveScene=" + _scene.GetHashCode()
                    + " sceneName=" + (mission.SceneName ?? "")
                    + " world=" + cache.WorldW.ToString("0.0") + "x" + cache.WorldH.ToString("0.0")
                    + " target=" + PhotoSize + "x" + GetPhotoHeight(cache)
                    + " path=" + _savePath);
            }
            catch (Exception ex)
            {
                _failed = true;
                TacticalMapLog.Error("[PhotoNative] REV=" + PhotoRevision + " START failed.", ex);
                ReleaseNativeResources();
                _stage = Stage.Idle;
            }
        }

        public bool Tick()
        {
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
                if (new FileInfo(_instanceSavePath).Length < 64)
                    return false;

                try
                {
                    if (!ApplyAndValidate(_instanceSavePath))
                        return Fail("PNG pixel validation failed", false);
                    TryDelete(_instanceSavePath);
                    _stage = Stage.Done;
                    TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " DONE elapsed=" + (_watch == null ? -1 : _watch.ElapsedMilliseconds) + "ms");
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
            _scene = null;
            _cache = null;
            _instanceCache = null;
            _savePath = null;
            _instanceSavePath = null;
            _watch = null;
            _failed = false;
            _callbackSeen = false;
            _saveIssued = false;
            _nativeError = false;
            TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " mission resources released; singleton reset.");
        }

        private void CreateObjects(TerrainCache cache)
        {
            int w = PhotoSize;
            int h = GetPhotoHeight(cache);

            ReleaseTableauOnly();
            _texture = TableauView.AddTableau(
                "TMapTerrainPhoto_REV5",
                new RenderTargetComponent.TextureUpdateEventHandler(OnTextureUpdated),
                _scene,
                w,
                h);
            if (_texture == null || _texture.TableauView == null)
                throw new InvalidOperationException("AddTableau returned null");

            _view = _texture.TableauView;
            _camera = Camera.CreateCamera();
            if (_camera == null)
                throw new InvalidOperationException("CreateCamera returned null");

            float halfW = cache.WorldW * 0.5f + 4f;
            float halfH = cache.WorldH * 0.5f + 4f;
            float cx = cache.OriginX + cache.WorldW * 0.5f;
            float cy = cache.OriginY + cache.WorldH * 0.5f;
            float z = cache.MaxH + 300f;
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
                if (sender == null || !ReferenceEquals(sender, _texture) || _view == null || _scene == null || _cache == null)
                    return;

                _callbackSeen = true;
                TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " TEXTURE UPDATE callback liveScene=" + _scene.GetHashCode());

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

                    double avg = count == 0 ? 0 : (double)sum / count;
                    double variance = count == 0 ? 0 : Math.Max(0.0, sum2 / count - avg * avg);
                    double nonBlackRatio = count == 0 ? 0 : (double)nonBlack / count;
                    TacticalMapLog.Info("[PhotoNative] REV=" + PhotoRevision + " PNG=" + bmp.Width + "x" + bmp.Height
                        + " avg=" + avg.ToString("0.0")
                        + " variance=" + variance.ToString("0.0")
                        + " nonBlack=" + (nonBlackRatio * 100.0).ToString("0.0") + "%"
                        + " min=" + min + " max=" + max);

                    if (nonBlackRatio < 0.01 || variance < 2.0 || max < 8)
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

        private void Apply(byte[] raw, int photoW, int photoH)
        {
            int outW = PublishSize;
            int outH = Math.Max(1, (int)Math.Round(outW * (double)_instanceCache.WorldH / _instanceCache.WorldW));
            byte[] output = new byte[outW * outH * 4];
            bool swapRB = TacticalSettings.Instance.PhotoMapSwapRedBlue;

            for (int r = 0; r < outH; r++)
            {
                float fy = (r + 0.5f) * photoH / outH - 0.5f;
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

                    double rr0 = raw[i00] + (raw[i01] - raw[i00]) * tx;
                    double rr1 = raw[i10] + (raw[i11] - raw[i10]) * tx;
                    double gg0 = raw[i00 + 1] + (raw[i01 + 1] - raw[i00 + 1]) * tx;
                    double gg1 = raw[i10 + 1] + (raw[i11 + 1] - raw[i10 + 1]) * tx;
                    double bb0 = raw[i00 + 2] + (raw[i01 + 2] - raw[i00 + 2]) * tx;
                    double bb1 = raw[i10 + 2] + (raw[i11 + 2] - raw[i10 + 2]) * tx;

                    int d = (r * outW + c) * 4;
                    output[d] = ClampByte((swapRB ? bb0 + (bb1 - bb0) * ty : rr0 + (rr1 - rr0) * ty));
                    output[d + 1] = ClampByte(gg0 + (gg1 - gg0) * ty);
                    output[d + 2] = ClampByte((swapRB ? rr0 + (rr1 - rr0) * ty : bb0 + (bb1 - bb0) * ty));
                    output[d + 3] = 255;
                }
            }

            _instanceCache.ApplyPhotoPixels(output, outW, outH);
        }

        private void ResetState()
        {
            ReleaseNativeResources();
            _scene = null;
            _cache = null;
            _instanceCache = null;
            _savePath = null;
            _instanceSavePath = null;
            _callbackSeen = false;
            _saveIssued = false;
            _nativeError = false;
            _waitFrames = 0;
            _watch = null;
            _failed = false;
            _stage = Stage.Idle;
        }

        private bool Fail(string reason, bool release)
        {
            TacticalMapLog.Warn("[PhotoNative] REV=" + PhotoRevision + " FAILED: " + reason
                + " | callback=" + _callbackSeen + " save=" + _saveIssued + " wait=" + _waitFrames);
            if (release) ReleaseNativeResources();
            _failed = true;
            _stage = Stage.Idle;
            return false;
        }

        private void ReleaseTableauOnly()
        {
            try { if (_view != null) _view.SetContinuousRendering(false); } catch { }
            try { if (_view != null) _view.SetEnable(false); } catch { }
            try { if (_texture != null) _texture.Release(); } catch { }
            _texture = null;
            _view = null;
        }

        private void ReleaseNativeResources()
        {
            ReleaseTableauOnly();
            try { if (_camera != null) _camera.ReleaseCamera(); } catch { }
            _camera = null;
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
