using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF.SpellForge
{
    /// <summary>
    /// 法术锻造界面（独立 Screen）。
    /// 从“新技能界面”(CustomSkillScreen) 的按钮打开，叠加在其之上。
    /// 关闭时通知新技能界面同步状态。
    /// </summary>
    public class SpellForgeScreen : ScreenBase
    {
        private readonly CustomSkillScreenVM _parentVM;
        private readonly Action _onCommit;
        private SpellForgeVM _dataSource;
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movie;

        public SpellForgeScreen(CustomSkillScreenVM parentVM, Action onCommit)
        {
            _parentVM = parentVM;
            _onCommit = onCommit;
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            _dataSource = new SpellForgeVM(_parentVM, () => CloseScreen());
            _layer = new GauntletLayer("SpellForgeScreen", 101)
            {
                IsFocusLayer = true
            };
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            AddLayer(_layer);
            ScreenManager.TrySetFocus(_layer);
            _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
            _movie = _layer.LoadMovie("SpellForgeScreen", _dataSource);
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            if (_layer != null && _layer.Input.IsHotKeyReleased("Exit"))
            {
                CloseScreen();
            }
        }

        protected override void OnFinalize()
        {
            // 参考 API迁移记忆库.md：OnFinalize 中直接置空即可，无需额外 ReleaseMovie
            // （基类释放流程会处理 movie，手动 ReleaseMovie 会造成重复释放 → NullReferenceException）
            base.OnFinalize();
            _dataSource = null;
            _movie = null;
            if (_layer != null)
            {
                try { RemoveLayer(_layer); } catch { }
                _layer = null;
            }
            _parentVM?.NotifySpellForgeClosed();
            _onCommit?.Invoke();
        }

        private void CloseScreen()
        {
            ScreenManager.PopScreen();
        }
    }
}
