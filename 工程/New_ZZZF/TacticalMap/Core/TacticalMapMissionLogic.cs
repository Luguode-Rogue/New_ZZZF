using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using New_ZZZF.TacticalMap.Config;
using System;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 战场 MissionBehavior：挂载小地图控制器、处理开关/点击/相机热键。
    /// </summary>
    public sealed class TacticalMapMissionLogic : MissionLogic
    {
        private TacticalMapController _controller;
        private MissionScreen _ms;
        private bool _ready;
        private bool _initialized;

        public override void OnAfterMissionCreated()
        {
            InformationManager.DisplayMessage(new InformationMessage("[TMap] OnAfterMissionCreated 进入"));
            if (_initialized) return;
            try
            {
                base.OnAfterMissionCreated();
                if (!FeatureGate.Enabled) { InformationManager.DisplayMessage(new InformationMessage("[TMap] 功能被关闭 (EnableMinimap=false)")); _initialized = true; return; }
                _controller = new TacticalMapController(Mission);
                _ready = _controller.Initialize(Mission);
                _initialized = true;
                var c = _controller.Cache;
                string err = string.IsNullOrEmpty(c.LastError) ? "" : ("err=" + c.LastError);
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] 初始化 _ready={_ready} baked={c.IsBaked} {c.Width}x{c.Height} {err}"));
            }
            catch (Exception ex)
            {
                string fr = ex.StackTrace != null ? ex.StackTrace.Split('\n')[0].Trim() : "";
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] OnAfterMissionCreated 异常: {ex.GetType().Name}: {ex.Message} @ {fr}"));
                _initialized = true;
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            // 懒初始化兜底：若 OnAfterMissionCreated 未被引擎调用，则在首个可用 tick 初始化
            if (!_initialized && Mission != null && Mission.Scene != null)
            {
                try
                {
                    _controller = new TacticalMapController(Mission);
                    _ready = _controller.Initialize(Mission);
                    _initialized = true;
                    var c = _controller.Cache;
                    string err = string.IsNullOrEmpty(c.LastError) ? "" : ("err=" + c.LastError);
                    InformationManager.DisplayMessage(new InformationMessage($"[TMap] 懒初始化 _ready={_ready} baked={c.IsBaked} {c.Width}x{c.Height} {err}"));
                }
                catch (Exception ex)
                {
                    string fr = ex.StackTrace != null ? ex.StackTrace.Split('\n')[0].Trim() : "";
                    InformationManager.DisplayMessage(new InformationMessage($"[TMap] 懒初始化 异常: {ex.GetType().Name}: {ex.Message} @ {fr}"));
                    _initialized = true;
                }
            }

            if (!_ready || _controller == null) return;

            if (_ms == null) _ms = ScreenManager.TopScreen as MissionScreen;
            if (_ms == null) return;

            var s = TacticalSettings.Instance;

            if (Input.IsKeyPressed(s.ToggleKey))
            {
                _controller.SetVisible(_ms, !_controller.IsVisible);
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] 切换显示 -> Visible={_controller.IsVisible} ready={_ready}"));
            }

            if (_controller.IsVisible)
            {
                if (Input.IsKeyPressed(s.CameraFollowKey))
                    _controller.ToggleCameraFollow();

                Vec2 mouse = Input.MousePositionPixel;
                bool left = Input.IsKeyPressed(InputKey.LeftMouseButton);
                bool right = Input.IsKeyPressed(InputKey.RightMouseButton);
                if (left || right)
                {
                    bool shift = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
                    _controller.HandleClick(mouse, shift, right);
                }

                _controller.Tick(Mission, _ms, dt);
            }
        }

        protected override void OnEndMission()
        {
            if (_controller != null && _ms != null)
                _controller.SetVisible(_ms, false);
            base.OnEndMission();
        }
    }
}
