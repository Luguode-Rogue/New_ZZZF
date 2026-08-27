using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.UI;
using New_ZZZF.TacticalMap.Diagnostics;
using BannerlordHtmlUI;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// TacticalMap 战场 MissionBehavior。
    /// Controller 负责游戏逻辑，TacticalMapHtmlUi 负责 HTMLUI 生命周期和表现。
    /// </summary>
    public sealed class TacticalMapMissionLogic : MissionLogic
    {
        private const int CompactOverlayWidth = 400;
        private const int CompactOverlayHeight = 400;
        private const int CompactOverlayMargin = 16;

        private TacticalMapController _controller;
        private MissionScreen _missionScreen;
        private bool _initialized;
        private bool _ready;
        private float _heartbeatAccum;
        private TacticalMapUiMode _lastLayoutMode = (TacticalMapUiMode)(-1);

        public override void OnAfterMissionCreated()
        {
            if (_initialized) return;

            TacticalMapLog.Section("MISSION CREATED");
            TacticalMapLog.Info("OnAfterMissionCreated entered. Mission=" + (Mission == null ? "null" : Mission.GetType().FullName));

            try
            {
                base.OnAfterMissionCreated();
                InitializeController();
            }
            catch (Exception ex)
            {
                _initialized = true;
                _ready = false;
                TacticalMapLog.Error("Mission initialization threw.", ex);
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage(
                        $"[TMap] Mission 初始化异常: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (Mission == null)
            {
                if (_controller != null || TacticalMapHtmlUi.Instance.IsVisible)
                {
                    TacticalMapLog.Info("Mission reference became null during tick; forcing TacticalMap UI detach.");
                    TacticalMapHtmlUi.Instance.DetachController();
                    _controller = null;
                    _ready = false;
                    HtmlUiOverlayLayout.UseFullWindow();
                    _lastLayoutMode = (TacticalMapUiMode)(-1);
                }
                return;
            }

            if (!_initialized && Mission.Scene != null)
                InitializeController();

            if (!_ready || _controller == null)
                return;

            if (_missionScreen == null)
            {
                _missionScreen = ScreenManager.TopScreen as MissionScreen;
                if (_missionScreen != null)
                    TacticalMapLog.Info("MissionScreen resolved: " + _missionScreen.GetType().FullName);
            }

            if (_missionScreen == null)
                return;

            _controller.SetVisible(_missionScreen, true);
            _controller.Tick(Mission, _missionScreen, dt);
            TacticalMapHtmlUi.Instance.Tick(dt);
            ApplyOverlayLayoutIfChanged();

            // Input ownership is changed only on TacticalMap mode transitions via ApplyInputMode().
            // Do not re-submit CaptureInput() every MissionTick: HtmlUiService marshals it to the WebView2 UI thread,
            // and repeating it every frame can starve browser input/timers and cause long-press latency.
            _heartbeatAccum += Math.Max(0f, dt);
            if (_heartbeatAccum >= 5f)
            {
                _heartbeatAccum = 0f;
                TacticalMapLog.Info("Mission heartbeat. UIVisible=" + TacticalMapHtmlUi.Instance.IsVisible +
                                    " Mode=" + TacticalMapHtmlUi.Instance.Mode +
                                    " Baked=" + _controller.Cache.IsBaked);
            }
        }

        private void ApplyOverlayLayoutIfChanged()
        {
            var mode = TacticalMapHtmlUi.Instance.Mode;
            if (mode == _lastLayoutMode) return;

            _lastLayoutMode = mode;
            if (mode != TacticalMapUiMode.FullInteractive && mode != TacticalMapUiMode.FullPassive)
            {
                HtmlUiOverlayLayout.UseTopRight(CompactOverlayWidth, CompactOverlayHeight, CompactOverlayMargin);
                TacticalMapLog.Info("Overlay layout=TopRight size=" + CompactOverlayWidth + "x" + CompactOverlayHeight + " margin=" + CompactOverlayMargin + ". Mode=" + mode);
            }
            else
            {
                HtmlUiOverlayLayout.UseFullWindow();
                TacticalMapLog.Info("Overlay layout=FullWindow. Mode=" + mode);
            }
        }

        private void InitializeController()
        {
            if (_initialized) return;
            _initialized = true;
            TacticalMapLog.Section("CONTROLLER INITIALIZE");

            if (Mission == null)
            {
                _ready = false;
                TacticalMapLog.Warn("Mission is null; controller initialization aborted.");
                return;
            }

            bool featureEnabled = FeatureGate.Enabled;
            bool sceneSupported = MissionSceneGuard.IsTacticalMapSupported(Mission);
            TacticalMapLog.Info("FeatureGate.Enabled=" + featureEnabled + ", SceneSupported=" + sceneSupported);

            if (!featureEnabled || !sceneSupported)
            {
                _ready = false;
                TacticalMapLog.Warn("TacticalMap disabled for this mission.");
                return;
            }

            try
            {
                _controller = new TacticalMapController(Mission);
                TacticalMapLog.Info("TacticalMapController constructed.");

                _ready = _controller.Initialize(Mission);
                TacticalMapLog.Info("TacticalMapController.Initialize result=" + _ready +
                                    ", Baked=" + _controller.Cache.IsBaked +
                                    ", Error=" + (_controller.Cache.LastError ?? "<none>"));

                if (_ready)
                {
                    TacticalMapHtmlUi.Instance.AttachController(_controller);
                    ApplyOverlayLayoutIfChanged();
                    TacticalMapLog.Info("TacticalMapHtmlUi.AttachController completed. Mode=" + TacticalMapHtmlUi.Instance.Mode);
                }
                else
                {
                    TacticalMapLog.Warn("Controller Initialize returned false; HTMLUI not attached.");
                }
            }
            catch (Exception ex)
            {
                _ready = false;
                TacticalMapLog.Error("Controller initialization failed.", ex);
            }
        }

        protected override void OnEndMission()
        {
            TacticalMapLog.Section("MISSION END");
            try
            {
                TacticalMapLog.Info("Detaching TacticalMap HTMLUI. PageVisible=" + TacticalMapHtmlUi.Instance.IsVisible);
                TacticalMapHtmlUi.Instance.DetachController();
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("TacticalMapHtmlUi.DetachController failed during mission end.", ex);
            }

            try
            {
                HtmlUiOverlayLayout.UseFullWindow();
            }
            catch (Exception ex)
            {
                TacticalMapLog.Info("Failed to restore full-window overlay layout during mission end: " + ex.GetBaseException().Message);
            }

            try
            {
                if (_controller != null && _missionScreen != null)
                {
                    _controller.SetVisible(_missionScreen, false);
                    TacticalMapLog.Info("Controller visibility disabled.");
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("Controller visibility cleanup failed.", ex);
            }

            try
            {
                if (CameraController.Instance != null)
                {
                    CameraController.Instance.Destroy();
                    TacticalMapLog.Info("CameraController destroyed.");
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("CameraController cleanup failed.", ex);
            }

            CameraController.Instance = null;
            _missionScreen = null;
            _controller = null;
            _ready = false;
            _initialized = false;
            _heartbeatAccum = 0f;
            _lastLayoutMode = (TacticalMapUiMode)(-1);

            base.OnEndMission();
            TacticalMapLog.Info("OnEndMission completed.");
        }
    }
}
