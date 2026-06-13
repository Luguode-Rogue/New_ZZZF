using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF
{
    // =========================================================================
    // 任务四：屏幕与图层管理 (Screen)
    //
    // 全新的技能界面 Screen：
    //   - 不依赖 InventoryLogic / IInventoryStateHandler / IChangeableScreen
    //   - 不依赖 SkillInventoryState / PlayerGameState
    //   - 使用 CustomSkillScreenVM 作为纯净 ViewModel
    //   - 加载自定义 CustomSkillScreen.xml 布局
    // =========================================================================

    /// <summary>
    /// 技能装备界面 Screen。
    /// 通过 ScreenManager.PushScreen(new CustomSkillScreen()) 直接推入屏幕栈。
    /// 不绑定任何 GameState，不依赖物品系统。
    /// </summary>
    public class CustomSkillScreen : ScreenBase
    {
        private CustomSkillScreenVM _dataSource;
        private GauntletLayer _gauntletLayer;
        private GauntletMovieIdentifier _movie;
        private GauntletMovieIdentifier _popupMovie;
        private bool _hasPopup = false;
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

            // 创建 ViewModel
            _dataSource = new CustomSkillScreenVM();
            _dataSource.SetCloseAction(CloseScreen);

            // 创建 Gauntlet 图层（层序 100，高于大部分游戏 UI）
            _gauntletLayer = new GauntletLayer("CustomSkillScreen", 100)
            {
                IsFocusLayer = true
            };
            _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);

            AddLayer(_gauntletLayer);
            ScreenManager.TrySetFocus(_gauntletLayer);

            // 注册热键
            _gauntletLayer.Input.RegisterHotKeyCategory(
                HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));

            // 加载自定义 XML 预制件
            _movie = _gauntletLayer.LoadMovie("CustomSkillScreen", _dataSource);
            // SkillDebug.Log($"[CSS] OnInitialize 完成: _movie={(_movie != null)}, _dataSource.Roster.Count={_dataSource?.Roster?.Count}, _dataSource.Skills.Count={_dataSource?.Skills?.Count}");
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);

            if (_gauntletLayer == null || _dataSource == null) return;

            // 检查弹窗状态变化
            bool hasPopupNow = _dataSource.IsPopupOpen;
            
            if (hasPopupNow && !_hasPopup)
            {
                // 弹窗刚刚打开：加载弹窗Movie
                _popupMovie = _gauntletLayer.LoadMovie("SkillSelectionPopup", _dataSource.SkillSelectionPopup);
                _hasPopup = true;
                // SkillDebug.Log($"[CSS] 弹窗已打开: _popupMovie={(_popupMovie != null)}, FilteredSkills.Count={_dataSource.SkillSelectionPopup?.FilteredSkills?.Count}, IsVisible={_dataSource.SkillSelectionPopup?.IsVisible}");
            }
            else if (!hasPopupNow && _hasPopup)
            {
                // 弹窗刚刚关闭：释放弹窗Movie
                if (_popupMovie != null)
                {
                    _gauntletLayer.ReleaseMovie(_popupMovie);
                    _popupMovie = null;
                }
                _hasPopup = false;
                // SkillDebug.Log("[CSS] 弹窗已关闭 (ReleaseMovie 完成)");
            }

            // ---- 键盘事件处理（带冷却防重复触发）----
            bool canRepeat = (dt > 0f) && ((_lastKeyRepeatTime += dt) >= KeyRepeatInterval);

            if (_hasPopup)
            {
                // ====== 弹窗内键盘导航 ======
                if (canRepeat)
                {
                    if (Input.IsKeyDown(InputKey.Down))
                    {
                        _dataSource.PopupSelectNextSkill();
                        _lastKeyRepeatTime = 0f;
                        return;
                    }
                    if (Input.IsKeyDown(InputKey.Up))
                    {
                        _dataSource.PopupSelectPrevSkill();
                        _lastKeyRepeatTime = 0f;
                        return;
                    }
                }

                // Enter 确认选择（无需冷却，只响应按下瞬间）
                if (Input.IsKeyReleased(InputKey.Enter))
                {
                    _dataSource.PopupSelectCurrentSkill();
                    return;
                }
            }
            else
            {
                // ====== 主界面键盘导航 ======
                if (canRepeat)
                {
                    // 英雄列表: ↑↓
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

                // 技能槽位: 1~8（仅响应按下瞬间，不需要冷却）
                if (Input.IsKeyReleased(InputKey.D1)) { _dataSource.SelectSlotByIndex(0); return; }
                if (Input.IsKeyReleased(InputKey.D2)) { _dataSource.SelectSlotByIndex(1); return; }
                if (Input.IsKeyReleased(InputKey.D3)) { _dataSource.SelectSlotByIndex(2); return; }
                if (Input.IsKeyReleased(InputKey.D4)) { _dataSource.SelectSlotByIndex(3); return; }
                if (Input.IsKeyReleased(InputKey.D5)) { _dataSource.SelectSlotByIndex(4); return; }
                if (Input.IsKeyReleased(InputKey.D6)) { _dataSource.SelectSlotByIndex(5); return; }
                if (Input.IsKeyReleased(InputKey.D7)) { _dataSource.SelectSlotByIndex(6); return; }
                if (Input.IsKeyReleased(InputKey.D8)) { _dataSource.SelectSlotByIndex(7); return; }

                // Ctrl+S → 应用更改
                if (IsControlDown() && Input.IsKeyReleased(InputKey.S))
                {
                    _dataSource.ExecuteApply();
                    return;
                }

                // Ctrl+Z → 撤销更改
                if (IsControlDown() && Input.IsKeyReleased(InputKey.Z))
                {
                    _dataSource.ExecuteUndoChanges();
                    return;
                }
            }

            // ESC 关闭界面或弹窗
            if (_gauntletLayer.Input.IsHotKeyReleased("Exit"))
            {
                if (_hasPopup)
                {
                    // 如果弹窗打开，先关闭弹窗
                    if (_dataSource?.SkillSelectionPopup != null)
                    {
                        _dataSource.SkillSelectionPopup.ExecuteClose();
                    }
                    return;
                }

                // 如果有未保存更改，先提示（后续可接入确认弹窗）
                if (_dataSource.IsDirty)
                {
                    // TODO: 弹出确认对话框 "是否放弃未保存的更改？"
                    // 目前直接关闭（丢弃更改）
                }
                CloseScreen();
                return;
            }
        }

        private static bool IsControlDown()
        {
            return Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            // SkillDebug.Log($"[CSS] OnActivate: Roster.Count={_dataSource?.Roster?.Count}, Skills.Count={_dataSource?.Skills?.Count}");

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
            // SkillDebug.Log("[CSS] OnFinalize 开始");
            base.OnFinalize();

            // 释放弹窗Movie
            if (_gauntletLayer != null && _popupMovie != null)
            {
                _gauntletLayer.ReleaseMovie(_popupMovie);
            }
            _popupMovie = null;

            // 释放主界面Movie
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

        // =========================================================================
        // 私有方法
        // =========================================================================

        private void CloseScreen()
        {
            ScreenManager.PopScreen();
        }
    }
}
