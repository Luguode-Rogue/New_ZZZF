using System; // 系统基础命名空间
using System.Collections.Generic; // 集合类支持
using System.Reflection; // 反射相关操作
using System.Runtime.CompilerServices; // 运行时编译特性
using Helpers; // 自定义辅助工具
using TaleWorlds.CampaignSystem; // 战役系统核心
using TaleWorlds.CampaignSystem.GameState; // 游戏状态管理
using TaleWorlds.CampaignSystem.Inventory; // 库存系统
using TaleWorlds.CampaignSystem.MapEvents; // 地图事件
using TaleWorlds.CampaignSystem.Party; // 队伍管理
using TaleWorlds.CampaignSystem.Roster; // 物品清单
using TaleWorlds.CampaignSystem.Settlements; // 聚居地相关
using TaleWorlds.Core; // 核心功能
using TaleWorlds.Library; // 基础库支持
using TaleWorlds.Localization; // 本地化支持

namespace New_ZZZF // 自定义命名空间
{
    public class SkillInventoryManager : InventoryManager // 继承原生库存管理器
    {
        // 当前库存模式属性（注意：set存在递归调用问题）
        public InventoryMode CurrentMode
        {
            set { CurrentMode = value; } // 递归赋值会导致栈溢出
            get { return _currentMode; }
        }

        // 单例实例获取（通过反射同步原生管理器状态）
        private void SyncWithOriginalManager()
        {
            try
            {
                // 获取原生库存管理器实例
                InventoryManager original = Campaign.Current.InventoryManager;
                if (original == null)
                {
                    //Debug.LogError("原生库存管理器未初始化");
                    return;
                }

                // 通过反射获取私有字段信息
                Type inventoryManagerType = typeof(InventoryManager);

                // 获取_currentMode字段
                FieldInfo currentModeField = inventoryManagerType.GetField(
                    "_currentMode",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                // 获取_inventoryLogic字段
                FieldInfo inventoryLogicField = inventoryManagerType.GetField(
                    "_inventoryLogic",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                // 获取_doneLogicExtrasDelegate字段
                FieldInfo doneDelegateField = inventoryManagerType.GetField(
                    "_doneLogicExtrasDelegate",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                // 检查字段是否存在
                if (currentModeField == null || inventoryLogicField == null || doneDelegateField == null)
                {
                    //Debug.LogError("反射获取字段失败");
                    return;
                }

                // 同步_currentMode
                InventoryMode originalMode = (InventoryMode)currentModeField.GetValue(original);
                if (this._currentMode != originalMode)
                {
                    this._currentMode = originalMode;
                }

                // 同步_inventoryLogic
                InventoryLogic originalLogic = (InventoryLogic)inventoryLogicField.GetValue(original);
                if (this._inventoryLogic != originalLogic)
                {
                    this._inventoryLogic = originalLogic ?? new InventoryLogic(null);
                }

                // 同步_doneLogicExtrasDelegate
                DoneLogicExtrasDelegate originalDelegate =
                    (DoneLogicExtrasDelegate)doneDelegateField.GetValue(original);
                if (this._doneLogicExtrasDelegate != originalDelegate)
                {
                    this._doneLogicExtrasDelegate = originalDelegate;
                }
            }
            catch (Exception ex)
            {
                //Debug.LogError($"同步库存管理器状态失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // 当前库存逻辑快捷访问
        public static InventoryLogic InventoryLogic => Instance._inventoryLogic;

        // 玩家接受交易报价
        public void PlayerAcceptTradeOffer()
        {
            _inventoryLogic?.SetPlayerAcceptTraderOffer();
        }

        // 关闭库存界面
        public void CloseInventoryPresentation(bool fromCancel)
        {
            if (_inventoryLogic?.DoneLogic() == true)
            {
                Game.Current.GameStateManager.PopState(0);
                _doneLogicExtrasDelegate?.Invoke();
                _doneLogicExtrasDelegate = null;
                _inventoryLogic = null;
            }
        }

        // 打开库存界面（内部使用）
        private void OpenInventoryPresentation(TextObject leftRosterName)
        {
            var itemRoster = new ItemRoster();

            // 非测试模式下填充书籍物品
            if (TestCommonBase.BaseInstance == null || !TestCommonBase.BaseInstance.IsTestEnabled)
            {
                foreach (var item in Game.Current.ObjectManager.GetObjectTypeList<ItemObject>())
                {
                    if (item.ItemType == ItemObject.ItemTypeEnum.Book)
                        itemRoster.AddToCounts(item, 10);
                }
            }

            // 初始化库存逻辑
            _inventoryLogic = new InventoryLogic(null);
            _inventoryLogic.Initialize(
                itemRoster,
                new ItemRoster(),
                MobileParty.MainParty.MemberRoster,
                false,
                false,
                CharacterObject.PlayerCharacter,
                InventoryManager.InventoryCategoryType.None,
                GetCurrentMarketData(),
                true,
                leftRosterName,
                null,
                null
            );

            // 切换游戏状态
            var inventoryState = Game.Current.GameStateManager.CreateState<SkillInventoryState>();
            inventoryState.InitializeLogic(_inventoryLogic);
            Game.Current.GameStateManager.PushState(inventoryState, 0);
        }

        // 获取当前市场数据（支持村庄/城镇/模拟数据）
        private static IMarketData GetCurrentMarketData()
        {
            IMarketData marketData = null;
            if (Campaign.Current.GameMode == CampaignGameMode.Campaign)
            {
                var settlement = MobileParty.MainParty.CurrentSettlement
                    ?? SettlementHelper.FindNearestTown(null, null);

                if (settlement != null)
                {
                    if (settlement.IsVillage)
                        marketData = settlement.Village.MarketData;
                    else if (settlement.IsTown)
                        marketData = settlement.Town.MarketData;
                }
            }
            return marketData ?? new FakeMarketData();
        }

        // 模拟市场数据实现
        internal class FakeMarketData : IMarketData
        {
            // 自动收集对象（反射支持）
            internal static void AutoGeneratedStaticCollectObjectsFakeMarketData(object o, List<object> collectedObjects)
            {
                ((FakeMarketData)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
            }

            // 实例级对象收集
            protected virtual void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects) { }

            // 获取物品基础价格
            public int GetPrice(ItemObject item, MobileParty tradingParty, bool isSelling, PartyBase merchantParty)
                => item.Value;

            // 获取装备元素价格
            public int GetPrice(EquipmentElement itemRosterElement, MobileParty tradingParty, bool isSelling, PartyBase merchantParty)
                => itemRosterElement.ItemValue;
        }

        // 打开子队伍库存界面
        public static void OpenScreenAsInventoryOfSubParty(MobileParty rightParty, MobileParty leftParty, DoneLogicExtrasDelegate doneDelegate)
        {
            var leader = rightParty.LeaderHero?.CharacterObject;
            var logic = new InventoryLogic(rightParty, leader, leftParty.Party);

            logic.Initialize(
                leftParty.ItemRoster,
                rightParty.ItemRoster,
                rightParty.MemberRoster,
                false,
                false,
                leader,
                InventoryManager.InventoryCategoryType.None,
                GetCurrentMarketData(),
                false,
                null,
                null,
                null
            );

            Instance._doneLogicExtrasDelegate = doneDelegate;
            var state = Game.Current.GameStateManager.CreateState<InventoryState>();
            state.InitializeLogic(logic);
            Game.Current.GameStateManager.PushState(state, 0);
        }

        // 打开分解物品界面
        public static void OpenScreenAsInventoryForCraftedItemDecomposition(MobileParty party, CharacterObject character, DoneLogicExtrasDelegate doneDelegate)
        {
            Instance._inventoryLogic = new InventoryLogic(null);
            Instance._inventoryLogic.Initialize(
                new ItemRoster(),
                party.ItemRoster,
                party.MemberRoster,
                false,
                false,
                character,
                InventoryManager.InventoryCategoryType.None,
                GetCurrentMarketData(),
                false,
                null,
                null,
                null
            );

            Instance._doneLogicExtrasDelegate = doneDelegate;
            var state = Game.Current.GameStateManager.CreateState<InventoryState>();
            state.InitializeLogic(Instance._inventoryLogic);
            Game.Current.GameStateManager.PushState(state, 0);
        }

        // 打开队伍库存界面
        public static void OpenScreenAsInventoryOf(MobileParty party, CharacterObject character)
        {
            Instance._inventoryLogic = new InventoryLogic(null);
            Instance._inventoryLogic.Initialize(
                new ItemRoster(),
                party.ItemRoster,
                party.MemberRoster,
                false,
                true,
                character,
                InventoryManager.InventoryCategoryType.None,
                GetCurrentMarketData(),
                false,
                null,
                null,
                null
            );

            var state = Game.Current.GameStateManager.CreateState<InventoryState>();
            state.InitializeLogic(Instance._inventoryLogic);
            Game.Current.GameStateManager.PushState(state, 0);
        }

        // 打开双队伍库存界面
        public static void OpenScreenAsInventoryOf(PartyBase rightParty, PartyBase leftParty)
        {
            Instance._inventoryLogic = new InventoryLogic(leftParty);
            Instance._inventoryLogic.Initialize(
                leftParty.ItemRoster,
                rightParty.ItemRoster,
                rightParty.MemberRoster,
                false,
                false,
                rightParty.LeaderHero?.CharacterObject,
                InventoryManager.InventoryCategoryType.None,
                GetCurrentMarketData(),
                false,
                null,
                leftParty.MemberRoster,
                null
            );

            var state = Game.Current.GameStateManager.CreateState<InventoryState>();
            state.InitializeLogic(Instance._inventoryLogic);
            Game.Current.GameStateManager.PushState(state, 0);
        }

        // 打开默认库存界面
        public static void OpenScreenAsInventory(DoneLogicExtrasDelegate doneDelegate = null)
        {
            Instance._currentMode = InventoryMode.Default;
            Instance.OpenInventoryPresentation(new TextObject("{=1234564}待选技能"));
            Instance._doneLogicExtrasDelegate = doneDelegate;
        }

        // 打开战斗战利品界面
        public static void OpenCampaignBattleLootScreen()
        {
            var lootDict = new Dictionary<PartyBase, ItemRoster>
            {
                { PartyBase.MainParty, MapEvent.PlayerMapEvent.GetMapEventSide(PartyBase.MainParty.Side).ItemRosterForPlayerLootShare(PartyBase.MainParty) }
            };
            OpenScreenAsLoot(lootDict);
        }

        // 打开战利品分配界面
        public static void OpenScreenAsLoot(Dictionary<PartyBase, ItemRoster> itemRostersToLoot)
        {
            var leftRoster = itemRostersToLoot[PartyBase.MainParty];
            Instance._currentMode = InventoryMode.Loot;
            Instance._inventoryLogic = new InventoryLogic(null);

            Instance._inventoryLogic.Initialize(
                leftRoster,
                MobileParty.MainParty.ItemRoster,
                MobileParty.MainParty.MemberRoster,
                false,
                true,
                CharacterObject.PlayerCharacter,
                InventoryManager.InventoryCategoryType.None,
                GetCurrentMarketData(),
                false,
                null,
                null,
                null
            );

            var state = Game.Current.GameStateManager.CreateState<InventoryState>();
            state.InitializeLogic(Instance._inventoryLogic);
            Game.Current.GameStateManager.PushState(state, 0);
        }

        // 打开仓库界面
        public static void OpenScreenAsStash(ItemRoster stash)
        {
            Instance._currentMode = InventoryMode.Stash;
            Instance._inventoryLogic = new InventoryLogic(null);

            Instance._inventoryLogic.Initialize(
                stash,
                MobileParty.MainParty,
                false,
                false,
                CharacterObject.PlayerCharacter,
                InventoryManager.InventoryCategoryType.None,
                GetCurrentMarketData(),
                false,
                new TextObject("{=nZbaYvVx}Stash"),
                null,
                null
            );

            var state = Game.Current.GameStateManager.CreateState<InventoryState>();
            state.InitializeLogic(Instance._inventoryLogic);
            Game.Current.GameStateManager.PushState(state, 0);
        }

        // 打开带容量限制的仓库界面
        public static void OpenScreenAsWarehouse(ItemRoster stash, InventoryLogic.CapacityData capacity)
        {
            Instance._currentMode = InventoryMode.Warehouse;
            Instance._inventoryLogic = new InventoryLogic(null);

            Instance._inventoryLogic.Initialize(
                stash,
                MobileParty.MainParty,
                false,
                false,
                CharacterObject.PlayerCharacter,
                InventoryManager.InventoryCategoryType.None,
                GetCurrentMarketData(),
                false,
                new TextObject("{=anTRftmb}Warehouse"),
                null,
                capacity
            );

            var state = Game.Current.GameStateManager.CreateState<InventoryState>();
            state.InitializeLogic(Instance._inventoryLogic);
            Game.Current.GameStateManager.PushState(state, 0);
        }

        // 打开物品接收界面
        public static void OpenScreenAsReceiveItems(ItemRoster items, TextObject leftName, DoneLogicExtrasDelegate doneDelegate = null)
        {
            Instance._currentMode = InventoryMode.Default;
            Instance._inventoryLogic = new InventoryLogic(null);

            Instance._inventoryLogic.Initialize(
                items,
                MobileParty.MainParty.ItemRoster,
                MobileParty.MainParty.MemberRoster,
                false,
                true,
                CharacterObject.PlayerCharacter,
                InventoryManager.InventoryCategoryType.None,
                GetCurrentMarketData(),
                false,
                leftName,
                null,
                null
            );

            Instance._doneLogicExtrasDelegate = doneDelegate;
            var state = Game.Current.GameStateManager.CreateState<InventoryState>();
            state.InitializeLogic(Instance._inventoryLogic);
            Game.Current.GameStateManager.PushState(state, 0);
        }

        // 打开商队交易界面
        public static void OpenTradeWithCaravanOrAlleyParty(MobileParty caravan, InventoryManager.InventoryCategoryType type)
        {
            Instance._currentMode = InventoryMode.Trade;
            Instance._inventoryLogic = new InventoryLogic(caravan.Party);

            Instance._inventoryLogic.Initialize(
                caravan.Party.ItemRoster,
                PartyBase.MainParty.ItemRoster,
                PartyBase.MainParty.MemberRoster,
                true,
                true,
                CharacterObject.PlayerCharacter,
                type,
                GetCurrentMarketData(),
                false,
                null,
                null,
                null
            );

            Instance._inventoryLogic.SetInventoryListener(new CaravanInventoryListener(caravan));
            var state = Game.Current.GameStateManager.CreateState<InventoryState>();
            state.InitializeLogic(Instance._inventoryLogic);
            Game.Current.GameStateManager.PushState(state, 0);
        }

        // 激活当前聚居地交易
        public static void ActivateTradeWithCurrentSettlement()
        {
            OpenScreenAsTrade(
                Settlement.CurrentSettlement.ItemRoster,
                Settlement.CurrentSettlement.SettlementComponent,
                InventoryManager.InventoryCategoryType.None,
                null
            );
        }

        // 打开交易界面
        public static void OpenScreenAsTrade(ItemRoster leftRoster, SettlementComponent component, InventoryManager.InventoryCategoryType type, DoneLogicExtrasDelegate doneDelegate)
        {
            Instance._currentMode = InventoryMode.Trade;
            Instance._inventoryLogic = new InventoryLogic(component.Owner);

            Instance._inventoryLogic.Initialize(
                leftRoster,
                PartyBase.MainParty.ItemRoster,
                PartyBase.MainParty.MemberRoster,
                true,
                true,
                CharacterObject.PlayerCharacter,
                type,
                GetCurrentMarketData(),
                false,
                null,
                null,
                null
            );

            Instance._inventoryLogic.SetInventoryListener(new MerchantInventoryListener(component));
            Instance._doneLogicExtrasDelegate = doneDelegate;

            var state = Game.Current.GameStateManager.CreateState<InventoryState>();
            state.InitializeLogic(Instance._inventoryLogic);
            Game.Current.GameStateManager.PushState(state, 0);

            state.Handler?.FilterInventoryAtOpening(type);
        }

        // 物品类型转换
        public static InventoryItemType GetInventoryItemTypeOfItem(ItemObject item)
        {
            if (item == null) return InventoryItemType.None;
            return item.ItemType switch
            {
                ItemObject.ItemTypeEnum.Horse => InventoryItemType.Horse,
                ItemObject.ItemTypeEnum.OneHandedWeapon => InventoryItemType.Weapon,
                ItemObject.ItemTypeEnum.TwoHandedWeapon => InventoryItemType.Weapon,
                ItemObject.ItemTypeEnum.Polearm => InventoryItemType.Weapon,
                ItemObject.ItemTypeEnum.Arrows => InventoryItemType.Weapon,
                ItemObject.ItemTypeEnum.Bolts => InventoryItemType.Weapon,
                ItemObject.ItemTypeEnum.Shield => InventoryItemType.Shield,
                ItemObject.ItemTypeEnum.Bow => InventoryItemType.Weapon,
                ItemObject.ItemTypeEnum.Crossbow => InventoryItemType.Weapon,
                ItemObject.ItemTypeEnum.Thrown => InventoryItemType.Weapon,
                ItemObject.ItemTypeEnum.Goods => InventoryItemType.Goods,
                ItemObject.ItemTypeEnum.HeadArmor => InventoryItemType.HeadArmor,
                ItemObject.ItemTypeEnum.BodyArmor => InventoryItemType.BodyArmor,
                ItemObject.ItemTypeEnum.LegArmor => InventoryItemType.LegArmor,
                ItemObject.ItemTypeEnum.HandArmor => InventoryItemType.HandArmor,
                ItemObject.ItemTypeEnum.Pistol => InventoryItemType.Weapon,
                ItemObject.ItemTypeEnum.Musket => InventoryItemType.Weapon,
                ItemObject.ItemTypeEnum.Bullets => InventoryItemType.Weapon,
                ItemObject.ItemTypeEnum.Animal => InventoryItemType.Animal,
                ItemObject.ItemTypeEnum.Book => InventoryItemType.Book,
                ItemObject.ItemTypeEnum.Cape => InventoryItemType.Cape,
                ItemObject.ItemTypeEnum.HorseHarness => InventoryItemType.HorseHarness,
                ItemObject.ItemTypeEnum.Banner => InventoryItemType.Banner,
                _ => InventoryItemType.None
            };
        }

        // 私有字段
        private InventoryMode _currentMode; // 当前模式
        private InventoryLogic _inventoryLogic; // 库存逻辑
        private DoneLogicExtrasDelegate _doneLogicExtrasDelegate; // 完成回调
        // 单例实例
        private static readonly Lazy<SkillInventoryManager> _instance =
            new Lazy<SkillInventoryManager>(() => new SkillInventoryManager(Campaign.Current.InventoryManager));
        // 单例访问器
        public static SkillInventoryManager Instance => _instance.Value;
        // 私有构造函数（单例）
        private SkillInventoryManager(InventoryManager originalManager)
        {
            // 通过反射同步状态（原逻辑）
            SyncWithOriginalManager();
        }

        // 库存分类枚举
        public enum InventoryCategoryType
        {
            None = -1,
            All,
            Armors,
            Weapon,
            Shield,
            HorseCategory,
            Goods,
            CategoryTypeAmount
        }

        // 完成操作委托
        public delegate void DoneLogicExtrasDelegate();

        // 商队交易监听器
        private class CaravanInventoryListener : InventoryListener
        {
            private readonly MobileParty _caravan;

            public CaravanInventoryListener(MobileParty caravan)
            {
                _caravan = caravan;
            }

            public override int GetGold() => _caravan.PartyTradeGold;
            public override TextObject GetTraderName() => _caravan.LeaderHero?.Name ?? _caravan.Name;
            public override void SetGold(int gold) => _caravan.PartyTradeGold = gold;
            public override PartyBase GetOppositeParty() => _caravan.Party;
            public override void OnTransaction() => throw new NotImplementedException();
        }

        // 商人交易监听器
        private class MerchantInventoryListener : InventoryListener
        {
            private readonly SettlementComponent _component;

            public MerchantInventoryListener(SettlementComponent component)
            {
                _component = component;
            }

            public override TextObject GetTraderName() => _component.Owner.Name;
            public override PartyBase GetOppositeParty() => _component.Owner;
            public override int GetGold() => _component.Gold;
            public override void SetGold(int gold) => _component.ChangeGold(gold - _component.Gold);
            public override void OnTransaction() => throw new NotImplementedException();
        }
    }
}