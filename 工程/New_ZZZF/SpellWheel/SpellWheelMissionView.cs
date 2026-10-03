using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.MountAndBlade.ViewModelCollection;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF
{
    /// <summary>战场屏幕就绪后正式注册视图，保证 MissionScreen 已绑定。</summary>
    public sealed class SpellWheelBootstrap : MissionLogic
    {
        private bool _attached;
        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_attached) return;
            MissionScreen screen = ScreenManager.TopScreen as MissionScreen;
            if (screen == null || screen.Mission != Mission || screen.SceneLayer == null) return;
            screen.AddMissionView(new SpellWheelMissionView());
            _attached = true;
        }
    }

    /// <summary>
    /// 按住 Tab 上方的 Tilde 键打开，松开确认。界面直接使用原生控件创建，
    /// 不依赖新增 XML 的部署位置，不改变资源加载或自动部署流程。
    /// </summary>
    public sealed class SpellWheelMissionView : MissionView
    {
        // 与原生指挥菜单一致：四分之一游戏速度。使用独立请求 ID，避免覆盖其他减速。
        private const float WheelTimeSpeed = 0.25f;
        private const int WheelTimeSpeedRequestId = 0x5A5A4657;
        private bool _timeSpeedRequested;
        private static SpellWheelMissionView _current;
        private GauntletLayer _layer;
        private UIContext _context;
        private Widget _root;
        private CircleActionSelectorWidget _selector;
        private readonly ButtonWidget[] _buttons = new ButtonWidget[4];
        private readonly TextWidget[] _labels = new TextWidget[4];
        private readonly SkillBase[] _skills = new SkillBase[4];
        private AgentSkillComponent _component;
        private bool _open;
        private bool _wasHeld;
        private bool _cancelledUntilRelease;

        public static bool IsOpen => _current != null && _current._open;
        public SpellWheelMissionView() { ViewOrderPriority = 250; }

        // 镜头补丁与技能输入共用同一可用性判断，界面未就绪时不抢占原生按键。
        private bool CanOpen()
        {
            MissionScreen screen = MissionScreen;
            Agent agent = Mission?.MainAgent;
            return _layer != null && screen != null && screen.SceneLayer != null &&
                ScreenManager.TopScreen == screen && !MBCommon.IsPaused &&
                !screen.IsPhotoModeEnabled && !screen.MouseVisible &&
                !((IMissionScreen)screen).GetDisplayDialog() && !Mission.IsOrderMenuOpen &&
                (Mission.Mode == MissionMode.Battle || Mission.Mode == MissionMode.Stealth) &&
                agent != null && agent.IsActive() && agent.IsPlayerControlled &&
                Script.GetActiveComponents(agent) != null &&
                (_open || !screen.IsRadialMenuActive);
        }

        public static bool BlocksSkillInput(Agent agent)
        {
            SpellWheelMissionView view = _current;
            if (view == null || view.Mission?.MainAgent != agent) return false;
            return view._open || (view.CanOpen() &&
                (TaleWorlds.InputSystem.Input.IsKeyDown(InputKey.Tilde) ||
                 TaleWorlds.InputSystem.Input.IsKeyReleased(InputKey.Tilde)));
        }

        internal static bool OwnsCharacterCamera(MissionScreen screen)
        {
            SpellWheelMissionView view = _current;
            return view != null && view.MissionScreen == screen &&
                (view._open || (view.CanOpen() && TaleWorlds.InputSystem.Input.IsKeyDown(InputKey.Tilde)));
        }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            if (_layer != null) return;
            _layer = new GauntletLayer("ZZZFSpellWheel", ViewOrderPriority, false);
            _layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.Invalid);
            MissionScreen.AddLayer(_layer);
            _current = this;
        }

        public override void OnMissionScreenTick(float dt)
        {
            if (_layer == null || MissionScreen == null) return;
            bool held = TaleWorlds.InputSystem.Input.IsKeyDown(InputKey.Tilde);
            AgentSkillComponent component = CanOpen() ? Script.GetActiveComponents(Mission.MainAgent) : null;
            if (component == null || (_open && (component != _component || _context != _layer.UIContext)))
            {
                Close(false);
                _cancelledUntilRelease = held;
            }
            else if (_open && TaleWorlds.InputSystem.Input.IsKeyPressed(InputKey.Escape))
            {
                Close(false);
                _cancelledUntilRelease = true;
            }
            else if (held && !_wasHeld && !_cancelledUntilRelease && !_open)
            {
                Open(component);
            }
            else if (!held && _wasHeld)
            {
                Close(true);
            }
            if (!held) _cancelledUntilRelease = false;
            _wasHeld = held;
        }

        private void Open(AgentSkillComponent component)
        {
            // 每次新建选择器，清空原生控件内部累计的鼠标方向，避免沿用上一次选项。
            _component = component;
            _context = _layer.UIContext;
            BuildWidgets();
            for (int i = 0; i < _skills.Length; i++)
                _skills[i] = component.SpellSlots[i];
            for (int i = 0; i < _skills.Length; i++)
                _labels[i].Text = SpellName(_skills[i]);
            _selector.TrySetSelectedIndex(component.SelectedSpellSlot);
            MissionScreen.RegisterRadialMenuObject(this);
            _open = true;
            if (!GameNetwork.IsMultiplayer && !_timeSpeedRequested)
            {
                Mission.AddTimeSpeedRequest(new Mission.TimeSpeedRequest(WheelTimeSpeed, WheelTimeSpeedRequestId));
                _timeSpeedRequested = true;
            }
        }

        private void BuildWidgets()
        {
            _root = new Widget(_context)
            {
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                DoNotAcceptEvents = true
            };
            _context.Root.AddChild(_root);
            Widget center = new Widget(_context);
            SetCenteredSize(center, 200, 200);
            _root.AddChild(center);

            Widget background = new Widget(_context)
            {
                Sprite = _context.SpriteData.GetSprite("General\\RadialMenu\\radial_menu_bg"),
                Color = new Color(0f, 0f, 0f, 0.6f),
                DoNotAcceptEvents = true
            };
            SetCenteredSize(background, 459, 459);
            center.AddChild(background);

            _selector = new CircleActionSelectorWidget(_context)
            {
                DistanceFromCenterModifier = 160,
                IsCircularInputEnabled = true,
                DoNotAcceptEvents = true
            };
            SetCenteredSize(_selector, 200, 200);
            center.AddChild(_selector);
            for (int i = 0; i < _buttons.Length; i++)
            {
                ButtonWidget button = new ButtonWidget(_context)
                {
                    Brush = _context.GetBrush("Mission.Radial.Item.Glow"),
                    DoNotAcceptEvents = true,
                    UpdateChildrenStates = true
                };
                SetCenteredSize(button, 117, 115);
                _selector.AddChild(button);
                TextWidget text = new TextWidget(_context)
                {
                    Brush = _context.GetBrush("Mission.DropCircle.ItemText"),
                    DoNotAcceptEvents = true
                };
                // 圆圈内预留技能图片，名称放在圆圈下方。
                SetCenteredSize(text, 180, 60);
                text.PositionYOffset = 90;
                button.AddChild(text);
                _buttons[i] = button;
                _labels[i] = text;
            }
        }

        private static void SetCenteredSize(Widget widget, float width, float height)
        {
            widget.WidthSizePolicy = SizePolicy.Fixed;
            widget.HeightSizePolicy = SizePolicy.Fixed;
            widget.SuggestedWidth = width;
            widget.SuggestedHeight = height;
            widget.HorizontalAlignment = HorizontalAlignment.Center;
            widget.VerticalAlignment = VerticalAlignment.Center;
        }

        private static bool IsEmpty(SkillBase skill) => skill == null || skill.SkillID == "NullSkill";
        private static string SpellName(SkillBase skill) =>
            IsEmpty(skill) ? "空槽" : (skill.Text?.ToString() ?? skill.SkillID);
        private int SelectedSlot()
        {
            for (int i = 0; i < _buttons.Length; i++)
                if (_buttons[i] != null && _buttons[i].IsSelected) return i;
            return -1;
        }

        private void Close(bool confirm)
        {
            // 所有关闭路径都先解除本轮盘的减速，包括取消、失焦、暂停及任务结束。
            // 原生 RemoveTimeSpeedRequest 在请求不存在时会抛异常，因此先查询。
            if (_timeSpeedRequested)
            {
                if (Mission != null && Mission.GetRequestedTimeSpeed(WheelTimeSpeedRequestId, out _))
                    Mission.RemoveTimeSpeedRequest(WheelTimeSpeedRequestId);
                _timeSpeedRequested = false;
            }
            if (confirm && _open && _component != null && Mission?.MainAgent != null &&
                Script.GetActiveComponents(Mission.MainAgent) == _component)
            {
                int slot = SelectedSlot();
                if (slot >= 0 && !IsEmpty(_skills[slot]) && _component.SpellSlots[slot] == _skills[slot])
                    _component.SelectSpellFromWheel(slot);
            }
            if (_open && MissionScreen != null) MissionScreen.UnregisterRadialMenuObject(this);
            _open = false;
            if (_root != null) _root.ParentWidget = null;
            _root = null;
            _selector = null;
            _component = null;
            _context = null;
            for (int i = 0; i < _buttons.Length; i++) { _buttons[i] = null; _labels[i] = null; _skills[i] = null; }
        }

        public override bool OnEscape()
        {
            if (!_open) return false;
            Close(false);
            _cancelledUntilRelease = true;
            return true;
        }
        public override void OnFocusChangeOnGameWindow(bool focusGained)
        {
            if (!focusGained) { Close(false); _cancelledUntilRelease = true; }
        }
        public override void OnMissionScreenDeactivate()
        {
            Close(false);
            _cancelledUntilRelease = true;
            base.OnMissionScreenDeactivate();
        }
        public override void OnPhotoModeActivated()
        {
            Close(false);
            _cancelledUntilRelease = true;
            base.OnPhotoModeActivated();
        }
        public override void OnMissionScreenFinalize()
        {
            Close(false);
            if (_layer != null && MissionScreen != null) MissionScreen.RemoveLayer(_layer);
            _layer = null;
            if (_current == this) _current = null;
            base.OnMissionScreenFinalize();
        }
    }

    [HarmonyPatch(typeof(MissionScreen), nameof(MissionScreen.IsViewingCharacter))]
    internal static class SpellWheelCharacterCameraPatch
    {
        private static bool Prefix(MissionScreen __instance, ref bool __result)
        {
            if (!SpellWheelMissionView.OwnsCharacterCamera(__instance)) return true;
            __result = false;
            return false;
        }
    }
}
