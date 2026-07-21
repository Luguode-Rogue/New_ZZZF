using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF
{
    // =========================================================================
    // 任务四：屏幕与图层管理 (Screen)  v2
    //
    // v2 变更：
    //   - 移除弹窗 Movie 管理（_popupMovie / _hasPopup）
    //   - 新增 F12 切换 DebugMode（解锁兵种模板+领主NPC）
    //   - 新增 Tab 循环 TargetType（队伍/兵种/领主）
    //   - 新增 ↑↓←→ 目录网格导航
    //   - 新增 Enter 选择/Esc 退出目录视图
    //   - ESC 关闭逻辑简化（无弹窗层级）
    // =========================================================================

    public class CustomSkillScreen : ScreenBase
    {
        private CustomSkillScreenVM _dataSource;
        private GauntletLayer _gauntletLayer;
        private GauntletMovieIdentifier _movie;

        // v2: 移除 _popupMovie / _hasPopup（弹窗体系已废弃）

        private float _lastKeyRepeatTime = 0f;
        private const float KeyRepeatInterval = 0.15f;

        // =========================================================================
        // 构造 & 生命周期
        // =========================================================================

        public CustomSkillScreen()
        {
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            _dataSource = new CustomSkillScreenVM();
            _dataSource.SetCloseAction(CloseScreen);

            _gauntletLayer = new GauntletLayer("CustomSkillScreen", 100)
            {
                IsFocusLayer = true
            };
            _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);

            AddLayer(_gauntletLayer);
            ScreenManager.TrySetFocus(_gauntletLayer);

            _gauntletLayer.Input.RegisterHotKeyCategory(
                HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));

            _movie = _gauntletLayer.LoadMovie("CustomSkillScreen", _dataSource);

            // 暂停大地图时间推进
            Game.Current.GameStateManager.RegisterActiveStateDisableRequest(this);
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);

            if (_gauntletLayer == null || _dataSource == null) return;

            // v2: 不再检查弹窗状态（弹窗体系已废弃）

            bool canRepeat = (dt > 0f) && ((_lastKeyRepeatTime += dt) >= KeyRepeatInterval);

            if (_dataSource.IsInCatalogView)
            {
                // ====== v2: 目录视图键盘导航 ======

                // 1~8 切换槽位（目录视图中也可换槽）
                if (Input.IsKeyReleased(InputKey.D1)) { _dataSource.SelectSlotByIndex(0); return; }
                if (Input.IsKeyReleased(InputKey.D2)) { _dataSource.SelectSlotByIndex(1); return; }
                if (Input.IsKeyReleased(InputKey.D3)) { _dataSource.SelectSlotByIndex(2); return; }
                if (Input.IsKeyReleased(InputKey.D4)) { _dataSource.SelectSlotByIndex(3); return; }
                if (Input.IsKeyReleased(InputKey.D5)) { _dataSource.SelectSlotByIndex(4); return; }
                if (Input.IsKeyReleased(InputKey.D6)) { _dataSource.SelectSlotByIndex(5); return; }
                if (Input.IsKeyReleased(InputKey.D7)) { _dataSource.SelectSlotByIndex(6); return; }
                if (Input.IsKeyReleased(InputKey.D8)) { _dataSource.SelectSlotByIndex(7); return; }

                if (canRepeat)
                {
                    if (Input.IsKeyDown(InputKey.Down))
                    {
                        _dataSource.SelectNextCatalogItem();
                        _lastKeyRepeatTime = 0f;
                        return;
                    }
                    if (Input.IsKeyDown(InputKey.Up))
                    {
                        _dataSource.SelectPrevCatalogItem();
                        _lastKeyRepeatTime = 0f;
                        return;
                    }
                    if (Input.IsKeyDown(InputKey.Left))
                    {
                        _dataSource.SelectPrevCatalogRow();
                        _lastKeyRepeatTime = 0f;
                        return;
                    }
                    if (Input.IsKeyDown(InputKey.Right))
                    {
                        _dataSource.SelectNextCatalogRow();
                        _lastKeyRepeatTime = 0f;
                        return;
                    }
                }

                // Enter 确认选择目录项
                if (Input.IsKeyReleased(InputKey.Enter))
                {
                    _dataSource.ExecuteSelectFromCatalog();
                    return;
                }

                // Esc 退出目录视图（返回技能槽视图）
                if (_gauntletLayer.Input.IsHotKeyReleased("Exit"))
                {
                    _dataSource.ExecuteCloseCatalog();
                    return;
                }
            }
            else
            {
                // ====== 主界面键盘导航 ======
                if (canRepeat)
                {
                    // 目标列表: ↑↓
                    if (Input.IsKeyDown(InputKey.Down))
                    {
                        _dataSource.SelectNextHero();
                        _lastKeyRepeatTime = 0f;
                        return;
                    }
                    if (Input.IsKeyDown(InputKey.Up))
                    {
                        _dataSource.SelectPrevHero();
                        _lastKeyRepeatTime = 0f;
                        return;
                    }
                }

                // 技能槽位: 1~8（仅响应按下瞬间）
                if (Input.IsKeyReleased(InputKey.D1)) { _dataSource.SelectSlotByIndex(0); return; }
                if (Input.IsKeyReleased(InputKey.D2)) { _dataSource.SelectSlotByIndex(1); return; }
                if (Input.IsKeyReleased(InputKey.D3)) { _dataSource.SelectSlotByIndex(2); return; }
                if (Input.IsKeyReleased(InputKey.D4)) { _dataSource.SelectSlotByIndex(3); return; }
                if (Input.IsKeyReleased(InputKey.D5)) { _dataSource.SelectSlotByIndex(4); return; }
                if (Input.IsKeyReleased(InputKey.D6)) { _dataSource.SelectSlotByIndex(5); return; }
                if (Input.IsKeyReleased(InputKey.D7)) { _dataSource.SelectSlotByIndex(6); return; }
                if (Input.IsKeyReleased(InputKey.D8)) { _dataSource.SelectSlotByIndex(7); return; }

                // v2: F12 → 切换调试模式
                if (Input.IsKeyReleased(InputKey.F12))
                {
                    _dataSource.ExecuteToggleDebug();
                    return;
                }

                // v2: Tab → 循环目标类型
                if (Input.IsKeyReleased(InputKey.Tab))
                {
                    _dataSource.ExecuteCycleTargetType();
                    return;
                }

                // Ctrl+S → 应用更改
                if (IsControlDown() && Input.IsKeyReleased(InputKey.S))
                {
                    _dataSource.ExecuteApply();
                    return;
                }

                // Ctrl+E → 导出所有技能配置到 XML 文件
                if (IsControlDown() && Input.IsKeyReleased(InputKey.E))
                {
                    _dataSource.ExecuteExport();
                    return;
                }

                // Ctrl+Z → 撤销更改
                if (IsControlDown() && Input.IsKeyReleased(InputKey.Z))
                {
                    _dataSource.ExecuteUndoChanges();
                    return;
                }

                // ESC 关闭界面
                if (_gauntletLayer.Input.IsHotKeyReleased("Exit"))
                {
                    // v2: 简化，无弹窗层级
                    if (_dataSource.IsDirty)
                    {
                        // TODO: 弹出确认对话框
                    }
                    CloseScreen();
                    return;
                }
            }
        }

        private static bool IsControlDown()
        {
            return Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            if (_dataSource != null)
            {
                _dataSource.RefreshRoster();
            }
            if (_gauntletLayer != null)
            {
                ScreenManager.TrySetFocus(_gauntletLayer);
            }
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();
        }

        protected override void OnFinalize()
        {
            // 恢复大地图时间推进
            Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(this);

            base.OnFinalize();

            // v2: 不再释放弹窗 Movie

            // 释放主界面 Movie
            if (_gauntletLayer != null && _movie != null)
            {
                _gauntletLayer.ReleaseMovie(_movie);
            }
            _movie = null;

            if (_dataSource != null)
            {
                _dataSource.OnFinalize();
                _dataSource = null;
            }

            if (_gauntletLayer != null)
            {
                RemoveLayer(_gauntletLayer);
                _gauntletLayer = null;
            }
        }

        private void CloseScreen()
        {
            ScreenManager.PopScreen();
        }
    }
}
