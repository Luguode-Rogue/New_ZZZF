using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.UI;
using System;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 战场 MissionBehavior：管理 TacticalMap HTMLUI 的 N 键状态机与地图数据刷新。
    /// N 短按：操作地图/非操作切换。
    /// N 长按：小地图 -> 全屏 -> 隐藏 -> 小地图。
    /// </summary>
    public sealed class TacticalMapMissionLogic : MissionLogic
    {
        private TacticalMapController _controller;
        private MissionScreen _ms;
        private bool _ready;
        private bool _initialized;
        private bool _defaultStateApplied;
        private bool _nTracking;
        private float _nHeldTime;
        private bool _loggedFirstTick;

        public override void OnAfterMissionCreated()
        {
            if (_initialized) return;
            TacticalMapHtmlUiDebug.Log("MISSION", "OnAfterMissionCreated enter");
            try
            {
                base.OnAfterMissionCreated();
                if (!FeatureGate.Enabled)
                {
                    TacticalMapHtmlUiDebug.Log("MISSION", "FeatureGate disabled");
                    _initialized = true;
                    return;
                }
                if (!MissionSceneGuard.IsTacticalMapSupported(Mission))
                {
                    TacticalMapHtmlUiDebug.Log("MISSION", "MissionSceneGuard rejected mission");
                    _initialized = true;
                    _ready = false;
                    return;
                }

                TacticalMapHtmlUiDebug.Log("MISSION", "creating TacticalMapController");
                _controller = new TacticalMapController(Mission);
                _ready = _controller.Initialize(Mission);
                TacticalMapHtmlUiDebug.Log("MISSION", "controller Initialize result=" + _ready);
                _initialized = true;
            }
            catch (Exception ex)
            {
                TacticalMapHtmlUiDebug.Log("MISSION_ERROR", ex.ToString());
                string fr = ex.StackTrace != null ? ex.StackTrace.Split('\n')[0].Trim() : "";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMap] OnAfterMissionCreated 异常: {ex.GetType().Name}: {ex.Message} @ {fr}"));
                _initialized = true;
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (!_loggedFirstTick)
            {
                _loggedFirstTick = true;
                TacticalMapHtmlUiDebug.Log("TICK", "first OnMissionTick; initialized=" + _initialized + " ready=" + _ready + " ui=" + (TacticalMapBootstrap.HtmlUi == null ? "null" : "exists"));
            }

            if (!_initialized && Mission != null && Mission.Scene != null)
            {
                try
                {
                    TacticalMapHtmlUiDebug.Log("MISSION", "lazy initialization path entered");
                    if (!MissionSceneGuard.IsTacticalMapSupported(Mission))
                    {
                        TacticalMapHtmlUiDebug.Log("MISSION", "lazy MissionSceneGuard rejected mission");
                        _initialized = true;
                        _ready = false;
                        return;
                    }

                    _controller = new TacticalMapController(Mission);
                    _ready = _controller.Initialize(Mission);
                    TacticalMapHtmlUiDebug.Log("MISSION", "lazy controller Initialize result=" + _ready);
                    _initialized = true;
                }
                catch (Exception ex)
                {
                    TacticalMapHtmlUiDebug.Log("MISSION_ERROR", "lazy init: " + ex);
                    string fr = ex.StackTrace != null ? ex.StackTrace.Split('\n')[0].Trim() : "";
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[TMap] 懒初始化异常: {ex.GetType().Name}: {ex.Message} @ {fr}"));
                    _initialized = true;
                }
            }

            if (!_ready || _controller == null) return;

            if (_ms == null)
            {
                _ms = ScreenManager.TopScreen as MissionScreen;
                if (_ms != null) TacticalMapHtmlUiDebug.Log("TICK", "MissionScreen resolved");
            }
            if (_ms == null) return;

            var ui = TacticalMapBootstrap.HtmlUi;
            if (!_defaultStateApplied)
            {
                TacticalMapHtmlUiDebug.Log("STATE", "applying default state; ui=" + (ui == null ? "null" : "exists"));
                _controller.SetVisible(_ms, true);
                ui?.SetVisible(true);
                ui?.ResetForMission();
                _defaultStateApplied = true;
                TacticalMapHtmlUiDebug.Log("STATE", "default state applied; controllerVisible=" + _controller.IsVisible + ", uiVisible=" + (ui != null && ui.IsVisible));
            }

            if (ui != null && ui.IsInteractive && Input.IsKeyPressed(InputKey.Escape))
            {
                TacticalMapHtmlUiDebug.Log("INPUT", "ESC -> ToggleInteraction");
                ui.ToggleInteraction();
                return;
            }

            HandleNKey(dt, ui);
            _controller.Tick(Mission, _ms, dt);
        }

        private void HandleNKey(float dt, TacticalMapHtmlUi ui)
        {
            var s = TacticalSettings.Instance;
            if (!_nTracking && Input.IsKeyPressed(s.ToggleKey))
            {
                _nTracking = true;
                _nHeldTime = 0f;
                TacticalMapHtmlUiDebug.Log("INPUT", "N down");
            }

            if (_nTracking && Input.IsKeyDown(s.ToggleKey))
                _nHeldTime += Math.Max(0f, dt);

            if (!_nTracking || !Input.IsKeyReleased(s.ToggleKey)) return;

            bool longPress = _nHeldTime >= s.ToggleLongPressThreshold;
            TacticalMapHtmlUiDebug.Log("INPUT", "N up held=" + _nHeldTime.ToString("F3") + " long=" + longPress + " ui=" + (ui == null ? "null" : ui.State.ToString()));
            _nTracking = false;
            _nHeldTime = 0f;

            if (ui == null) return;

            if (longPress)
                HandleLongPress(ui);
            else if (ui.IsVisible)
                ui.ToggleInteraction();
            else
                ui.SetUiState(TacticalMapHtmlUi.UiState.CompactPassive);
        }

        private void HandleLongPress(TacticalMapHtmlUi ui)
        {
            TacticalMapHtmlUiDebug.Log("STATE", "HandleLongPress before=" + ui.State);
            if (!ui.IsVisible)
            {
                ui.SetUiState(TacticalMapHtmlUi.UiState.CompactPassive);
                return;
            }

            if (ui.IsFullscreen)
                ui.SetUiState(TacticalMapHtmlUi.UiState.Hidden);
            else
                ui.ToggleFullscreenPreservingInteraction();

            TacticalMapHtmlUiDebug.Log("STATE", "HandleLongPress after=" + ui.State);
        }

        protected override void OnEndMission()
        {
            TacticalMapHtmlUiDebug.Log("MISSION", "OnEndMission");
            if (_controller != null && _ms != null)
                _controller.SetVisible(_ms, false);

            var ui = TacticalMapBootstrap.HtmlUi;
            if (ui != null)
                ui.SetUiState(TacticalMapHtmlUi.UiState.Hidden);

            if (CameraController.Instance != null)
            {
                try { CameraController.Instance.Destroy(); }
                catch (Exception) { }
                CameraController.Instance = null;
            }

            _defaultStateApplied = false;
            _nTracking = false;
            _nHeldTime = 0f;
            _ms = null;
            _controller = null;
            _ready = false;
            _initialized = false;
            _loggedFirstTick = false;
            base.OnEndMission();
        }
    }
}
