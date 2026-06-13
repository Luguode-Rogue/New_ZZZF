using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace New_ZZZF
{
    /// <summary>
    /// 词缀系统战役行为。
    /// 职责：
    /// 1. 管理全部物品的 AffixInstance 字典（与存档同步）
    /// 2. 监听掉落/商店购买事件，触发词缀生成
    /// 3. 提供查询接口给 UI 层
    /// </summary>
    public class AffixCampaignBehavior : CampaignBehaviorBase
    {
        // ========== 单例访问 ==========
        public static AffixCampaignBehavior? Current { get; private set; }

        // ========== 存档数据 ==========

        /// <summary>
        /// 物品词缀字典。
        /// Key: AffixInstance.InstanceId（唯一标识）
        /// Value: 对应的词缀实例
        /// 不直接存档（Dictionary 泛型序列化需 TypeDefiner，改用 _serializedAffixData）。
        /// </summary>
        public Dictionary<string, AffixInstance> ItemAffixMap = new Dictionary<string, AffixInstance>();

        /// <summary>系统是否已初始化</summary>
        public bool IsInitialized;

        // ========== 存档数据（仅支持 Bannerlord 原生可序列化类型） ==========

        /// <summary>词缀字典的序列化形式：每行一个 AffixInstance 的文本表示</summary>
        [SaveableField(1)]
        private List<string> _serializedAffixData = new List<string>();

        /// <summary>IsInitialized 存档副本</summary>
        [SaveableField(2)]
        private bool _serializedIsInitialized;

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
                // 存档：将字典序列化为字符串列表
                _serializedAffixData = SerializeAffixMap();
                _serializedIsInitialized = IsInitialized;
            }

            dataStore.SyncData<List<string>>("_serializedAffixData", ref _serializedAffixData);
            dataStore.SyncData<bool>("_serializedIsInitialized", ref _serializedIsInitialized);

            if (dataStore.IsSaving)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[装备词缀系统] 存档完成，共 {ItemAffixMap.Count} 件词缀物品"));
            }
            if (dataStore.IsLoading)
            {
                IsInitialized = _serializedIsInitialized;
                // 读档：反序列化字符串列表为字典
                ItemAffixMap = DeserializeAffixMap(_serializedAffixData);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[装备词缀系统] 读档完成，共 {ItemAffixMap.Count} 件词缀物品"));
                // 读档后重建 [NonSerialized] 缓存
                var db = AffixDatabase.Instance;
                db.Initialize();
                foreach (var instanceId in ItemAffixMap.Keys.ToList())
                {
                    var instance = ItemAffixMap[instanceId];
                    instance.ResolveDefinitions(db);
                    ItemAffixMap[instanceId] = instance;
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
                $"[装备词缀系统] 游戏加载完成，共 {ItemAffixMap.Count} 件词缀物品"));
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
                    // 为新获得的物品生成词缀
                    AffixItemIfNeeded(itemElement.EquipmentElement.Item, "PlayerInventory");
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
        /// 检查物品是否需要生成词缀，如果需要则生成并存入字典。
        /// 返回该物品的词缀实例（有则返回，无则 null）。
        /// </summary>
        public AffixInstance? AffixItemIfNeeded(ItemObject item, string source)
        {
            if (item == null) return null;

            string itemId = item.StringId;

            // 检查是否已有词缀实例（通过 BaseItemId 匹配）
            foreach (var kv in ItemAffixMap)
            {
                if (kv.Value.BaseItemId == itemId)
                    return kv.Value; // 已生成过，直接返回
            }

            // 需要生成新词缀
            string itemType = ClassifyItemType(item);
            int itemLevel = CalculateItemLevel(item);

            AffixInstance instance = AffixGenerator.Generate(itemId, itemType, itemLevel);

            // 只有有词缀的才记录（普通物品不记录，节省存储空间）
            if (instance.HasAnyAffix)
            {
                ItemAffixMap[instance.InstanceId] = instance;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[装备词缀系统] [{source}] 生成词缀物品: {instance.BuildFullName(item.Name.ToString())} ({instance.Rarity})"));
                return instance;
            }

            return null;
        }

        /// <summary>
        /// 强制为物品生成词缀（Debug/测试用），跳过Normal判定，必定带词缀。
        /// </summary>
        public AffixInstance ForceAffixItem(ItemObject item, string source)
        {
            if (item == null) return null;

            string itemId = item.StringId;
            string itemType = ClassifyItemType(item);
            int itemLevel = CalculateItemLevel(item);
            int seed = (int)(DateTime.UtcNow.Ticks % int.MaxValue);

            AffixInstance instance = AffixGenerator.GenerateForceAffix(itemId, itemType, itemLevel, seed);

            ItemAffixMap[instance.InstanceId] = instance;
            InformationManager.DisplayMessage(new InformationMessage(
                $"[装备词缀系统] [{source}] 强制生成词缀: {instance.BuildFullName(item.Name.ToString())} ({instance.Rarity})"));
            return instance;
        }

        /// <summary>
        /// 重随物品词缀——生成全新前后缀替换旧的，保留同一物品。
        /// 返回新的词缀实例；物品上没有词缀时返回 null。
        /// </summary>
        public AffixInstance? RerollAffix(ItemObject item)
        {
            if (item == null) return null;

            var oldAffix = GetAffixByItemId(item.StringId);
            if (oldAffix == null) return null;

            // 删除旧记录，创建新词缀
            ItemAffixMap.Remove(oldAffix.InstanceId);
            return ForceAffixItem(item, "Reroll");
        }

        /// <summary>
        /// 根据 InstanceId 查找词缀实例
        /// </summary>
        public AffixInstance? GetAffixByInstanceId(string instanceId)
        {
            ItemAffixMap.TryGetValue(instanceId, out var instance);
            return instance;
        }

        /// <summary>
        /// 根据物品ID查找词缀实例（可能有多个，返回第一个匹配的）
        /// </summary>
        public AffixInstance? GetAffixByItemId(string itemId)
        {
            foreach (var kv in ItemAffixMap)
            {
                if (kv.Value.BaseItemId == itemId)
                    return kv.Value;
            }
            return null;
        }

        /// <summary>
        /// 获取物品的完整显示名称（含前后缀）
        /// </summary>
        public string GetItemDisplayName(ItemObject item)
        {
            var affix = GetAffixByItemId(item.StringId);
            if (affix != null)
                return affix.BuildFullName(item.Name.ToString());
            return item.Name.ToString();
        }

        /// <summary>
        /// 获取词缀对指定属性的伤害倍率（战斗用）。
        /// 返回 1.0 + 百分比加成，如 +15 伤害 → 1.15
        /// </summary>
        public static float GetAffixDamageMultiplier(ItemObject item, string statKey)
        {
            if (Current == null || item == null) return 1f;
            var affix = Current.GetAffixByItemId(item.StringId);
            if (affix == null || !affix.HasAnyAffix) return 1f;
            if (!affix.FinalStatModifiers.TryGetValue(statKey, out float bonus) || bonus == 0f)
                return 1f;
            return 1f + bonus * 0.01f;
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

        // ========== 序列化辅助（List<string> 格式存档，避免 Dictionary<自定义类型> 序列化问题） ==========

        /// <summary>将 ItemAffixMap 编码为字符串列表，每行格式：
        /// InstanceId|BaseItemId|ItemLevel|Rarity|prefixId,prefixId|suffixId,suffixId|statKey=statVal;statKey=statVal
        /// </summary>
        private List<string> SerializeAffixMap()
        {
            var result = new List<string>(ItemAffixMap.Count);
            foreach (var kv in ItemAffixMap)
            {
                var affix = kv.Value;
                string prefixStr = string.Join(",", affix.PrefixIds);
                string suffixStr = string.Join(",", affix.SuffixIds);
                string statStr = string.Join(";", System.Linq.Enumerable.Select(
                    affix.FinalStatModifiers, s => $"{s.Key}={s.Value}"));
                result.Add($"{affix.InstanceId}|{affix.BaseItemId}|{affix.ItemLevel}|{affix.Rarity}|{prefixStr}|{suffixStr}|{statStr}");
            }
            return result;
        }

        /// <summary>从字符串列表还原 ItemAffixMap</summary>
        private Dictionary<string, AffixInstance> DeserializeAffixMap(List<string> lines)
        {
            var result = new Dictionary<string, AffixInstance>();
            if (lines == null) return result;

            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split('|');
                if (parts.Length < 7) continue;

                var instance = new AffixInstance();
                instance.InstanceId = parts[0];
                instance.BaseItemId = parts[1];
                int.TryParse(parts[2], out int level);
                instance.ItemLevel = level;
                instance.Rarity = parts[3];

                // 前缀列表
                if (!string.IsNullOrEmpty(parts[4]))
                    instance.PrefixIds = new List<string>(parts[4].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                else
                    instance.PrefixIds = new List<string>();

                // 后缀列表
                if (!string.IsNullOrEmpty(parts[5]))
                    instance.SuffixIds = new List<string>(parts[5].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                else
                    instance.SuffixIds = new List<string>();

                // 属性修正
                instance.FinalStatModifiers = new Dictionary<string, float>();
                if (!string.IsNullOrEmpty(parts[6]))
                {
                    foreach (string pair in parts[6].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int eqIndex = pair.IndexOf('=');
                        if (eqIndex > 0 && eqIndex < pair.Length - 1
                            && float.TryParse(pair.Substring(eqIndex + 1), out float val))
                        {
                            instance.FinalStatModifiers[pair.Substring(0, eqIndex)] = val;
                        }
                    }
                }

                result[instance.InstanceId] = instance;
            }
            return result;
        }
    }
}
