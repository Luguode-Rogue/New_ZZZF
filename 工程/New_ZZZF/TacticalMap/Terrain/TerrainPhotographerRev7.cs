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
    /// <summary>REV7: singleton owner + process-lived SceneView/Camera/RenderTarget.</summary>
    public sealed class TerrainPhotographerRev7
    {
        public static readonly TerrainPhotographerRev7 Instance = new TerrainPhotographerRev7();

        private const int Revision = 7;
        private const int PhotoSize = 2048;
        private const int PublishSize = 1024;
        private const int SettleFrames = 45;
        private const int ExposeFrames = 15;
        private const int MaxWaitFrames = 1800;
        private const int CacheLimit = 8;

        private enum Stage { Idle, Warming, Exposing, Reading, Done }

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

        // These are intentionally process-lived. The Mission only changes the bound Scene.
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
        private int _exposeFrames;
        private bool _saveIssued;
        private bool _failed;
        private string _savePath;
        private string _stablePath;
        private long _stableLength = -1;

        private TerrainPhotographerRev7() { }

        public bool IsCompleted { get { return _stage == Stage.Done; } }
        public bool IsActive
        {
            get { return _stage == Stage.Warming || _stage == Stage.Exposing || _stage == Stage.Reading; }
        }
        public bool Failed { get { return _failed; } }

        public void Start(Mission mission, TerrainCache cache)
        {
            ResetState();
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
                TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " cache hit signature=" + cache.BakeSignature);
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
                    " valid=" + SafeValid() +
                    " isRT=" + SafeIsRT());
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
                    case Stage.Warming: return TickWarming();
                    case Stage.Exposing: return TickExposing();
                    case Stage.Reading: return TickReading();
                    default: return false;
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] REV=" + Revision + " tick failed.", ex);
                _failed = true;
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
            _stableLength = -1;
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " mission end; singleton renderer retained.");
        }

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

            _stage = Stage.Exposing;
            _waitFrames = 0;
            _exposeFrames = 0;
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                " view ready; sceneReady=" + SafeSceneReady() +
                " settle=" + SettleFrames);
            return false;
        }

        private bool TickExposing()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("render timeout");
            if (!SafeReady())
                return false;
            if (++_exposeFrames < ExposeFrames)
                return false;

            if (!_saveIssued)
            {
                if (_target == null || !SafeValid())
                    return Fail("RT invalid before save");

                _target.SetTextureAsAlwaysValid();
                _target.SaveToFile(_savePath);
                _saveIssued = true;
                TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                    " SaveToFile issued before disable; valid=" + SafeValid() +
                    " isRT=" + SafeIsRT());
            }

            DisableView();
            _stage = Stage.Reading;
            _waitFrames = 0;
            return false;
        }

        private bool TickReading()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("PNG timeout");
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
            Scene scene = _mission.Scene;
            if (_view == null)
                _view = SceneView.CreateSceneView();
            if (_view == null)
                throw new InvalidOperationException("CreateSceneView returned null.");

            // Always disable before rebinding a Scene used by the previous Mission.
            try { _view.SetEnable(false); } catch { }
            _view.SetScene(scene);

            int width = PhotoSize;
            int height = Math.Max(64, (int)Math.Round(PhotoSize * (double)_cache.WorldH / _cache.WorldW));
            string key = width + "x" + height;
            if (_target == null || _targetSizeKey != key)
            {
                if (_target != null)
                {
                    try { _target.Release(); } catch { }
                }
                _target = Texture.CreateRenderTarget("TMapPhotoNative_REV" + Revision,
                    width, height, false, false, false, true);
                if (_target == null)
                    throw new InvalidOperationException("CreateRenderTarget returned null.");
                _targetSizeKey = key;
            }

            _view.SetRenderTarget(_target);
            if (_camera == null)
                _camera = Camera.CreateCamera();
            if (_camera == null)
                throw new InvalidOperationException("CreateCamera returned null.");

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
            Scene scene = _mission.Scene;
            scene.PreloadForRendering();
            scene.ForceLoadResources(true);
            scene.CalculateEffectiveLighting();

            _view.SetAutoDepthTargetCreation(true);
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
            _view.SetDoNotRenderThisFrame(false);
            _view.SetEnable(true);
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
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), raw, y * bmp.Width * 4,
                            Math.Min(stride, bmp.Width * 4));

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
                    if (PhotoCache.Count >= CacheLimit)
                        PhotoCache.Clear();
                    PhotoCache[_cache.BakeSignature] = new CachedPhoto
                    {
                        Rgba = output,
                        Width = PublishSize,
                        Height = Math.Max(1, (int)Math.Round(PublishSize * (double)_cache.WorldH / _cache.WorldW)),
                        WorldW = _cache.WorldW,
                        WorldH = _cache.WorldH
                    };
                    _cache.ApplyPhotoPixels(output, PhotoCache[_cache.BakeSignature].Width,
                        PhotoCache[_cache.BakeSignature].Height);
                    return true;
                }
                finally { bmp.UnlockBits(data); }
            }
        }

        private byte[] ResizeAndOrient(byte[] raw, int sourceW, int sourceH)
        {
            int outW = PublishSize;
            int outH = Math.Max(1, (int)Math.Round(outW * (double)_cache.WorldH / _cache.WorldW));
            bool swap = TacticalSettings.Instance.PhotoMapSwapRedBlue;
            byte[] output = new byte[outW * outH * 4];

            for (int y = 0; y < outH; y++)
            {
                float sy = (y + 0.5f) * sourceH / outH - 0.5f;
                sy = sourceH - 1f - sy;
                int y0 = Clamp((int)Math.Floor(sy), 0, sourceH - 1);
                int y1 = Clamp(y0 + 1, 0, sourceH - 1);
                float ty = sy - (float)Math.Floor(sy);

                for (int x = 0; x < outW; x++)
                {
                    float sx = (x + 0.5f) * sourceW / outW - 0.5f;
                    int x0 = Clamp((int)Math.Floor(sx), 0, sourceW - 1);
                    int x1 = Clamp(x0 + 1, 0, sourceW - 1);
                    float tx = sx - (float)Math.Floor(sx);

                    int a = (y0 * sourceW + x0) * 4;
                    int b = (y0 * sourceW + x1) * 4;
                    int c = (y1 * sourceW + x0) * 4;
                    int d = (y1 * sourceW + x1) * 4;
                    int o = (y * outW + x) * 4;

                    byte r = Blend(raw[a], raw[b], raw[c], raw[d], tx, ty);
                    byte g = Blend(raw[a + 1], raw[b + 1], raw[c + 1], raw[d + 1], tx, ty);
                    byte bl = Blend(raw[a + 2], raw[b + 2], raw[c + 2], raw[d + 2], tx, ty);
                    output[o] = swap ? bl : r;
                    output[o + 1] = g;
                    output[o + 2] = swap ? r : bl;
                    output[o + 3] = 255;
                }
            }
            return output;
        }

        private bool SafeReady()
        {
            try { return _view != null && _view.ReadyToRender(); }
            catch { return false; }
        }

        private bool SafeSceneReady()
        {
            try { return _view != null && _view.CheckSceneReadyToRender(); }
            catch { return false; }
        }

        private bool SafeValid()
        {
            try { return _target != null && _target.IsValid; }
            catch { return false; }
        }

        private bool SafeIsRT()
        {
            try { return _target != null && _target.IsRenderTarget; }
            catch { return false; }
        }

        private void DisableView()
        {
            try { if (_view != null) _view.SetEnable(false); } catch { }
        }

        private bool Fail(string reason)
        {
            TacticalMapLog.Warn("[PhotoNative] REV=" + Revision +
                " FAILED stage=" + _stage + " reason=" + reason +
                " wait=" + _waitFrames + " settle=" + _settleFrames +
                " expose=" + _exposeFrames);
            _failed = true;
            DisableView();
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
            _saveIssued = false;
            _failed = false;
            _savePath = null;
            _stablePath = null;
            _stableLength = -1;
            _mission = null;
            _cache = null;
            _watch = null;
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
                Average = avg,
                Variance = Math.Max(0, sum2 / count - avg * avg),
                NonBlack = (double)nonBlack / count,
                Min = min,
                Max = max
            };
        }

        private static byte Blend(byte a, byte b, byte c, byte d, float tx, float ty)
        {
            float top = a + (b - a) * tx;
            float bottom = c + (d - c) * tx;
            float value = top + (bottom - top) * ty;
            return (byte)Math.Max(0, Math.Min(255, (int)value));
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
