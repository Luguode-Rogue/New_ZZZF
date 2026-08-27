using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using New_ZZZF.TacticalMap.UI;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Core
{
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
                TacticalMapHtmlUi.Instance.DetachController();
                _controller = null;
                _ready = false;
                HtmlUiOverlayLayout.UseFullWindow();
                _lastLayoutMode = (TacticalMapUiMode)(-1);
                return;
            }

            if (!_initialized && Mission.Scene != null)
                InitializeController();
            if (!_ready || _controller == null) return;

            if (_missionScreen == null)
                _missionScreen = ScreenManager.TopScreen as MissionScreen;
            if (_missionScreen == null) return;

            _controller.SetVisible(_missionScreen, true);
            _controller.Tick(Mission, _missionScreen, dt);
            TacticalMapHtmlUi.Instance.Tick(dt);
            ApplyOverlayLayoutIfChanged();

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
            TacticalMapUiMode mode = TacticalMapHtmlUi.Instance.Mode;
            if (mode == _lastLayoutMode) return;
            _lastLayoutMode = mode;

            if (mode == TacticalMapUiMode.FullInteractive)
                HtmlUiOverlayLayout.UseFullWindow();
            else
                HtmlUiOverlayLayout.UseTopRight(CompactOverlayWidth, CompactOverlayHeight, CompactOverlayMargin);
        }

        private void InitializeController()
        {
            if (_initialized) return;
            _initialized = true;

            if (Mission == null) return;
            if (!FeatureGate.Enabled || !MissionSceneGuard.IsTacticalMapSupported(Mission))
            {
                _ready = false;
                return;
            }

            try
            {
                _controller = new TacticalMapController(Mission);
                _ready = _controller.Initialize(Mission);
                if (_ready)
                {
                    TacticalMapHtmlUi.Instance.AttachController(_controller);
                    ApplyOverlayLayoutIfChanged();
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
            try { TacticalMapHtmlUi.Instance.DetachController(); }
            catch (Exception ex) { TacticalMapLog.Error("TacticalMapHtmlUi.DetachController failed during mission end.", ex); }

            try { HtmlUiOverlayLayout.UseFullWindow(); }
            catch (Exception ex) { TacticalMapLog.Debug("Failed to restore full-window overlay layout: " + ex.GetBaseException().Message); }

            try
            {
                if (_controller != null && _missionScreen != null)
                    _controller.SetVisible(_missionScreen, false);
            }
            catch (Exception ex) { TacticalMapLog.Error("Controller visibility cleanup failed.", ex); }

            try { CameraController.Instance?.Destroy(); }
            catch (Exception ex) { TacticalMapLog.Error("CameraController cleanup failed.", ex); }

            CameraController.Instance = null;
            _missionScreen = null;
            _controller = null;
            _ready = false;
            _initialized = false;
            _heartbeatAccum = 0f;
            _lastLayoutMode = (TacticalMapUiMode)(-1);
            base.OnEndMission();
        }
    }
}
