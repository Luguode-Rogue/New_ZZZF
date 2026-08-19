using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.UI;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// TacticalMap 战场 MissionBehavior。
    /// Controller 负责游戏逻辑，TacticalMapHtmlUi 负责 HTMLUI 生命周期和表现。
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

            _controller.SetVisible(_missionScreen, true);
            _controller.Tick(Mission, _missionScreen, dt);
            TacticalMapHtmlUi.Instance.AttachController(_controller);
            TacticalMapHtmlUi.Instance.Tick(dt);
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
            if (_ready)
                TacticalMapHtmlUi.Instance.AttachController(_controller);
        }

        protected override void OnEndMission()
        {
            try
            {
                TacticalMapHtmlUi.Instance.DetachController();
            }
            catch
            {
                // UI cleanup must not throw during mission shutdown.
            }

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
