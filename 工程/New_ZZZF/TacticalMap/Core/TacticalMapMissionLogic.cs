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

        public override void OnAfterMissionCreated()
        {
            if (_initialized) return;
            try
            {
                base.OnAfterMissionCreated();
                if (!FeatureGate.Enabled)
                {
                    _initialized = true;
                    return;
                }
                if (!MissionSceneGuard.IsTacticalMapSupported(Mission))
                {
                    _initialized = true;
                    _ready = false;
                    return;
                }

                _controller = new TacticalMapController(Mission);
                _ready = _controller.Initialize(Mission);
                _initialized = true;
            }
            catch (Exception ex)
            {
                string fr = ex.StackTrace != null ? ex.StackTrace.Split('\n')[0].Trim() : "";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMap] OnAfterMissionCreated 异常: {ex.GetType().Name}: {ex.Message} @ {fr}"));
                _initialized = true;
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (!_initialized && Mission != null && Mission.Scene != null)
            {
                try
                {
                    if (!MissionSceneGuard.IsTacticalMapSupported(Mission))
                    {
                        _initialized = true;
                        _ready = false;
                        return;
                    }

                    _controller = new TacticalMapController(Mission);
                    _ready = _controller.Initialize(Mission);
                    _initialized = true;
                }
                catch (Exception ex)
                {
                    string fr = ex.StackTrace != null ? ex.StackTrace.Split('\n')[0].Trim() : "";
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[TMap] 懒初始化异常: {ex.GetType().Name}: {ex.Message} @ {fr}"));
                    _initialized = true;
                }
            }

            if (!_ready || _controller == null) return;

            if (_ms == null) _ms = ScreenManager.TopScreen as MissionScreen;
            if (_ms == null) return;

            var ui = TacticalMapBootstrap.HtmlUi;

            // Core controller 始终保持运行，以便地图隐藏时仍可快速恢复；HTML 页面显隐完全由 UiState 管理。
            if (!_defaultStateApplied)
            {
                _controller.SetVisible(_ms, true);
                ui?.SetVisible(true);
                ui?.ResetForMission();
                _defaultStateApplied = true;
            }

            // ESC 只退出 HTML 地图操作，不关闭地图；Captuerd 模式使用非激活窗口时，键盘仍由 Bannerlord 主窗口接收。
            if (ui != null && ui.IsInteractive && Input.IsKeyPressed(InputKey.Escape))
            {
                ui.ToggleInteraction();
                return;
            }

            HandleNKey(dt, ui);

            // 地图数据更新不依赖 HTML 页面当前是否可见，因此隐藏状态也保持 Core Tick。
            _controller.Tick(Mission, _ms, dt);
        }

        private void HandleNKey(float dt, TacticalMapHtmlUi ui)
        {
            var s = TacticalSettings.Instance;
            if (!_nTracking && Input.IsKeyPressed(s.ToggleKey))
            {
                _nTracking = true;
                _nHeldTime = 0f;
            }

            if (_nTracking && Input.IsKeyDown(s.ToggleKey))
                _nHeldTime += Math.Max(0f, dt);

            if (!_nTracking || !Input.IsKeyReleased(s.ToggleKey)) return;

            bool longPress = _nHeldTime >= s.ToggleLongPressThreshold;
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
            if (!ui.IsVisible)
            {
                ui.SetUiState(TacticalMapHtmlUi.UiState.CompactPassive);
                return;
            }

            if (ui.IsFullscreen)
            {
                ui.SetUiState(TacticalMapHtmlUi.UiState.Hidden);
            }
            else
            {
                ui.ToggleFullscreenPreservingInteraction();
            }
        }

        protected override void OnEndMission()
        {
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
            base.OnEndMission();
        }
    }
}
