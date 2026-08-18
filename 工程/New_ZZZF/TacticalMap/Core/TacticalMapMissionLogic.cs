using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using New_ZZZF.TacticalMap.Config;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// TacticalMap battle MissionBehavior.
    /// The legacy HtmlUI lifecycle/state machine has been removed. This class now owns only
    /// TacticalMap controller initialization, mission lifetime, and backend ticking.
    /// The new HtmlUI will consume the controller through a clean integration layer.
    /// </summary>
    public sealed class TacticalMapMissionLogic : MissionLogic
    {
        private TacticalMapController _controller;
        private MissionScreen _missionScreen;
        private bool _initialized;
        private bool _ready;

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
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage(
                        $"[TMap] Mission 初始化异常: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (!_initialized && Mission != null && Mission.Scene != null)
                InitializeController();

            if (!_ready || _controller == null)
                return;

            if (_missionScreen == null)
                _missionScreen = ScreenManager.TopScreen as MissionScreen;

            if (_missionScreen == null)
                return;

            // Keep the backend active for the upcoming UI integration.
            // Until the new UI exists, the controller remains the sole owner of TacticalMap state.
            _controller.SetVisible(_missionScreen, true);
            _controller.Tick(Mission, _missionScreen, dt);
        }

        private void InitializeController()
        {
            if (_initialized) return;
            _initialized = true;

            if (!FeatureGate.Enabled || !MissionSceneGuard.IsTacticalMapSupported(Mission))
            {
                _ready = false;
                return;
            }

            _controller = new TacticalMapController(Mission);
            _ready = _controller.Initialize(Mission);
        }

        protected override void OnEndMission()
        {
            try
            {
                if (_controller != null && _missionScreen != null)
                    _controller.SetVisible(_missionScreen, false);
            }
            catch
            {
                // Mission cleanup must not throw.
            }

            try
            {
                if (CameraController.Instance != null)
                    CameraController.Instance.Destroy();
            }
            catch
            {
                // Mission cleanup must not throw.
            }

            CameraController.Instance = null;
            _missionScreen = null;
            _controller = null;
            _ready = false;
            _initialized = false;

            base.OnEndMission();
        }
    }
}
