using System;
using System.Collections.Generic; // 引用集合类库
using Helpers; // 自定义的帮助工具类
using SandBox.View; // 沙盒模式的视图相关类
using TaleWorlds.CampaignSystem; // 游戏战役系统的功能
using TaleWorlds.CampaignSystem.GameState; // 游戏状态相关的功能
using TaleWorlds.CampaignSystem.Inventory; // 库存管理功能
using TaleWorlds.CampaignSystem.Party; // 队伍管理功能
using TaleWorlds.CampaignSystem.Roster; // 名册管理功能
using TaleWorlds.CampaignSystem.Settlements; // 聚居地相关功能
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory; // 库存视图模型集合
using TaleWorlds.Core; // 核心功能模块
using TaleWorlds.Engine; // 引擎核心功能
using TaleWorlds.Engine.GauntletUI; // Gauntlet UI 系统
using TaleWorlds.GauntletUI.Data; // Gauntlet UI 数据处理
using TaleWorlds.InputSystem; // 输入系统
using TaleWorlds.Library; // 基础库功能
using TaleWorlds.Localization; // 本地化支持
using TaleWorlds.MountAndBlade; // Mount & Blade 游戏主模块
using TaleWorlds.MountAndBlade.View; // 视图相关功能
using TaleWorlds.MountAndBlade.View.Screens; // 屏幕管理功能
using TaleWorlds.ScreenSystem; // 屏幕系统
using TaleWorlds.TwoDimension; // 2D 图形处理

namespace New_ZZZF // 命名空间 PST
{
    [GameStateScreen(typeof(SkillInventoryState))] // 定义当前屏幕为 SkillInventoryState 的游戏状态屏幕
    public class GauntletSkillScreen : ScreenBase, IInventoryStateHandler, IGameStateListener, IChangeableScreen
    {
        public SkillInventoryState SkillInventoryState { get; private set; } // 当前技能库存状态

        protected override void OnFrameTick(float dt) // 每帧更新逻辑
        {
            base.OnFrameTick(dt); // 调用基类的 OnFrameTick 方法
            if (!this._closed) // 如果屏幕未关闭
            {
                LoadingWindow.DisableGlobalLoadingWindow(); // 禁用全局加载窗口
            }
            this._dataSource.IsFiveStackModifierActive = this._gauntletLayer.Input.IsHotKeyDown("FiveStackModifier"); // 检测五栈修饰键是否按下
            this._dataSource.IsEntireStackModifierActive = this._gauntletLayer.Input.IsHotKeyDown("EntireStackModifier"); // 检测全栈修饰键是否按下
            if (!this._dataSource.IsSearchAvailable || !this._gauntletLayer.IsFocusedOnInput()) // 如果搜索不可用或未聚焦输入
            {           
                //代码测试区
                if (Input.IsKeyPressed(InputKey.K))
                {

                    //SkillInventoryManager.ActivateTradeWithCurrentSettlement();
                        return;

                }
                //代码测试区

                if (this._gauntletLayer.Input.IsHotKeyReleased("SwitchAlternative") && this._dataSource != null) // 如果切换替代键释放且数据源不为空
                {
                    this._dataSource.CompareNextItem(); // 对比下一个物品
                    return;
                }
                if (this._gauntletLayer.Input.IsHotKeyDownAndReleased("Exit") || this._gauntletLayer.Input.IsGameKeyDownAndReleased(38)) // 如果退出键按下并释放
                {
                    this.ExecuteCancel(); // 执行取消操作
                    return;
                }
                if (this._gauntletLayer.Input.IsHotKeyDownAndReleased("Confirm")) // 如果确认键按下并释放
                {
                    this.ExecuteConfirm(); // 执行确认操作
                    return;
                }
                if (this._gauntletLayer.Input.IsHotKeyDownAndReleased("Reset")) // 如果重置键按下并释放
                {
                    this.HandleResetInput(); // 处理重置输入
                    return;
                }
                if (this._gauntletLayer.Input.IsHotKeyPressed("SwitchToPreviousTab")) // 如果切换到上一个标签页键按下
                {
                    if (!this._dataSource.IsFocusedOnItemList || !Input.IsGamepadActive) // 如果未聚焦于物品列表或手柄未激活
                    {
                        this.ExecuteSwitchToPreviousTab(); // 切换到上一个标签页
                        return;
                    }
                    if (this._dataSource.CurrentFocusedItem != null && this._dataSource.CurrentFocusedItem.IsTransferable && this._dataSource.CurrentFocusedItem.InventorySide == InventoryLogic.InventorySide.OtherInventory) // 如果当前聚焦物品可转移且在其他库存中
                    {
                        this.ExecuteBuySingle(); // 执行单个购买
                        return;
                    }
                }
                else if (this._gauntletLayer.Input.IsHotKeyPressed("SwitchToNextTab")) // 如果切换到下一个标签页键按下
                {
                    if (!this._dataSource.IsFocusedOnItemList || !Input.IsGamepadActive) // 如果未聚焦于物品列表或手柄未激活
                    {
                        this.ExecuteSwitchToNextTab(); // 切换到下一个标签页
                        return;
                    }
                    if (this._dataSource.CurrentFocusedItem != null && this._dataSource.CurrentFocusedItem.IsTransferable && this._dataSource.CurrentFocusedItem.InventorySide == InventoryLogic.InventorySide.PlayerInventory) // 如果当前聚焦物品可转移且在玩家库存中
                    {
                        this.ExecuteSellSingle(); // 执行单个出售
                        return;
                    }
                }
                else
                {
                    if (this._gauntletLayer.Input.IsHotKeyPressed("TakeAll")) // 如果“全部拿取”键按下
                    {
                        this.ExecuteTakeAll(); // 执行全部拿取
                        return;
                    }
                    if (this._gauntletLayer.Input.IsHotKeyPressed("GiveAll")) // 如果“全部给予”键按下
                    {
                        this.ExecuteGiveAll(); // 执行全部给予
                    }
                }
            }
        }
        public GauntletSkillScreen() {  }
        public GauntletSkillScreen(SkillInventoryState SkillInventoryState) // 构造函数
        {
            this.SkillInventoryState = SkillInventoryState; // 初始化技能库存状态
            this.SkillInventoryState.Handler = this; // 设置当前实例为处理器
        }

        protected override void OnInitialize() // 初始化逻辑
        {
            SpriteData spriteData = UIResourceManager.SpriteData; // 获取精灵数据
            TwoDimensionEngineResourceContext resourceContext = UIResourceManager.ResourceContext; // 获取资源上下文
            ResourceDepot uiresourceDepot = UIResourceManager.UIResourceDepot; // 获取 UI 资源仓库
            this._inventoryCategory = spriteData.SpriteCategories["ui_inventory"]; // 加载库存精灵类别
            this._inventoryCategory.Load(resourceContext, uiresourceDepot); // 加载资源
            InventoryLogic inventoryLogic = this.SkillInventoryState.InventoryLogic; // 获取库存逻辑
            Mission mission = Mission.Current; // 获取当前任务
            this._dataSource = new SPSkillVM(inventoryLogic, false/*mission != null && mission.DoesMissionRequireCivilianEquipment*/, new Func<WeaponComponentData, ItemObject.ItemUsageSetFlags>(this.GetItemUsageSetFlag), this.GetFiveStackShortcutkeyText(), this.GetEntireStackShortcutkeyText()); // 初始化数据源
            this._dataSource.SetGetKeyTextFromKeyIDFunc(new Func<string, TextObject>(Game.Current.GameTextManager.GetHotKeyGameTextFromKeyID)); // 设置按键文本获取函数
            this._dataSource.SetResetInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Reset")); // 设置重置键
            this._dataSource.SetCancelInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Exit")); // 设置取消键
            this._dataSource.SetDoneInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Confirm")); // 设置确认键
            this._dataSource.SetPreviousCharacterInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("SwitchToPreviousTab")); // 设置上一个角色键
            this._dataSource.SetNextCharacterInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("SwitchToNextTab")); // 设置下一个角色键
            this._dataSource.SetBuyAllInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("TakeAll")); // 设置全部购买键
            this._dataSource.SetSellAllInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("GiveAll")); // 设置全部出售键
            this._gauntletLayer = new GauntletLayer(15, "GauntletLayer", true) // 创建 Gauntlet 层
            {
                IsFocusLayer = true // 设置为聚焦层
            };
            this._gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All); // 设置输入限制
            base.AddLayer(this._gauntletLayer); // 添加层
            ScreenManager.TrySetFocus(this._gauntletLayer); // 尝试设置焦点
            this._gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory")); // 注册通用面板热键类别
            this._gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory")); // 注册战役面板热键类别
            this._gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("InventoryHotKeyCategory")); // 注册库存热键类别
            this._gauntletMovie = this._gauntletLayer.LoadMovie("Inventory", this._dataSource); // 加载电影（界面）
            this._openedFromMission = (this.SkillInventoryState.Predecessor is MissionState); // 检查是否从任务状态打开
            InformationManager.ClearAllMessages(); // 清除所有消息
            UISoundsHelper.PlayUISound("event:/ui/panels/panel_inventory_open"); // 播放打开音效
            this._gauntletLayer.GamepadNavigationContext.GainNavigationAfterFrames(2, null); // 设置手柄导航
        }

        private string GetFiveStackShortcutkeyText() // 获取五栈快捷键文本
        {
            if (!Input.IsControllerConnected || Input.IsMouseActive) // 如果未连接控制器或鼠标激活
            {
                return Module.CurrentModule.GlobalTextManager.FindText("str_game_key_text", "anyshift").ToString(); // 返回 Shift 键文本
            }
            return string.Empty; // 否则返回空字符串
        }

        private string GetEntireStackShortcutkeyText() // 获取全栈快捷键文本
        {
            if (!Input.IsControllerConnected || Input.IsMouseActive) // 如果未连接控制器或鼠标激活
            {
                return Module.CurrentModule.GlobalTextManager.FindText("str_game_key_text", "anycontrol").ToString(); // 返回 Control 键文本
            }
            return null; // 否则返回 null
        }

        protected override void OnDeactivate() // 停用逻辑
        {
            base.OnDeactivate(); // 调用基类的停用方法
            this._closed = true; // 设置关闭标志
            MBInformationManager.HideInformations(); // 隐藏信息
        }

        protected override void OnActivate() // 激活逻辑
        {
            base.OnActivate(); // 调用基类的激活方法
            SPSkillVM dataSource = this._dataSource; // 获取数据源
            if (dataSource != null) // 如果数据源不为空
            {
                dataSource.RefreshCallbacks(); // 刷新回调
            }
            if (this._gauntletLayer != null) // 如果 Gauntlet 层不为空
            {
                ScreenManager.TrySetFocus(this._gauntletLayer); // 尝试设置焦点
            }
        }

        protected override void OnFinalize() // 最终化逻辑
        {
            base.OnFinalize(); // 调用基类的最终化方法
            this._gauntletMovie = null; // 清空电影对象
            this._inventoryCategory.Unload(); // 卸载库存精灵类别
            this._dataSource.OnFinalize(); // 数据源最终化
            this._dataSource = null; // 清空数据源
            this._gauntletLayer = null; // 清空 Gauntlet 层
        }

        void IGameStateListener.OnActivate() // 游戏状态监听器激活
        {
            Game.Current.EventManager.TriggerEvent<TutorialContextChangedEvent>(new TutorialContextChangedEvent(TutorialContexts.InventoryScreen)); // 触发教程上下文变更事件
        }

        void IGameStateListener.OnDeactivate() // 游戏状态监听器停用
        {
            Game.Current.EventManager.TriggerEvent<TutorialContextChangedEvent>(new TutorialContextChangedEvent(TutorialContexts.None)); // 触发教程上下文变更事件
        }

        void IGameStateListener.OnInitialize() // 游戏状态监听器初始化
        {
        }

        void IGameStateListener.OnFinalize() // 游戏状态监听器最终化
        {
        }

        void IInventoryStateHandler.FilterInventoryAtOpening(InventoryManager.InventoryCategoryType inventoryCategoryType) // 在打开时过滤库存
        {
            if (this._dataSource == null) // 如果数据源为空
            {
                Debug.FailedAssert("Data source is not initialized when filtering inventory", "C:\\Develop\\MB3\\Source\\Bannerlord\\SandBox.GauntletUI\\GauntletInventoryScreen.cs", "FilterInventoryAtOpening", 234); // 断言失败
                return;
            }
            switch (inventoryCategoryType) // 根据库存类别类型过滤
            {
                case InventoryManager.InventoryCategoryType.Armors: // 如果是盔甲类别
                    this._dataSource.ExecuteFilterArmors(); // 执行盔甲过滤
                    return;
                case InventoryManager.InventoryCategoryType.Weapon: // 如果是武器类别
                    this._dataSource.ExecuteFilterWeapons(); // 执行武器过滤
                    return;
                case InventoryManager.InventoryCategoryType.Shield: // 如果是盾牌类别
                    break;
                case InventoryManager.InventoryCategoryType.HorseCategory: // 如果是马匹类别
                    this._dataSource.ExecuteFilterMounts(); // 执行坐骑过滤
                    return;
                case InventoryManager.InventoryCategoryType.Goods: // 如果是商品类别
                    this._dataSource.ExecuteFilterMisc(); // 执行杂项过滤
                    break;
                default:
                    return;
            }
        }

        public void ExecuteLootingScript() // 执行掠夺脚本
        {
            this._dataSource.ExecuteBuyAllItems(); // 执行全部购买
        }

        public void ExecuteSellAllLoot() // 执行出售所有战利品
        {
            this._dataSource.ExecuteSellAllItems(); // 执行全部出售
        }

        private void HandleResetInput() // 处理重置输入
        {
            if (!this._dataSource.ItemPreview.IsSelected) // 如果未选择物品预览
            {
                this._dataSource.ExecuteResetTranstactions(); // 执行重置交易
                UISoundsHelper.PlayUISound("event:/ui/default"); // 播放默认音效
            }
        }

        public void ExecuteCancel() // 执行取消操作
        {
            if (this._dataSource.ItemPreview.IsSelected) // 如果选择了物品预览
            {
                UISoundsHelper.PlayUISound("event:/ui/default"); // 播放默认音效
                this._dataSource.ClosePreview(); // 关闭预览
                return;
            }
            if (this._dataSource.IsExtendedEquipmentControlsEnabled) // 如果扩展装备控制已启用
            {
                this._dataSource.IsExtendedEquipmentControlsEnabled = false; // 禁用扩展装备控制
                return;
            }
            UISoundsHelper.PlayUISound("event:/ui/default"); // 播放默认音效
            this._dataSource.ExecuteResetAndCompleteTranstactions(); // 执行重置和完成交易
        }

        public void ExecuteConfirm() // 执行确认操作
        {
            if (!this._dataSource.ItemPreview.IsSelected && !this._dataSource.IsDoneDisabled) // 如果未选择物品预览且未禁用完成按钮
            {
                this._dataSource.ExecuteCompleteTranstactions(); // 执行完成交易
                UISoundsHelper.PlayUISound("event:/ui/default"); // 播放默认音效
            }
        }

        public void ExecuteSwitchToPreviousTab() // 切换到上一个标签页
        {
            if (!this._dataSource.ItemPreview.IsSelected) // 如果未选择物品预览
            {
                MBBindingList<InventoryCharacterSelectorItemVM> itemList = this._dataSource.CharacterList.ItemList; // 获取角色列表
                if (itemList != null && itemList.Count > 1) // 如果列表不为空且数量大于 1
                {
                    UISoundsHelper.PlayUISound("event:/ui/default"); // 播放默认音效
                }
                this._dataSource.CharacterList.ExecuteSelectPreviousItem(); // 执行选择上一个角色
            }
        }

        public void ExecuteSwitchToNextTab() // 切换到下一个标签页
        {
            if (!this._dataSource.ItemPreview.IsSelected) // 如果未选择物品预览
            {
                MBBindingList<InventoryCharacterSelectorItemVM> itemList = this._dataSource.CharacterList.ItemList; // 获取角色列表
                if (itemList != null && itemList.Count > 1) // 如果列表不为空且数量大于 1
                {
                    UISoundsHelper.PlayUISound("event:/ui/default"); // 播放默认音效
                }
                this._dataSource.CharacterList.ExecuteSelectNextItem(); // 执行选择下一个角色
            }
        }

        public void ExecuteBuySingle() // 执行单个购买
        {
            this._dataSource.CurrentFocusedItem.ExecuteBuySingle(); // 执行单个购买
            UISoundsHelper.PlayUISound("event:/ui/transfer"); // 播放转移音效
        }

        public void ExecuteSellSingle() // 执行单个出售
        {
            this._dataSource.CurrentFocusedItem.ExecuteSellSingle(); // 执行单个出售
            UISoundsHelper.PlayUISound("event:/ui/transfer"); // 播放转移音效
        }

        public void ExecuteTakeAll() // 执行全部拿取
        {
            if (!this._dataSource.ItemPreview.IsSelected) // 如果未选择物品预览
            {
                this._dataSource.ExecuteBuyAllItems(); // 执行全部购买
                UISoundsHelper.PlayUISound("event:/ui/inventory/take_all"); // 播放全部拿取音效
            }
        }

        public void ExecuteGiveAll() // 执行全部给予
        {
            if (!this._dataSource.ItemPreview.IsSelected) // 如果未选择物品预览
            {
                this._dataSource.ExecuteSellAllItems(); // 执行全部出售
                UISoundsHelper.PlayUISound("event:/ui/inventory/take_all"); // 播放全部拿取音效
            }
        }

        public void ExecuteBuyConsumableItem() // 执行购买消耗品
        {
            this._dataSource.ExecuteBuyItemTest(); // 执行购买测试
        }

        private ItemObject.ItemUsageSetFlags GetItemUsageSetFlag(WeaponComponentData item) // 获取物品使用标志
        {
            if (!string.IsNullOrEmpty(item.ItemUsage)) // 如果物品使用字段不为空
            {
                return MBItem.GetItemUsageSetFlags(item.ItemUsage); // 返回物品使用标志
            }
            return (ItemObject.ItemUsageSetFlags)0; // 否则返回默认值
        }

        private void CloseInventoryScreen() // 关闭库存屏幕
        {
            InventoryManager.Instance.CloseInventoryPresentation(false); // 关闭库存展示
        }

        bool IChangeableScreen.AnyUnsavedChanges() // 检查是否有未保存的更改
        {
            return this.SkillInventoryState.InventoryLogic.IsThereAnyChanges(); // 返回是否有任何更改
        }

        bool IChangeableScreen.CanChangesBeApplied() // 检查更改是否可以应用
        {
            return this.SkillInventoryState.InventoryLogic.CanPlayerCompleteTransaction(); // 返回玩家是否可以完成交易
        }

        void IChangeableScreen.ApplyChanges() // 应用更改
        {
            this._dataSource.ItemPreview.Close(); // 关闭物品预览
            this.SkillInventoryState.InventoryLogic.DoneLogic(); // 完成交易逻辑
        }

        void IChangeableScreen.ResetChanges() // 重置更改
        {
            this.SkillInventoryState.InventoryLogic.Reset(true); // 重置交易逻辑
        }

        private IGauntletMovie _gauntletMovie; // Gauntlet 电影对象
        private SPSkillVM _dataSource; // 数据源
        private GauntletLayer _gauntletLayer; // Gauntlet 层
        private bool _closed; // 是否关闭
        private bool _openedFromMission; // 是否从任务状态打开
        private SpriteCategory _inventoryCategory; // 库存精灵类别
    }
}