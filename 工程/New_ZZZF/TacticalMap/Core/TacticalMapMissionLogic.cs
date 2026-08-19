using System;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using New_ZZZF.TacticalMap.Config;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// TacticalMap 战场 MissionBehavior。
    /// 只负责控制器生命周期、后端数据刷新和 N 键显示切换；HTMLUI 自身由 Consumer 管理。
    /// </summary>
    public sealed class TacticalMapMissionLogic : MissionLogic
    {
        private TacticalMapController _controller;
        private MissionScreen _missionScreen;
        private bool _initialized;
        private bool _ready;
        private bool _uiAttached;

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

            var ui = TacticalMapBootstrap.HtmlUi;
            if (!_uiAttached && ui != null)
            {
                ui.AttachController(_controller);
                ui.ResetForMission();
                _uiAttached = true;
            }

            if (Input.IsKeyPressed(InputKey.N) && ui != null)
            {
                ui.SetVisible(!ui.IsVisible);
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage(
                        ui.IsVisible ? "[TMap] HTMLUI 已显示" : "[TMap] HTMLUI 已隐藏"));
            }

            _controller.SetVisible(_missionScreen, ui != null && ui.IsVisible);
            _controller.Tick(Mission, _missionScreen, dt);
            ui?.Tick(dt);
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
                var ui = TacticalMapBootstrap.HtmlUi;
                if (ui != null)
                {
                    ui.SetVisible(false);
                    ui.AttachController(null);
                }
            }
            catch
            {
                // Mission cleanup must not throw.
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
            _uiAttached = false;

            base.OnEndMission();
        }
    }
}
