using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.BattleHud;
using New_ZZZF.TacticalMap.Diagnostics;
using New_ZZZF.TacticalMap.UI;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// Uses Bannerlord's native photo-mode screenshot path. The live combat camera is
    /// temporarily driven by an owned orthographic camera; all borrowed state is restored
    /// immediately after the screenshot request.
    /// </summary>
    public sealed class TerrainPhotographerRev20
    {
        public static readonly TerrainPhotographerRev20 Instance = new TerrainPhotographerRev20();

        private const int Revision = 27;
        private const int PhotoWidth = 1024;
        private const int ConfirmedRenderFrames = 4;
        private const int HiddenMillisecondsBeforeShot = 250;
        private const int MaxWaitFrames = 7200;

        private enum Stage { Idle, Arming, Shooting, Reading, Done }

        private readonly Dictionary<Agent, MBAgentVisuals> _hiddenAgents =
            new Dictionary<Agent, MBAgentVisuals>();

        private Stage _stage;
        private Mission _mission;
        private MissionScreen _screen;
        private TerrainCache _cache;
        private Camera _mapCamera;
        private MatrixFrame _mapFrame;
        private float _viewHalfW;
        private float _viewHalfH;
        private int _frames;
        private int _confirmedRenderFrames;
        private long _hiddenSinceTimestamp;
        private string _photoPath;
        private long _stableLength = -1;
        private bool _presentationChanged;
        private bool _htmlUiSuspended;
        private bool _previousUiHidden;
        private bool _previousPhotoMode;
        private float _previousFocus;
        private float _previousFocusStart;
        private float _previousFocusEnd;
        private float _previousExposure;
        private bool _previousVignette;
        private bool _failed;
        private Stopwatch _watch;

        private TerrainPhotographerRev20() { }

        public bool IsCompleted { get { return _stage == Stage.Done; } }
        public bool IsActive { get { return _stage == Stage.Arming || _stage == Stage.Shooting || _stage == Stage.Reading; } }
        public bool Failed { get { return _failed; } }

        public void Start(Mission mission, TerrainCache cache, MissionScreen screen)
        {
            Reset();
            if (mission == null || mission.Scene == null || cache == null || !cache.IsBaked ||
                screen == null || screen.CombatCamera == null)
                return;

            try
            {
                _mission = mission;
                _cache = cache;
                _screen = screen;
                _watch = Stopwatch.StartNew();
                _mapCamera = Camera.CreateCamera();
                if (_mapCamera == null) throw new InvalidOperationException("Camera.CreateCamera returned null.");

                float centerX = cache.OriginX + cache.WorldW * 0.5f;
                float centerY = cache.OriginY + cache.WorldH * 0.5f;
                float screenAspect = Math.Max(0.1f, Screen.AspectRatio);
                _viewHalfW = cache.WorldW * 0.5f;
                _viewHalfH = cache.WorldH * 0.5f;
                if (_viewHalfW / _viewHalfH < screenAspect)
                    _viewHalfW = _viewHalfH * screenAspect;
                else
                    _viewHalfH = _viewHalfW / screenAspect;

                _mapFrame = MatrixFrame.Identity;
                _mapFrame.origin = new Vec3(centerX, centerY, cache.MaxH + 50f);
                ApplyOrthographicCamera();
                _stage = Stage.Arming;
                TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                    " START path=NATIVE_PHOTO projection=ORTHOGRAPHIC screenAspect=" + screenAspect +
                    " mapBounds=" + cache.OriginX + "," + cache.OriginY + "," + cache.WorldW + "," + cache.WorldH +
                    " view=" + (_viewHalfW * 2f) + "x" + (_viewHalfH * 2f));
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] REV=" + Revision + " start failed.", ex);
                FailAndRestore();
            }
        }

        public bool Tick()
        {
            try
            {
                if (_mission == null || _screen == null || _mission.Scene == null)
                    return FailAndRestore();
                if (++_frames > MaxWaitFrames)
                    return FailAndRestore("timeout stage=" + _stage);

                switch (_stage)
                {
                    case Stage.Arming: return TickArming();
                    case Stage.Shooting: return TickShooting();
                    case Stage.Reading: return TickReading();
                    default: return false;
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] REV=" + Revision + " tick failed.", ex);
                return FailAndRestore();
            }
        }

        private bool TickArming()
        {
            ApplyOrthographicCamera();
            MatrixFrame renderedFrame = _mission.Scene.LastFinalRenderCameraFrame;
            Vec3 renderedDirection = -renderedFrame.rotation.u;
            bool loadingFinished = _screen.MissionLoadingWindowDisabled() &&
                !LoadingWindow.IsLoadingWindowActive;
            bool cameraRendered = renderedFrame.origin.DistanceSquared(_mapFrame.origin) < 1f &&
                Vec3.DotProduct(renderedDirection, _mapCamera.Direction) > 0.999f;
            bool shadersReady = Utilities.GetNumberOfShaderCompilationsInProgress() == 0;

            if (!_screen.MissionStartedRendering() || !loadingFinished || !cameraRendered || !shadersReady)
            {
                _confirmedRenderFrames = 0;
                if (_frames == 1 || _frames % 120 == 0)
                {
                    TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                        " WAIT started=" + _screen.MissionStartedRendering() +
                        " loadingFinished=" + loadingFinished +
                        " cameraRendered=" + cameraRendered +
                        " shadersReady=" + shadersReady +
                        " finalOrigin=" + renderedFrame.origin +
                        " finalDirection=" + renderedDirection);
                }
                return false;
            }

            if (++_confirmedRenderFrames < ConfirmedRenderFrames) return false;

            HidePresentation();
            _stage = Stage.Shooting;
            _frames = 0;
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision +
                " ARMED direction=" + _mapCamera.Direction + " hiddenAgents=" + _hiddenAgents.Count);
            return false;
        }

        private bool TickShooting()
        {
            ApplyOrthographicCamera();
            KeepAgentsHidden();
            MBDebug.DisableAllUI = true;
            double hiddenMilliseconds = (Stopwatch.GetTimestamp() - _hiddenSinceTimestamp) *
                1000.0 / Stopwatch.Frequency;
            if (hiddenMilliseconds < HiddenMillisecondsBeforeShot) return false;

            _photoPath = _mission.Scene.TakePhotoModePicture(false, false, false);
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " SHOT path=" + _photoPath);
            if (string.IsNullOrEmpty(_photoPath)) return FailAndRestore("native photo returned empty path");
            _stage = Stage.Reading;
            _frames = 0;
            return false;
        }

        private bool TickReading()
        {
            ApplyOrthographicCamera();
            KeepAgentsHidden();
            MBDebug.DisableAllUI = true;
            if (!File.Exists(_photoPath)) return false;
            long length = new FileInfo(_photoPath).Length;
            if (length <= 0 || length != _stableLength)
            {
                _stableLength = length;
                return false;
            }

            using (Bitmap bmp = new Bitmap(_photoPath))
                ApplyScreenshot(bmp);
            _stage = Stage.Done;
            RestorePresentation();
            ReleaseOwnedCamera();
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " DONE source=" +
                new FileInfo(_photoPath).Name + " elapsed=" + _watch.ElapsedMilliseconds + "ms");
            return true;
        }

        private void ApplyOrthographicCamera()
        {
            _mapCamera.Frame = _mapFrame;
            _mapCamera.SetViewVolume(false, -_viewHalfW, _viewHalfW, -_viewHalfH, _viewHalfH,
                1f, Math.Max(4000f, _mapFrame.origin.Z - _cache.MinH + 512f));
        }

        private void HidePresentation()
        {
            _previousUiHidden = MBDebug.DisableAllUI;
            _previousPhotoMode = _mission.Scene.GetPhotoModeOn();
            _mission.Scene.GetPhotoModeFocus(ref _previousFocusStart, ref _previousFocusEnd,
                ref _previousFocus, ref _previousExposure, ref _previousVignette);
            MBDebug.DisableAllUI = true;
            _mission.Scene.SetPhotoModeOn(true);
            _mission.Scene.SetPhotoModeFocus(0f, 0f, 0f, 0f);
            _presentationChanged = true;
            _hiddenSinceTimestamp = Stopwatch.GetTimestamp();
            _htmlUiSuspended = true;
            TacticalMapHtmlUi.Instance.SetCaptureSuspended(true);
            BattleHudHtmlUi.Instance.SetCaptureSuspended(true);
            KeepAgentsHidden();
        }

        private void KeepAgentsHidden()
        {
            foreach (Agent agent in _mission.AllAgents)
            {
                MBAgentVisuals visuals = agent.AgentVisuals;
                if (visuals == null) continue;
                if (!_hiddenAgents.ContainsKey(agent) && visuals.GetVisible())
                    _hiddenAgents.Add(agent, visuals);
                if (_hiddenAgents.ContainsKey(agent)) visuals.SetVisible(false);
            }
        }

        private void RestorePresentation()
        {
            if (_presentationChanged && _mission != null && _mission.Scene != null)
            {
                try
                {
                    _mission.Scene.SetPhotoModeFocus(_previousFocusStart, _previousFocusEnd,
                        _previousFocus, _previousExposure);
                    _mission.Scene.SetPhotoModeVignette(_previousVignette);
                    _mission.Scene.SetPhotoModeOn(_previousPhotoMode);
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Error("[PhotoNative] REV=" + Revision +
                        " photo state restore failed.", ex);
                }
                MBDebug.DisableAllUI = _previousUiHidden;
            }

            if (_mission != null)
            {
                foreach (Agent agent in _mission.AllAgents)
                {
                    MBAgentVisuals visuals;
                    if (_hiddenAgents.TryGetValue(agent, out visuals) &&
                        ReferenceEquals(agent.AgentVisuals, visuals))
                    {
                        try { visuals.SetVisible(true); }
                        catch (Exception ex)
                        {
                            TacticalMapLog.Error("[PhotoNative] REV=" + Revision +
                                " agent visibility restore failed.", ex);
                        }
                    }
                }
            }
            _hiddenAgents.Clear();
            _presentationChanged = false;
            if (_htmlUiSuspended)
            {
                try { TacticalMapHtmlUi.Instance.SetCaptureSuspended(false); }
                catch (Exception ex)
                {
                    TacticalMapLog.Error("[PhotoNative] REV=" + Revision +
                        " tactical map resume failed.", ex);
                }
                try { BattleHudHtmlUi.Instance.SetCaptureSuspended(false); }
                catch (Exception ex)
                {
                    TacticalMapLog.Error("[PhotoNative] REV=" + Revision +
                        " battle HUD resume failed.", ex);
                }
                _htmlUiSuspended = false;
            }
        }

        private void ApplyScreenshot(Bitmap bmp)
        {
            int cropW = Math.Max(1, Math.Min(bmp.Width,
                (int)Math.Round(bmp.Width * _cache.WorldW / (_viewHalfW * 2f))));
            int cropH = Math.Max(1, Math.Min(bmp.Height,
                (int)Math.Round(bmp.Height * _cache.WorldH / (_viewHalfH * 2f))));
            int cropX = (bmp.Width - cropW) / 2;
            int cropY = (bmp.Height - cropH) / 2;
            int outH = Math.Max(64, (int)Math.Round(PhotoWidth * (double)_cache.WorldH / _cache.WorldW));
            byte[] rgba = new byte[PhotoWidth * outH * 4];
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < outH; y++)
                {
                    int sy = cropY + cropH - 1 - Math.Min(cropH - 1, y * cropH / outH);
                    for (int x = 0; x < PhotoWidth; x++)
                    {
                        int sx = cropX + Math.Min(cropW - 1, x * cropW / PhotoWidth);
                        int dest = (y * PhotoWidth + x) * 4;
                        IntPtr pixel = IntPtr.Add(data.Scan0, sy * data.Stride + sx * 4);
                        rgba[dest] = Marshal.ReadByte(pixel, 2);
                        rgba[dest + 1] = Marshal.ReadByte(pixel, 1);
                        rgba[dest + 2] = Marshal.ReadByte(pixel, 0);
                        rgba[dest + 3] = 255;
                    }
                }
            }
            finally { bmp.UnlockBits(data); }
            _cache.ApplyPhotoPixels(rgba, PhotoWidth, outH);
            TacticalMapLog.Info("[PhotoNative] REV=" + Revision + " CROP source=" + bmp.Width + "x" + bmp.Height +
                " rect=" + cropX + "," + cropY + "," + cropW + "," + cropH +
                " output=" + PhotoWidth + "x" + outH);
        }

        public void OnMissionEnd() { Reset(); }

        private bool FailAndRestore(string reason = null)
        {
            if (reason != null) TacticalMapLog.Error("[PhotoNative] REV=" + Revision + " failed: " + reason, null);
            _failed = true;
            RestorePresentation();
            ReleaseOwnedCamera();
            _stage = Stage.Idle;
            return false;
        }

        private void ReleaseOwnedCamera()
        {
            if (_mapCamera != null)
            {
                try { _mapCamera.ReleaseCameraEntity(); } catch { }
                _mapCamera = null;
            }
        }

        private void Reset()
        {
            RestorePresentation();
            ReleaseOwnedCamera();
            _stage = Stage.Idle;
            _mission = null;
            _screen = null;
            _cache = null;
            _photoPath = null;
            _stableLength = -1;
            _frames = 0;
            _confirmedRenderFrames = 0;
            _hiddenSinceTimestamp = 0;
            _failed = false;
            _watch = null;
        }

        internal void ApplyMapCameraToFinalView(MissionScreen screen)
        {
            if ((_stage == Stage.Arming || _stage == Stage.Shooting || _stage == Stage.Reading) &&
                ReferenceEquals(screen, _screen) && _mapCamera != null)
            {
                ApplyOrthographicCamera();
                screen.SceneView.SetCamera(_mapCamera);
            }
        }

        [HarmonyPatch(typeof(MissionScreen), "CheckForUpdateCamera")]
        private static class FinalViewCameraPatch
        {
            [HarmonyPostfix]
            private static void Postfix(MissionScreen __instance)
            {
                Instance.ApplyMapCameraToFinalView(__instance);
            }
        }
    }
}
