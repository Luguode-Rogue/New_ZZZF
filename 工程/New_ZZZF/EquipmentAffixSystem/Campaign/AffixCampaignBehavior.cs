using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
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

        /// <summary>系统是否已初始化</summary>
        public bool IsInitialized;

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
                // 存档：将实例记录表 + 绑定表序列化为字符串列表
                _serializedAffixData = SerializeRecordMap();
                _serializedBindingData = SerializeBindingMap();
                _serializedIsInitialized = IsInitialized;
            }

            dataStore.SyncData<List<string>>("_serializedAffixData", ref _serializedAffixData);
            dataStore.SyncData<List<string>>("_serializedBindingData", ref _serializedBindingData);
            dataStore.SyncData<bool>("_serializedIsInitialized", ref _serializedIsInitialized);

            if (dataStore.IsSaving)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[装备词缀系统] 存档完成，共 {ItemRecordMap.Count} 件词缀物品，{BindingMap.Count} 条绑定"));
            }
            if (dataStore.IsLoading)
            {
                IsInitialized = _serializedIsInitialized;
                // 读档：反序列化字符串列表为实例记录表 + 绑定表
                ItemRecordMap = DeserializeRecordMap(_serializedAffixData);
                BindingMap = DeserializeBindingMap(_serializedBindingData);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[装备词缀系统] 读档完成，共 {ItemRecordMap.Count} 件词缀物品，{BindingMap.Count} 条绑定"));
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
            InformationManager.DisplayMessage(new InformationMessage(
                $"[装备词缀系统] 游戏加载完成，共 {ItemRecordMap.Count} 件词缀物品"));
        }

        /// <summary>
        /// 玩家物品交换事件（购买、拾取、掉落获得物品时触发）
        /// </summary>
        private void OnPlayerInventoryExchange(
            List<(ItemRosterElement, int)> purchasedItems,
            List<(ItemRosterElement, int)> soldItems,
            bool isTrading)
        {
            foreach (var (itemElement, amount) in purchasedItems)
            {
                if (itemElement.EquipmentElement.Item != null)
                {
                    // 为新获得的物品创建实例记录（生成词缀）
                    CreateAffixedRecord(itemElement.EquipmentElement.Item, amount, "PlayerInventory");
                }
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
        /// 搜索 ItemRecordMap 中首个 BaseItemId 匹配的记录，删除后重建。
        /// 返回新的词缀实例；物品上没有词缀时返回 null。
        /// </summary>
        public AffixInstance? RerollAffix(ItemObject item)
        {
            if (item == null) return null;

            // 搜索首个 BaseItemId 匹配的记录
            var oldRecord = ItemRecordMap.Values
                .FirstOrDefault(r => r.BaseItemId == item.StringId);
            if (oldRecord == null) return null;

            // 删除旧记录，创建新词缀
            ItemRecordMap.Remove(oldRecord.InstanceId);
            var newRecord = ForceAffixItem(item, "Reroll");
            return newRecord?.Affix;
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
    }
}
