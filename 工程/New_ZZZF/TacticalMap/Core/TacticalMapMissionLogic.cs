using System;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using New_ZZZF.TacticalMap.UI;
using New_ZZZF.TacticalMap.Diagnostics;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Terrain;

namespace New_ZZZF.TacticalMap.Core
{
    public sealed class TacticalMapMissionLogic : MissionLogic
    {
        private TacticalMapController _controller;
        private MissionScreen _missionScreen;
        private bool _initialized;
        private bool _ready;
        private float _heartbeatAccum;

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
                return;
            }

            if (!_initialized && Mission.Scene != null)
                InitializeController();
            if (!_ready || _controller == null) return;

            if (_missionScreen == null)
                _missionScreen = ScreenManager.TopScreen as MissionScreen;
            if (_missionScreen == null) return;

            HandleToggleInput();

            _controller.SetVisible(_missionScreen, true);
            _controller.Tick(Mission, _missionScreen, dt);
            TacticalMapHtmlUi.Instance.Tick(dt);

            _heartbeatAccum += Math.Max(0f, dt);
            if (_heartbeatAccum >= 5f)
            {
                _heartbeatAccum = 0f;
                TacticalMapLog.Info("Mission heartbeat. UIVisible=" + TacticalMapHtmlUi.Instance.IsVisible +
                                    " Mode=" + TacticalMapHtmlUi.Instance.Mode +
                                    " Baked=" + _controller.Cache.IsBaked);
            }
        }

        private void HandleToggleInput()
        {
            InputKey toggleKey = TacticalSettings.Instance.ToggleKey;

            try
            {
                if (DebugInput != null && DebugInput.IsKeyPressed(toggleKey))
                {
                    TacticalMapHtmlUi.Instance.ToggleInteractive();
                    TacticalMapLog.Info("Toggle key pressed: " + toggleKey +
                                        ", new mode=" + TacticalMapHtmlUi.Instance.Mode);
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("TacticalMap toggle input handling failed.", ex);
            }
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
                    // The terrain bake only samples the heightmap/material layers. Scene geometry
                    // such as houses and fences lives in GameEntity physics, so add its XY footprint
                    // before the HTML static snapshot is published.
                    SceneObstacleMap.Rebuild(_controller.Cache, Mission.Scene);
                    TacticalMapHtmlUi.Instance.AttachController(_controller);
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
            base.OnEndMission();
        }
    }
}
