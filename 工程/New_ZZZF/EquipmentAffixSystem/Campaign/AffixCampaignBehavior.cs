using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace New_ZZZF
{
    /// <summary>
    /// 词缀系统战役行为。
    /// 职责：
    /// 1. 管理全部物品的 AffixedItemRecord 实例表（与存档同步）
    /// 2. 监听掉落/商店购买事件，触发词缀生成
    /// 3. 提供查询接口给 UI 层和战斗层
    /// </summary>
    public class AffixCampaignBehavior : CampaignBehaviorBase
    {
        // ========== 单例访问 ==========
        public static AffixCampaignBehavior? Current { get; private set; }

        // ========== 实例记录表 ==========

        /// <summary>
        /// 物品实例记录表（替代旧 ItemAffixMap）。
        /// Key: AffixedItemRecord.InstanceId（唯一标识）
        /// Value: 物品实例记录（含基础物品ID、词缀、来源、堆叠数等）
        /// 不直接存档（Dictionary 泛型序列化需 TypeDefiner，改用 _serializedAffixData）。
        /// </summary>
        public Dictionary<string, AffixedItemRecord> ItemRecordMap = new Dictionary<string, AffixedItemRecord>();

        /// <summary>
        /// 运行时绑定表。记录"哪件词缀物品现在在谁手里、在哪个槽位"。
        /// Key: OwnerId:SlotIndex（如 "hero_123:4"）
        /// Value: 绑定记录
        /// </summary>
        public Dictionary<string, AffixBinding> BindingMap = new Dictionary<string, AffixBinding>();

        /// <summary>
        /// ItemModifier → InstanceId 映射表。
        /// 解决 UI 层无法从 EquipmentElement 获取 InstanceId 的问题。
        /// Key: ItemModifier.StringId（如 "affix_mod_abc123"）
        /// Value: InstanceId
        /// </summary>
        public Dictionary<string, string> ModifierToInstanceMap = new Dictionary<string, string>();

        /// <summary>系统是否已初始化</summary>
        public bool IsInitialized;

        /// <summary>防止 OnPlayerInventoryExchange 递归重入（设置 ItemModifier 可能触发二次事件）</summary>
        private bool _isProcessingInventory;

        // ========== 存档数据（仅支持 Bannerlord 原生可序列化类型） ==========

        /// <summary>词缀字典的序列化形式：每行一个 AffixInstance 的文本表示</summary>
        [SaveableField(1)]
        private List<string> _serializedAffixData = new List<string>();

        /// <summary>IsInitialized 存档副本</summary>
        [SaveableField(2)]
        private bool _serializedIsInitialized;

        /// <summary>绑定表的序列化形式</summary>
        [SaveableField(3)]
        private List<string> _serializedBindingData = new List<string>();

        /// <summary>ModifierToInstanceMap 的序列化形式</summary>
        [SaveableField(4)]
        private List<string> _serializedModifierData = new List<string>();

        public AffixCampaignBehavior()
        {
            Current = this;
        }

        // ========== CampaignBehaviorBase 必须实现 ==========

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(
                this, new Action<CampaignGameStarter>(OnNewGameCreated));

            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(
                this, new Action<CampaignGameStarter>(OnGameLoaded));

            // 监听物品获得事件（玩家从任何途径获得物品时触发）
            CampaignEvents.PlayerInventoryExchangeEvent.AddNonSerializedListener(
                this, new Action<List<(ItemRosterElement, int)>, List<(ItemRosterElement, int)>, bool>(
                    OnPlayerInventoryExchange));
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                // 存档：将实例记录表 + 绑定表 + 修饰符映射序列化为字符串列表
                _serializedAffixData = SerializeRecordMap();
                _serializedBindingData = SerializeBindingMap();
                _serializedModifierData = SerializeModifierMap();
                _serializedIsInitialized = IsInitialized;
            }

            dataStore.SyncData<List<string>>("_serializedAffixData", ref _serializedAffixData);
            dataStore.SyncData<List<string>>("_serializedBindingData", ref _serializedBindingData);
            dataStore.SyncData<List<string>>("_serializedModifierData", ref _serializedModifierData);
            dataStore.SyncData<bool>("_serializedIsInitialized", ref _serializedIsInitialized);

            if (dataStore.IsSaving)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[装备词缀系统] 存档完成，共 {ItemRecordMap.Count} 件词缀物品，{BindingMap.Count} 条绑定"));
            }
            if (dataStore.IsLoading)
            {
                IsInitialized = _serializedIsInitialized;
                // 读档：反序列化字符串列表为实例记录表 + 绑定表 + 修饰符映射
                ItemRecordMap = DeserializeRecordMap(_serializedAffixData);
                BindingMap = DeserializeBindingMap(_serializedBindingData);
                ModifierToInstanceMap = DeserializeModifierMap(_serializedModifierData);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[装备词缀系统] 读档完成，共 {ItemRecordMap.Count} 件词缀物品，{BindingMap.Count} 条绑定，{ModifierToInstanceMap.Count} 条修饰符映射"));
                // 读档后重建 [NonSerialized] 缓存
                var db = AffixDatabase.Instance;
                db.Initialize();
                foreach (var record in ItemRecordMap.Values)
                {
                    record.Affix.ResolveDefinitions(db);
                }
            }
        }

        // ========== 事件处理 ==========

        private void OnNewGameCreated(CampaignGameStarter campaignGameStarter)
        {
            Initialize();
            InformationManager.DisplayMessage(new InformationMessage(
                "[装备词缀系统] 新游戏创建，系统已初始化"));
        }

        private void OnGameLoaded(CampaignGameStarter campaignGameStarter)
        {
            Initialize();
            // 重建存档中的定制 ItemModifier 对象（注册到 MBObjectManager）
            RebuildModifiers();
            // 将既有记录同步到玩家实际背包元素（修复旧存档中未设置 Modifier 的物品）
            SyncAffixModifiersToPlayerRoster();
            // 同步 Hero 装备槽绑定
            SyncHeroEquipmentBindings();
            InformationManager.DisplayMessage(new InformationMessage(
                $"[装备词缀系统] 游戏加载完成，共 {ItemRecordMap.Count} 件词缀物品，{ModifierToInstanceMap.Count} 条修饰符映射"));
        }

        /// <summary>
        /// 玩家物品交换事件（购买、拾取、掉落获得物品时触发）。
        /// 注意：purchasedItems 中的 ItemRosterElement 是交易系统创建的临时副本，
        /// 修改副本不会影响玩家实际背包。必须直接在玩家 ItemRoster 上设置 Modifier。
        /// </summary>
        private void OnPlayerInventoryExchange(
            List<(ItemRosterElement, int)> purchasedItems,
            List<(ItemRosterElement, int)> soldItems,
            bool isTrading)
        {
            // 防止递归重入：SyncAffixModifiersToPlayerRoster 中设置 ItemModifier 可能触发二次事件
            if (_isProcessingInventory) return;
            _isProcessingInventory = true;
            try
            {
                // 1. 为每批新物品创建词缀记录（记录插入 ItemRecordMap）
                foreach (var (itemElement, amount) in purchasedItems)
                {
                    if (itemElement.EquipmentElement.Item == null)
                        continue;

                    // 跳过已由调试工具预先处理过的物品（已带 Modifier 且已注册映射）
                    if (itemElement.EquipmentElement.ItemModifier != null)
                    {
                        string existingModId = itemElement.EquipmentElement.ItemModifier.StringId;
                        if (!string.IsNullOrEmpty(existingModId)
                            && ModifierToInstanceMap.ContainsKey(existingModId))
                        {
                            continue; // 已处理，跳过重复创建
                        }
                    }

                    CreateAffixedRecord(itemElement.EquipmentElement.Item, amount, "PlayerInventory");
                }

                // 2. 将记录中的修饰符同步到玩家实际背包元素
                SyncAffixModifiersToPlayerRoster();

                // 3. 同步 Hero 装备槽 → InstanceId 绑定
                SyncHeroEquipmentBindings();
            }
            finally
            {
                _isProcessingInventory = false;
            }
        }

        // ========== 核心方法 ==========

        private void Initialize()
        {
            if (IsInitialized) return;

            // 初始化词缀数据库
            AffixDatabase.Instance.Initialize();

            IsInitialized = true;
        }

        /// <summary>
        /// 为物品创建实例记录并生成词缀（替代旧 AffixItemIfNeeded）。
        /// 返回 AffixedItemRecord（包含 InstanceId + 词缀）；无词缀时返回 null。
        /// </summary>
        public AffixedItemRecord? CreateAffixedRecord(ItemObject item, int stackCount = 1, string source = "")
        {
            if (item == null) return null;

            // 不检查是否已有同 BaseItemId 记录 —— 每次调用都创建新实例
            string itemType = ClassifyItemType(item);
            int itemLevel = CalculateItemLevel(item);

            var record = new AffixedItemRecord
            {
                BaseItemId = item.StringId,
                StackCount = stackCount,
                Source = source
            };

            int seed = record.InstanceId.GetHashCode();
            record.Affix = AffixGenerator.GenerateSeeded(item.StringId, itemType, itemLevel, seed);

            if (record.Affix.HasAnyAffix)
            {
                ItemRecordMap[record.InstanceId] = record;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[装备词缀系统] [{source}] 生成词缀物品: {record.Affix.BuildFullName(item.Name.ToString())} ({record.Affix.Rarity})"));
                return record;
            }

            return null;
        }

        /// <summary>
        /// 强制为物品生成词缀（Debug/测试用），跳过Normal判定，必定带词缀。
        /// 返回 AffixedItemRecord。
        /// </summary>
        public AffixedItemRecord ForceAffixItem(ItemObject item, string source)
        {
            if (item == null) return null;

            string itemId = item.StringId;
            string itemType = ClassifyItemType(item);
            int itemLevel = CalculateItemLevel(item);

            var record = new AffixedItemRecord
            {
                BaseItemId = itemId,
                StackCount = 1,
                Source = source
            };

            int seed = record.InstanceId.GetHashCode();
            record.Affix = AffixGenerator.GenerateForceAffix(itemId, itemType, itemLevel, seed);

            ItemRecordMap[record.InstanceId] = record;
            InformationManager.DisplayMessage(new InformationMessage(
                $"[装备词缀系统] [{source}] 强制生成词缀: {record.Affix.BuildFullName(item.Name.ToString())} ({record.Affix.Rarity})"));
            return record;
        }

        /// <summary>
        /// 重随物品词缀——生成全新前后缀替换旧的。
        /// 按 InstanceId 精确定位记录，删除后重建同底材新词缀。
        /// 同时更新背包中的实际元素（旧 Modifier → 新 Modifier）。
        /// 返回新的词缀实例；InstanceId 不存在时返回 null。
        /// </summary>
        public AffixInstance? RerollAffix(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;

            // 按 InstanceId 精确定位
            if (!ItemRecordMap.TryGetValue(instanceId, out var oldRecord))
                return null;

            // 获取底材物品
            ItemObject? item = MBObjectManager.Instance.GetObject<ItemObject>(oldRecord.BaseItemId);
            if (item == null) return null;

            string oldModifierId = "zzzf_affix_" + instanceId;

            // 1. 创建新词缀记录（新 InstanceId、新 ModifierId）
            var newRecord = ForceAffixItem(item, "Reroll");
            if (newRecord == null) return null;
            string newModifierId = "zzzf_affix_" + newRecord.InstanceId;

            // 2. 删除旧记录 + 旧 Modifier 映射
            ItemRecordMap.Remove(instanceId);
            ModifierToInstanceMap.Remove(oldModifierId);

            // 3. 同步背包元素：移除带旧 Modifier 的元素，添加带新 Modifier 的元素
            ReplaceModifierInPlayerRoster(item, oldModifierId, newModifierId, newRecord.InstanceId);

            return newRecord.Affix;
        }

        /// <summary>
        /// 在玩家背包中查找带有指定旧 Modifier 的装备元素，
        /// 用带新 Modifier 的等效元素替换它（保持数量不变）。
        /// </summary>
        private void ReplaceModifierInPlayerRoster(
            ItemObject item, string oldModifierId, string newModifierId, string newInstanceId)
        {
            try
            {
                var roster = MobileParty.MainParty?.ItemRoster;
                if (roster == null) return;

                var oldModifier = MBObjectManager.Instance.GetObject<ItemModifier>(oldModifierId);
                var newModifier = CreateOrGetItemModifier(newModifierId, newInstanceId);
                if (newModifier == null) return;

                for (int i = 0; i < roster.Count; i++)
                {
                    var element = roster.GetElementCopyAtIndex(i);
                    if (element.EquipmentElement.Item != item) continue;
                    if (oldModifier != null
                        && element.EquipmentElement.ItemModifier != oldModifier) continue;
                    // 如果是 null modifier（Modifier 已丢失但 instanceId 匹配），也替换

                    int amount = element.Amount;
                    roster.Remove(element);
                    roster.AddToCounts(new EquipmentElement(item, newModifier), amount);
                    break; // 只替换第一个匹配项
                }
            }
            catch (Exception ex)
            {
                // 不在此阶段崩溃
            }
        }

        /// <summary>
        /// 根据 InstanceId 查找词缀实例
        /// </summary>
        public AffixInstance? GetAffixByInstanceId(string instanceId)
        {
            if (ItemRecordMap.TryGetValue(instanceId, out var record))
                return record.Affix;
            return null;
        }

        /// <summary>
        /// 过渡方法：根据 BaseItemId 搜索首个匹配实例记录的词缀。
        /// 仅用于仅有 ItemObject 无法获取 InstanceId 的场景（UI/战斗/调试）。
        /// 应逐步迁移到按 InstanceId 查询。
        /// </summary>
        [Obsolete("使用 GetAffixByInstanceId 替代。此方法为模板级回退，无法区分同模板不同实例。")]
        public AffixInstance? GetAffixByBaseItemId(string baseItemId)
        {
            foreach (var record in ItemRecordMap.Values)
            {
                if (record.BaseItemId == baseItemId)
                    return record.Affix;
            }
            return null;
        }

        // ========== 实例绑定（装备槽 → InstanceId） ==========

        /// <summary>
        /// 将物品实例绑定到角色的装备槽。
        /// 应在角色装备物品时调用。
        /// </summary>
        public void BindEquipment(Hero hero, EquipmentIndex slotIndex, string instanceId)
        {
            if (hero == null) return;
            string key = $"{hero.StringId}:{(int)slotIndex}";
            BindingMap[key] = new AffixBinding
            {
                InstanceId = instanceId,
                OwnerType = AffixOwnerType.Equipment,
                OwnerId = hero.StringId,
                SlotIndex = (int)slotIndex
            };

            if (ItemRecordMap.TryGetValue(instanceId, out var record))
                record.IsEquipped = true;
        }

        /// <summary>
        /// 将物品实例从角色的装备槽解绑。
        /// 应在角色卸下装备时调用。
        /// </summary>
        public void UnbindEquipment(Hero hero, EquipmentIndex slotIndex)
        {
            if (hero == null) return;
            string key = $"{hero.StringId}:{(int)slotIndex}";
            if (BindingMap.TryGetValue(key, out var binding))
            {
                if (ItemRecordMap.TryGetValue(binding.InstanceId, out var record))
                    record.IsEquipped = false;
                BindingMap.Remove(key);
            }
        }

        /// <summary>
        /// 根据角色和装备槽索引，获取已绑定的 InstanceId。
        /// 未绑定时返回 null。
        /// </summary>
        public string? GetEquippedInstanceId(Hero hero, EquipmentIndex slotIndex)
        {
            if (hero == null) return null;
            string key = $"{hero.StringId}:{(int)slotIndex}";
            if (BindingMap.TryGetValue(key, out var binding))
                return binding.InstanceId;
            return null;
        }

        /// <summary>
        /// 同步 Hero 当前装备槽 → InstanceId 绑定表。
        /// 读取 Hero.BattleEquipment 和 CivilianEquipment 中带 ItemModifier 的槽位，
        /// 通过 ModifierToInstanceMap 反查 InstanceId，更新 BindingMap。
        ///
        /// 调用时机：OnPlayerInventoryExchange（完成交换后）、OnGameLoaded（读档后）
        /// </summary>
        public void SyncHeroEquipmentBindings()
        {
            if (Hero.MainHero == null) return;

            // 收集需要同步的英雄列表：玩家 + 队伍中所有同伴
            var heroesToSync = new List<Hero> { Hero.MainHero };
            if (MobileParty.MainParty != null)
            {
                foreach (var member in MobileParty.MainParty.MemberRoster
                    .GetTroopRoster().Where(t => t.Character?.IsHero == true))
                {
                    var hero = member.Character?.HeroObject;
                    if (hero != null && hero != Hero.MainHero)
                        heroesToSync.Add(hero);
                }
            }

            foreach (var hero in heroesToSync)
            {
                // 同步 BattleEquipment
                SyncEquipmentToBindings(hero, hero.BattleEquipment, AffixOwnerType.Equipment);
                // 同步 CivilianEquipment
                SyncEquipmentToBindings(hero, hero.CivilianEquipment, AffixOwnerType.Equipment);
            }
        }

        /// <summary>
        /// 扫描 Hero 的单个 Equipment，将带 Modifier 的物品槽位更新到 BindingMap。
        /// 无 Modifier 的旧绑定会被清理。
        /// </summary>
        private void SyncEquipmentToBindings(Hero hero, Equipment equipment, AffixOwnerType ownerType)
        {
            if (hero == null || equipment == null) return;

            for (int i = 0; i <= (int)EquipmentIndex.HorseHarness; i++)
            {
                var slot = (EquipmentIndex)i;
                var element = equipment[slot];
                string key = $"{hero.StringId}:{(int)slot}";

                if (element.IsEmpty || element.Item == null || element.ItemModifier == null)
                {
                    // 槽位空或无语缀修饰符 → 清除旧绑定
                    if (BindingMap.TryGetValue(key, out var oldBinding))
                    {
                        if (ItemRecordMap.TryGetValue(oldBinding.InstanceId, out var record))
                            record.IsEquipped = false;
                        BindingMap.Remove(key);
                    }
                    continue;
                }

                // 有 Modifier → 尝试查 InstanceId
                string modifierId = element.ItemModifier.StringId;
                if (string.IsNullOrEmpty(modifierId)) continue;

                if (!ModifierToInstanceMap.TryGetValue(modifierId, out string? instanceId)
                    || string.IsNullOrEmpty(instanceId))
                    continue;

                // 检查是否需要更新
                if (BindingMap.TryGetValue(key, out var existing)
                    && existing.InstanceId == instanceId)
                    continue; // 已是最新，跳过

                // 清除旧绑定
                if (BindingMap.TryGetValue(key, out var toRemove))
                {
                    if (ItemRecordMap.TryGetValue(toRemove.InstanceId, out var oldRecord))
                        oldRecord.IsEquipped = false;
                }

                // 设置新绑定
                BindingMap[key] = new AffixBinding
                {
                    InstanceId = instanceId,
                    OwnerType = ownerType,
                    OwnerId = hero.StringId,
                    SlotIndex = (int)slot
                };

                if (ItemRecordMap.TryGetValue(instanceId, out var newRecord))
                    newRecord.IsEquipped = true;
            }
        }

        // ========== Modifier 桥接（UI层实例区分） ==========

        /// <summary>
        /// 注册 ItemModifier → InstanceId 映射。
        /// 应在词缀物品生成/拾取时调用，使 UI 层能通过 EquipmentElement.ItemModifier 反查实例。
        /// </summary>
        public void RegisterModifierBinding(string modifierStringId, string instanceId)
        {
            if (string.IsNullOrEmpty(modifierStringId) || string.IsNullOrEmpty(instanceId)) return;
            ModifierToInstanceMap[modifierStringId] = instanceId;
        }

        /// <summary>
        /// 为词缀物品实例创建唯一 ItemModifier 并注册到 MBObjectManager。
        /// 核心目的：使同底材不同词缀的物品在背包中独立显示、不会堆叠。
        /// 同时注册 modifierId → instanceId 映射供 UI 层精确查找。
        /// 返回创建的（或已存在的）ItemModifier。
        /// </summary>
        public ItemModifier CreateOrGetItemModifier(string modifierId, string instanceId)
        {
            // 1. 注册映射（UI 层通过 ItemModifier.StringId 反查实例）
            RegisterModifierBinding(modifierId, instanceId);

            // 2. 创建/获取唯一 ItemModifier
            ItemModifier? modifier = MBObjectManager.Instance.GetObject<ItemModifier>(modifierId);
            if (modifier == null)
            {
                modifier = new ItemModifier();
                modifier.StringId = modifierId;
                modifier.Initialize();
                // ItemModifier.PriceMultiplier 为 private set，需反射设为 1.0 维持原价
                typeof(ItemModifier).GetProperty("PriceMultiplier")?.SetValue(modifier, 1.0f);
                // Name 必须包含 {ITEMNAME} 占位符，否则 GetModifiedItemName() 返回空字符串导致物品名字消失
                var nameTO = new TextObject("{=zzzf_affix_mod}{ITEMNAME}", null);
                typeof(ItemModifier).GetProperty("Name")?.SetValue(modifier, nameTO);
                LogDebug($"[CreateOrGetItemModifier] modifierId={modifierId}, Name.ToString()='{modifier.Name?.ToString()}'");
                // 注册到 MBObjectManager：presumed=true 保证存档/读档可正确序列化
                modifier = MBObjectManager.Instance.RegisterPresumedObject<ItemModifier>(modifier);
                LogDebug($"[CreateOrGetItemModifier] After RegisterPresumedObject, Name.ToString()='{modifier.Name?.ToString()}'");
            }

            return modifier;
        }

        /// <summary>
        /// 读档后重建所有定制 ItemModifier 并注册到 MBObjectManager。
        /// 遍历 ModifierToInstanceMap 为每个映射创建 ItemModifier。
        /// 已存在则跳过（游戏读档时已自动注册）。
        /// </summary>
        private void RebuildModifiers()
        {
            foreach (var kv in ModifierToInstanceMap)
            {
                string modifierId = kv.Key;
                if (MBObjectManager.Instance.GetObject<ItemModifier>(modifierId) != null)
                    continue;

                var modifier = new ItemModifier();
                modifier.StringId = modifierId;
                modifier.Initialize();
                typeof(ItemModifier).GetProperty("PriceMultiplier")?.SetValue(modifier, 1.0f);
                // Name 必须包含 {ITEMNAME} 占位符，否则 GetModifiedItemName() 返回空字符串导致物品名字消失
                var nameTO = new TextObject("{=zzzf_affix_mod}{ITEMNAME}", null);
                typeof(ItemModifier).GetProperty("Name")?.SetValue(modifier, nameTO);
                LogDebug($"[RebuildModifiers] modifierId={modifierId}, Name.ToString()='{modifier.Name?.ToString()}'");
                modifier = MBObjectManager.Instance.RegisterPresumedObject<ItemModifier>(modifier);
                LogDebug($"[RebuildModifiers] After RegisterPresumedObject, Name.ToString()='{modifier.Name?.ToString()}'");
            }
        }

        /// <summary>
        /// 将 ItemRecordMap 中已创建但尚未分配修饰符的词缀记录，
        /// 同步到玩家实际背包中对应的 ItemRosterElement 上。
        ///
        /// 关键：ItemRosterElement 是 struct 值类型，roster[i] 返回副本。
        /// 必须通过 Remove + AddToCounts(EquipmentElement(item, modifier)) 模式
        /// 才能真正设置修饰符。
        ///
        /// 为什么需要这个方法：
        /// PlayerInventoryExchangeEvent 的 purchasedItems 是交易系统创建的
        /// 临时 ItemRosterElement 副本（见 InventoryLogic.GetTransferredItems），
        /// 修改副本不会影响玩家实际背包。必须直接操作 MobileParty.MainParty.ItemRoster。
        /// </summary>
        public void SyncAffixModifiersToPlayerRoster()
        {
            try
            {
                if (MobileParty.MainParty == null) return;
                var roster = MobileParty.MainParty.ItemRoster;
                if (roster == null) return;

                // 第一步：收集需要设置修饰符的元素（不能边遍历边修改 ItemRoster）
                var pendingAssignments = new List<(int Index, EquipmentElement NewElement, int Amount)>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var element = roster.GetElementCopyAtIndex(i);
                    if (element.EquipmentElement.Item == null || element.EquipmentElement.ItemModifier != null)
                        continue;

                    string baseItemId = element.EquipmentElement.Item.StringId;

                    // 找一条 BaseItemId 匹配 且 尚未分配修饰符的记录
                    foreach (var record in ItemRecordMap.Values)
                    {
                        if (record.BaseItemId == baseItemId
                            && !ModifierToInstanceMap.ContainsValue(record.InstanceId))
                        {
                            string modifierId = "zzzf_affix_" + record.InstanceId;
                            var modifier = CreateOrGetItemModifier(modifierId, record.InstanceId);
                            if (modifier != null)
                            {
                                pendingAssignments.Add((
                                    i,
                                    new EquipmentElement(element.EquipmentElement.Item, modifier),
                                    element.Amount
                                ));
                            }
                            break;
                        }
                    }
                }

                // 第二步：倒序应用修改（避免索引因 Remove 而变化）
                for (int j = pendingAssignments.Count - 1; j >= 0; j--)
                {
                    var (index, newElement, amount) = pendingAssignments[j];
                    var oldElement = roster.GetElementCopyAtIndex(index);
                    // 移除旧的（无 Modifier 的）堆叠
                    roster.Remove(oldElement);
                    // 添加新的（带 Modifier 的）堆叠
                    roster.AddToCounts(newElement, amount);
                }
            }
            catch (Exception ex)
            {
                // 不在此阶段崩溃
            }
        }

        /// <summary>
        /// 根据 EquipmentElement 获取词缀实例（仅精确实例查找）。
        /// 通过 ItemModifier.StringId → ModifierToInstanceMap → GetAffixByInstanceId 精确匹配。
        /// 非 zzzf 词缀或无修饰符返回 null，不做模板回退以免污染原版物品显示。
        /// </summary>
        public AffixInstance? GetAffixForEquipmentElement(EquipmentElement element)
        {
            if (element.Item == null) return null;

            // 优先级1：ItemModifier 桥接 → 精确实例查找
            if (element.ItemModifier != null)
            {
                string modifierId = element.ItemModifier.StringId;
                if (!string.IsNullOrEmpty(modifierId)
                    && ModifierToInstanceMap.TryGetValue(modifierId, out string? instId)
                    && !string.IsNullOrEmpty(instId))
                {
                    var affix = GetAffixByInstanceId(instId);
                    if (affix != null) return affix;
                }
            }

            // 非 zzzf 词缀或无修饰符 → 不降级，避免用模板查找污染原版物品名称
            return null;
        }

        // ========== 显示名称查询 ==========

        /// <summary>
        /// 获取物品的完整显示名称（含前后缀）—— 模板回退版本。
        /// 仅用于仅有 ItemObject 无 InstanceId 的过渡场景。
        /// </summary>
        [Obsolete("使用 GetItemDisplayName(string instanceId) 替代。")]
        public string GetItemDisplayName(ItemObject item)
        {
            var affix = GetAffixByBaseItemId(item.StringId);
            if (affix != null)
                return affix.BuildFullName(item.Name.ToString());
            return item.Name.ToString();
        }

        /// <summary>
        /// 按实例ID获取完整显示名称（含前后缀）。
        /// 实例优先路径。
        /// </summary>
        public string GetItemDisplayName(string instanceId)
        {
            if (ItemRecordMap.TryGetValue(instanceId, out var record))
            {
                ItemObject? item = MBObjectManager.Instance.GetObject<ItemObject>(record.BaseItemId);
                if (item != null)
                {
                    return record.Affix.HasAnyAffix
                        ? record.Affix.BuildFullName(item.Name.ToString())
                        : item.Name.ToString();
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取词缀对指定属性的伤害倍率（战斗用）—— 模板回退版本。
        /// 返回 1.0 + 百分比加成，如 +15 伤害 → 1.15
        /// </summary>
        [Obsolete("使用 GetAffixDamageMultiplier(string?, ItemObject, string) 替代。")]
        public static float GetAffixDamageMultiplier(ItemObject item, string statKey)
        {
            if (Current == null || item == null) return 1f;
            var affix = Current.GetAffixByBaseItemId(item.StringId);
            if (affix == null || !affix.HasAnyAffix) return 1f;
            if (!affix.FinalStatModifiers.TryGetValue(statKey, out float bonus) || bonus == 0f)
                return 1f;
            return 1f + bonus * 0.01f;
        }

        /// <summary>
        /// 获取词缀对指定属性的伤害倍率（实例优先）。
        /// instanceId 非空时走实例路径，为空或未找到时回退到模板查找。
        /// </summary>
        public static float GetAffixDamageMultiplier(string? instanceId, ItemObject item, string statKey)
        {
            if (Current == null || item == null) return 1f;

            if (!string.IsNullOrEmpty(instanceId))
            {
                var affix = Current.GetAffixByInstanceId(instanceId);
                if (affix != null && affix.HasAnyAffix)
                {
                    if (!affix.FinalStatModifiers.TryGetValue(statKey, out float bonus) || bonus == 0f)
                        return 1f;
                    return 1f + bonus * 0.01f;
                }
            }

            // 回退到模板查找
#pragma warning disable CS0618 // 过渡回退
            return GetAffixDamageMultiplier(item, statKey);
#pragma warning restore CS0618
        }

        /// <summary>
        /// 获取物品的稀有度颜色（用于 UI 显示）
        /// </summary>
        public static uint GetRarityColor(string rarity)
        {
            return rarity switch
            {
                "Normal" => 0xFFFFFFFF,    // 白色
                "Magic" => 0xFF4169FF,     // 蓝色
                "Rare" => 0xFFFFFF00,      // 黄色
                "Unique" => 0xFFDAA520,    // 暗金色
                _ => 0xFFFFFFFF
            };
        }

        // ========== 辅助方法 ==========

        /// <summary>将 ItemObject 分类为词缀系统使用的类型字符串</summary>
        private static string ClassifyItemType(ItemObject item)
        {
            if (item == null) return "Unknown";

            var weapon = item.WeaponComponent;
            var armor = item.ArmorComponent;
            var horse = item.HorseComponent;

            if (weapon != null)
            {
                var desc = weapon.GetItemType();
                // WeaponClass: 0=Undefined,1=Dagger,2=OneHandedSword,3=TwoHandedSword,4=OneHandedAxe,5=TwoHandedAxe,
                //              6=Mace,7=Pick,8=TwoHandedMace,9=OneHandedPolearm,10=TwoHandedPolearm,11=LowGripPolearm,
                //              12=Arrow,13=Bolt,14=Cartridge,15=Bow,16=Crossbow,17=Stone,18=Boulder,19=ThrowingAxe,
                //              20=ThrowingKnife,21=Javelin,22=Pistol,23=Musket
                var wc = weapon.PrimaryWeapon.WeaponClass;
                if (wc == WeaponClass.Bow) return "Bow";
                if (wc == WeaponClass.Crossbow) return "Crossbow";
                if (wc == WeaponClass.Arrow || wc == WeaponClass.Bolt) return "Ammo";
                if (wc == WeaponClass.Dagger) return "Dagger";
                if (wc == WeaponClass.Mace || wc == WeaponClass.TwoHandedMace) return "Mace";
                if (wc == WeaponClass.OneHandedPolearm || wc == WeaponClass.TwoHandedPolearm || wc == WeaponClass.LowGripPolearm) return "Polearm";
                if (wc == WeaponClass.TwoHandedSword || wc == WeaponClass.TwoHandedAxe) return "TwoHanded";
                if (wc == WeaponClass.Javelin || wc == WeaponClass.ThrowingAxe || wc == WeaponClass.ThrowingKnife) return "Thrown";
                return "OneHanded"; // 默认单手武器
            }

            if (armor != null)
            {
                ItemObject.ItemTypeEnum itemType = item.ItemType;
                if (itemType == ItemObject.ItemTypeEnum.HeadArmor) return "HeadArmor";
                if (itemType == ItemObject.ItemTypeEnum.Cape) return "Cape";
                if (itemType == ItemObject.ItemTypeEnum.BodyArmor) return "BodyArmor";
                if (itemType == ItemObject.ItemTypeEnum.HandArmor) return "HandArmor";
                if (itemType == ItemObject.ItemTypeEnum.LegArmor) return "LegArmor";
                return "BodyArmor";
            }

            if (horse != null)
            {
                ItemObject.ItemTypeEnum itemType = item.ItemType;
                if (itemType == ItemObject.ItemTypeEnum.Horse) return "Horse";
                if (itemType == ItemObject.ItemTypeEnum.HorseHarness) return "HorseHarness";
                return "Horse";
            }

            if (item.ItemType == ItemObject.ItemTypeEnum.Shield) return "Shield";

            return "Misc";
        }

        /// <summary>根据物品的 Tier/Value 估算物品等级</summary>
        private static int CalculateItemLevel(ItemObject item)
        {
            if (item == null) return 1;

            // 等级基于物品价值粗略估算
            int value = item.Value;
            if (value < 200) return 1;
            if (value < 500) return 5;
            if (value < 1000) return 10;
            if (value < 2000) return 15;
            if (value < 4000) return 20;
            if (value < 7000) return 25;
            if (value < 10000) return 30;
            if (value < 15000) return 35;
            if (value < 20000) return 40;
            if (value < 35000) return 50;
            if (value < 50000) return 60;
            if (value < 75000) return 70;
            return 80;
        }

        // ========== 序列化辅助 ==========

        /// <summary>将 ItemRecordMap 编码为字符串列表，每行格式：
        /// InstanceId|BaseItemId|ItemLevel|Rarity|Source|StackCount|IsEquipped|prefixId,prefixId|suffixId,suffixId|statKey=statVal;statKey=statVal
        /// </summary>
        private List<string> SerializeRecordMap()
        {
            var result = new List<string>(ItemRecordMap.Count);
            foreach (var record in ItemRecordMap.Values)
            {
                var affix = record.Affix;
                string prefixStr = string.Join(",", affix.PrefixIds);
                string suffixStr = string.Join(",", affix.SuffixIds);
                string statStr = string.Join(";", System.Linq.Enumerable.Select(
                    affix.FinalStatModifiers, s => $"{s.Key}={s.Value}"));
                result.Add($"{affix.InstanceId}|{affix.BaseItemId}|{affix.ItemLevel}|{affix.Rarity}|{record.Source}|{record.StackCount}|{record.IsEquipped}|{prefixStr}|{suffixStr}|{statStr}");
            }
            return result;
        }

        /// <summary>从字符串列表还原 ItemRecordMap</summary>
        private Dictionary<string, AffixedItemRecord> DeserializeRecordMap(List<string> lines)
        {
            var result = new Dictionary<string, AffixedItemRecord>();
            if (lines == null) return result;

            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split('|');
                // v2 格式：10 个字段 (含 Source/StackCount/IsEquipped)
                // v1 兼容：7 个字段 (旧格式仅有 AffixInstance 数据)
                bool isV2Format = parts.Length >= 10;

                var affix = new AffixInstance();
                affix.InstanceId = parts[0];
                affix.BaseItemId = parts[1];
                int.TryParse(parts[2], out int level);
                affix.ItemLevel = level;
                affix.Rarity = parts[3];

                int sourceIndex = 4;
                string source = "", stackStr = "", isEquippedStr = "";
                int prefixIndex, suffixIndex, statIndex;

                if (isV2Format)
                {
                    source = parts[4];      // Source
                    stackStr = parts[5];    // StackCount
                    isEquippedStr = parts[6]; // IsEquipped
                    prefixIndex = 7;
                    suffixIndex = 8;
                    statIndex = 9;
                }
                else
                {
                    prefixIndex = 4;
                    suffixIndex = 5;
                    statIndex = 6;
                }

                // 前缀列表
                if (prefixIndex < parts.Length && !string.IsNullOrEmpty(parts[prefixIndex]))
                    affix.PrefixIds = new List<string>(parts[prefixIndex].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                else
                    affix.PrefixIds = new List<string>();

                // 后缀列表
                if (suffixIndex < parts.Length && !string.IsNullOrEmpty(parts[suffixIndex]))
                    affix.SuffixIds = new List<string>(parts[suffixIndex].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                else
                    affix.SuffixIds = new List<string>();

                // 属性修正
                affix.FinalStatModifiers = new Dictionary<string, float>();
                if (statIndex < parts.Length && !string.IsNullOrEmpty(parts[statIndex]))
                {
                    foreach (string pair in parts[statIndex].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int eqIndex = pair.IndexOf('=');
                        if (eqIndex > 0 && eqIndex < pair.Length - 1
                            && float.TryParse(pair.Substring(eqIndex + 1), out float val))
                        {
                            affix.FinalStatModifiers[pair.Substring(0, eqIndex)] = val;
                        }
                    }
                }

                var record = new AffixedItemRecord
                {
                    InstanceId = affix.InstanceId,
                    BaseItemId = affix.BaseItemId,
                    Source = isV2Format ? source : "",
                    StackCount = isV2Format && int.TryParse(stackStr, out int sc) ? sc : 1,
                    IsEquipped = isV2Format && bool.TryParse(isEquippedStr, out bool ie) && ie,
                    Affix = affix
                };

                result[record.InstanceId] = record;
            }
            return result;
        }

        // ========== BindingMap 序列化辅助 ==========

        /// <summary>将 BindingMap 编码为字符串列表，每行格式：InstanceId|OwnerType|OwnerId|SlotIndex</summary>
        private List<string> SerializeBindingMap()
        {
            var result = new List<string>(BindingMap.Count);
            foreach (var binding in BindingMap.Values)
            {
                result.Add($"{binding.InstanceId}|{(int)binding.OwnerType}|{binding.OwnerId}|{binding.SlotIndex}");
            }
            return result;
        }

        /// <summary>从字符串列表还原 BindingMap</summary>
        private Dictionary<string, AffixBinding> DeserializeBindingMap(List<string> lines)
        {
            var result = new Dictionary<string, AffixBinding>();
            if (lines == null) return result;

            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split('|');
                if (parts.Length < 4) continue;

                var binding = new AffixBinding
                {
                    InstanceId = parts[0],
                    OwnerType = (AffixOwnerType)(int.TryParse(parts[1], out int ot) ? ot : 0),
                    OwnerId = parts[2],
                    SlotIndex = int.TryParse(parts[3], out int si) ? si : 0
                };

                // Key 格式：OwnerId:SlotIndex
                string key = $"{binding.OwnerId}:{binding.SlotIndex}";
                result[key] = binding;
            }
            return result;
        }

        /// <summary>将 ModifierToInstanceMap 编码为字符串列表，每行：modifierId|instanceId</summary>
        private List<string> SerializeModifierMap()
        {
            var result = new List<string>(ModifierToInstanceMap.Count);
            foreach (var kv in ModifierToInstanceMap)
                result.Add($"{kv.Key}|{kv.Value}");
            return result;
        }

        /// <summary>从字符串列表还原 ModifierToInstanceMap</summary>
        private Dictionary<string, string> DeserializeModifierMap(List<string> lines)
        {
            var result = new Dictionary<string, string>();
            if (lines == null) return result;
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split('|');
                if (parts.Length >= 2)
                    result[parts[0]] = parts[1];
            }
            return result;
        }

        // ========== 调试日志 ==========
        private static readonly string DebugLogPath =
            @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\New_ZZZF\工程\affix_debug.log";

        private static void LogDebug(string msg)
        {
            try
            {
                System.IO.File.AppendAllText(DebugLogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
