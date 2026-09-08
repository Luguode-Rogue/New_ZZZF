using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
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
    /// REV12: no secondary Native Scene. Terrain is captured from live Mission.Scene.
    /// ThumbnailCreatorView is used only as the native capture bridge. Its callback is
    /// chained with the previous callback and restored after the mission ends.
    /// </summary>
    public sealed class TerrainPhotographerRev11
    {
        public static readonly TerrainPhotographerRev11 Instance = new TerrainPhotographerRev11();

        private const int Revision = 12;
        private const int PhotoWidth = 1024;
        private const int MaxWaitFrames = 600;
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

        private static ThumbnailCreatorView _thumbnailView;
        private static Camera _camera;
        private static bool _callbackInstalled;
        private static MemberInfo _callbackMember;
        private static Delegate _previousRenderCallback;

        private string _activeRenderId;
        private Texture _completedTarget;
        private bool _renderCallbackReceived;
        private GameEntity _photoAnchor;

        private Stage _stage = Stage.Idle;
        private Mission _mission;
        private TerrainCache _cache;
        private Stopwatch _watch;
        private int _waitFrames;
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

                EnsureThumbnailRenderer();
                QueueLiveSceneRenderRequest();

                _stage = Stage.WaitingRender;
                TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                    " START liveScene=" + mission.Scene.GetHashCode() +
                    " sceneName=" + mission.SceneName +
                    " target=" + PhotoWidth + "x" + GetTargetHeight());
            }
            catch (Exception ex)
            {
                _failed = true;
                _stage = Stage.Idle;
                TacticalMapLog.Error("[PhotoNative] REV=" + Revision + " start failed.", ex);
                StopPhotoRenderer();
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
                StopPhotoRenderer();
                _stage = Stage.Idle;
                return false;
            }
        }

        public void OnMissionEnd()
        {
            StopPhotoRenderer();
            RestorePreviousRenderCallback();
            _stage = Stage.Idle;
            _mission = null;
            _cache = null;
            _saveIssued = false;
            _failed = false;
            _completedTarget = null;
            _renderCallbackReceived = false;
            _activeRenderId = null;
            _photoAnchor = null;
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " mission end; live-scene capture detached.");
        }

        private void EnsureThumbnailRenderer()
        {
            if (_thumbnailView == null)
                _thumbnailView = ThumbnailCreatorView.CreateThumbnailCreatorView();
            if (_thumbnailView == null)
                throw new InvalidOperationException("ThumbnailCreatorView.CreateThumbnailCreatorView returned null.");

            InstallCallback();
            _thumbnailView.SetEnable(true);

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
        }

        private void QueueLiveSceneRenderRequest()
        {
            if (_mission == null || _mission.Scene == null)
                throw new InvalidOperationException("Live mission scene is unavailable.");

            try { _thumbnailView.ClearRequests(); } catch { }

            _completedTarget = null;
            _renderCallbackReceived = false;
            _activeRenderId = "TMapPhotoLive_REV" + Revision + "_" +
                              _cache.BakeSignature + "_" + Guid.NewGuid().ToString("N");

            _thumbnailView.RegisterScene(_mission.Scene, false);

            _photoAnchor = GameEntity.CreateEmpty(_mission.Scene, false, false, false);
            if (_photoAnchor == null)
                throw new InvalidOperationException("GameEntity.CreateEmpty returned null for live-scene capture.");

            TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                " anchor pointer=" + _photoAnchor.Pointer +
                " target=" + PhotoWidth + "x" + GetTargetHeight());

            ThumbnailRenderRequest request = ThumbnailRenderRequest.CreateWithoutTexture(
                _mission.Scene,
                _camera,
                _photoAnchor,
                _activeRenderId,
                PhotoWidth,
                GetTargetHeight(),
                "TacticalMapTerrainPhotoREV12",
                0);

            _thumbnailView.RegisterRenderRequest(ref request);
        }

        private bool TickWaitingRender()
        {
            if (++_waitFrames > MaxWaitFrames)
                return Fail("thumbnail render timeout; callback=" + _renderCallbackReceived);

            if (!_renderCallbackReceived || _completedTarget == null)
                return false;

            Texture target = _completedTarget;
            _completedTarget = null;

            try { target.SetTextureAsAlwaysValid(); } catch { }

            TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                " render callback received from live Mission.Scene; saving=" + _savePath);

            try
            {
                target.SaveToFile(_savePath, false);
                _saveIssued = true;
            }
            finally
            {
                StopPhotoRenderer();
            }

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

        private void InstallCallback()
        {
            if (_callbackInstalled)
                return;

            try
            {
                Type type = typeof(ThumbnailCreatorView);
                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                FieldInfo field = type.GetField("renderCallback", flags);
                PropertyInfo property = field == null ? type.GetProperty("renderCallback", flags) : null;

                if (field == null && (property == null || !property.CanWrite))
                    throw new MissingMemberException(type.FullName, "renderCallback");

                Type callbackType = field != null ? field.FieldType : property.PropertyType;
                Delegate previous = field != null
                    ? field.GetValue(null) as Delegate
                    : property.GetValue(null, null) as Delegate;

                MethodInfo callbackMethod = typeof(TerrainPhotographerRev11).GetMethod(
                    "OnThumbnailRenderComplete",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Delegate ours = Delegate.CreateDelegate(callbackType, callbackMethod);
                Delegate combined = previous == null ? ours : Delegate.Combine(previous, ours);

                if (field != null)
                    field.SetValue(null, combined);
                else
                    property.SetValue(null, combined, null);

                _previousRenderCallback = previous;
                _callbackMember = field != null ? (MemberInfo)field : property;
                _callbackInstalled = true;

                TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " render callback chained.");
            }
            catch
            {
                _callbackInstalled = false;
                _callbackMember = null;
                _previousRenderCallback = null;
                throw;
            }
        }

        private static void RestorePreviousRenderCallback()
        {
            if (!_callbackInstalled || _callbackMember == null)
                return;

            try
            {
                FieldInfo field = _callbackMember as FieldInfo;
                PropertyInfo property = _callbackMember as PropertyInfo;
                if (field != null)
                    field.SetValue(null, _previousRenderCallback);
                else if (property != null && property.CanWrite)
                    property.SetValue(null, _previousRenderCallback, null);
                TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " render callback restored.");
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] REV=" + Revision + " failed to restore render callback.", ex);
            }
            finally
            {
                _callbackInstalled = false;
                _callbackMember = null;
                _previousRenderCallback = null;
            }
        }

        private static void OnThumbnailRenderComplete(string renderId, Texture renderTarget)
        {
            TerrainPhotographerRev11 instance = Instance;
            if (instance == null || instance._stage != Stage.WaitingRender)
                return;
            if (string.IsNullOrEmpty(instance._activeRenderId) ||
                !string.Equals(instance._activeRenderId, renderId, StringComparison.Ordinal))
                return;

            instance._completedTarget = renderTarget;
            instance._renderCallbackReceived = true;
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " thumbnail callback renderId=" + renderId);
        }

        private void StopPhotoRenderer()
        {
            if (_thumbnailView != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_activeRenderId))
                        _thumbnailView.CancelRequest(_activeRenderId);
                }
                catch { }

                try { _thumbnailView.ClearRequests(); } catch { }
                try { _thumbnailView.SetEnable(false); } catch { }
            }

            _activeRenderId = null;
            _renderCallbackReceived = false;
            _completedTarget = null;
            _photoAnchor = null;
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
            StopPhotoRenderer();
            _stage = Stage.Idle;
            return false;
        }

        private void ResetMissionState()
        {
            StopPhotoRenderer();
            _stage = Stage.Idle;
            _mission = null;
            _cache = null;
            _watch = null;
            _waitFrames = 0;
            _saveIssued = false;
            _failed = false;
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
