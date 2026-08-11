using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF.ActionExplorer
{
    /// <summary>
    /// M1：正式 4×5 ActionExplorer Screen。
    /// 只加载 Gauntlet UI + VM，不创建 Scene / Agent / 3D。
    /// 负责：Screen + GauntletLayer + 输入焦点 + 生命周期。
    /// </summary>
    public class ActionExplorerScreen : ScreenBase
    {
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movie;
        private ActionExplorerVM _vm;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            M0_Probe.M0Log.Lifecycle("M1", "SCREEN_CREATE");

            _vm = new ActionExplorerVM();
            _vm.CloseRequested += OnCloseRequested;

            _layer = new GauntletLayer("ActionExplorer", 100);

            AddLayer(_layer);

            _movie = _layer.LoadMovie("ActionExplorer", _vm);
            M0_Probe.M0Log.Lifecycle("M1", "MOVIE_LOAD");

            // 初始就把焦点设到该层，点击命中测试才会派发到控件。
            _layer.IsFocusLayer = true;
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            ScreenManager.TrySetFocus(_layer);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            if (_layer != null)
            {
                _layer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_layer);
            }
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();
            if (_layer != null)
            {
                // 让出焦点，避免阻塞下层界面输入。
                ScreenManager.TryLoseFocus(_layer);
            }
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);

            if (Input.IsKeyPressed(InputKey.Escape))
            {
                M0_Probe.M0Log.Lifecycle("M1", "ESC_CLOSE");
                CloseScreen();
            }
        }

        private void OnCloseRequested()
        {
            M0_Probe.M0Log.Lifecycle("M1", "CLOSE_REQUESTED");
            CloseScreen();
        }

        public void CloseScreen()
        {
            ScreenManager.PopScreen();
        }

        protected override void OnFinalize()
        {
            M0_Probe.M0Log.Lifecycle("M1", "DISPOSE_BEGIN");

            if (_layer != null && _movie != null)
            {
                try { _layer.ReleaseMovie(_movie); }
                catch (System.Exception ex)
                {
                    M0_Probe.M0Log.Warn("ReleaseMovie failed: " + ex);
                }
            }

            if (_layer != null)
            {
                try { ScreenManager.TryLoseFocus(_layer); }
                catch (System.Exception ex)
                {
                    M0_Probe.M0Log.Warn("TryLoseFocus failed: " + ex);
                }
                try { RemoveLayer(_layer); }
                catch (System.Exception ex)
                {
                    M0_Probe.M0Log.Warn("RemoveLayer failed: " + ex);
                }
            }

            if (_vm != null)
            {
                _vm.CloseRequested -= OnCloseRequested;
                _vm.OnFinalize();
            }

            _movie = null;
            _layer = null;
            _vm = null;

            ActionExplorerLauncher.NotifyClosed();

            M0_Probe.M0Log.Lifecycle("M1", "DISPOSE_END");

            base.OnFinalize();
        }
    }
}
