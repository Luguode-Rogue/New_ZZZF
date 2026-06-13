using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace New_ZZZF
{
    /// <summary>
    /// 词缀数据库（单例）。
    /// 管理所有词缀定义，提供查询和注册接口。
    /// 预设了暗黑2风格的词缀池。
    /// </summary>
    public sealed class AffixDatabase
    {
        // ========== 单例 ==========
        private static readonly AffixDatabase _instance = new AffixDatabase();
        public static AffixDatabase Instance => _instance;

        // ========== 数据存储 ==========
        /// <summary>所有词缀定义：ID → Definition</summary>
        [SaveableProperty(1)]
        public Dictionary<string, AffixDefinition> AffixMap { get; private set; } = new Dictionary<string, AffixDefinition>();

        private bool _initialized = false;

        private AffixDatabase() { }

        // ========== 初始化 ==========

        /// <summary>初始化预设词缀池。重复调用无副作用。</summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            RegisterMeleePrefixes();
            RegisterRangedPrefixes();
            RegisterArmorPrefixes();
            RegisterCommonSuffixes();
            RegisterUniqueAffixes();

            InformationManager.DisplayMessage(new InformationMessage(
                $"[装备词缀系统] 初始化完成，共加载 {AffixMap.Count} 条词缀定义"));
        }

        // ========== 注册方法 ==========

        private void RegisterAffix(AffixDefinition def)
        {
            if (!AffixMap.ContainsKey(def.Id))
                AffixMap[def.Id] = def;
        }

        // ========== 查询方法 ==========

        public AffixDefinition? GetDefinition(string id)
        {
            AffixMap.TryGetValue(id, out var def);
            return def;
        }

        /// <summary>获取可用于指定装备类型和等级的前缀列表</summary>
        public List<AffixDefinition> GetAvailablePrefixes(string itemType, int itemLevel)
        {
            return AffixMap.Values
                .Where(affix => affix.IsPrefix
                    && affix.CanApplyTo(itemType)
                    && itemLevel >= affix.MinItemLevel
                    && (affix.MaxItemLevel == 0 || itemLevel <= affix.MaxItemLevel))
                .ToList();
        }

        /// <summary>获取可用于指定装备类型和等级的后缀列表</summary>
        public List<AffixDefinition> GetAvailableSuffixes(string itemType, int itemLevel)
        {
            return AffixMap.Values
                .Where(affix => !affix.IsPrefix
                    && affix.CanApplyTo(itemType)
                    && itemLevel >= affix.MinItemLevel
                    && (affix.MaxItemLevel == 0 || itemLevel <= affix.MaxItemLevel))
                .ToList();
        }

        // ========== 预设词缀池 ==========

        // --- 近战武器前缀 ---
        private void RegisterMeleePrefixes()
        {
            var oneHandedTypes = new List<string> { "OneHanded", "Dagger", "Mace" };
            var twoHandedTypes = new List<string> { "TwoHanded", "Polearm" };
            var allMeleeTypes = oneHandedTypes.Concat(twoHandedTypes).ToList();

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_cruel",
                IsPrefix = true,
                DisplayName = "残忍的",
                Description = "伤害大幅提升",
                Weight = 50, MinItemLevel = 20,
                AllowedItemTypes = allMeleeTypes,
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", 15f }, { "ThrustDamage", 10f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_sharp",
                IsPrefix = true,
                DisplayName = "锋利的",
                Description = "伤害小幅提升",
                Weight = 100, MinItemLevel = 1,
                AllowedItemTypes = allMeleeTypes,
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", 5f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_heavy",
                IsPrefix = true,
                DisplayName = "沉重的",
                Description = "伤害提升但速度降低",
                Weight = 70, MinItemLevel = 10,
                AllowedItemTypes = allMeleeTypes,
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", 10f }, { "SpeedRating", -5f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_quick",
                IsPrefix = true,
                DisplayName = "迅捷的",
                Description = "攻击速度提升",
                Weight = 80, MinItemLevel = 5,
                AllowedItemTypes = allMeleeTypes,
                StatModifiers = new Dictionary<string, float> { { "SpeedRating", 8f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_berserker",
                IsPrefix = true,
                DisplayName = "狂战士之",
                Description = "伤害大幅提升，速度小幅降低",
                Weight = 30, MinItemLevel = 35,
                AllowedItemTypes = twoHandedTypes,
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", 25f }, { "SpeedRating", -10f } },
                Rarity = "Rare"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_kings",
                IsPrefix = true,
                DisplayName = "国王之",
                Description = "全属性提升",
                Weight = 15, MinItemLevel = 50,
                AllowedItemTypes = allMeleeTypes,
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", 10f }, { "ThrustDamage", 10f }, { "SpeedRating", 5f }, { "WeaponLength", 5f } },
                Rarity = "Rare"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_rotten",
                IsPrefix = true,
                DisplayName = "腐朽的",
                Description = "伤害降低（诅咒词缀）",
                Weight = 40, MinItemLevel = 1,
                AllowedItemTypes = allMeleeTypes,
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", -8f }, { "SpeedRating", -3f } },
                Rarity = "Magic"
            });
        }

        // --- 远程武器前缀 ---
        private void RegisterRangedPrefixes()
        {
            var rangedTypes = new List<string> { "Bow", "Crossbow" };

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_deadly",
                IsPrefix = true,
                DisplayName = "致命的",
                Description = "远程伤害提升",
                Weight = 60, MinItemLevel = 15,
                AllowedItemTypes = rangedTypes,
                StatModifiers = new Dictionary<string, float> { { "MissileDamage", 12f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_precise",
                IsPrefix = true,
                DisplayName = "精准的",
                Description = "精度提升",
                Weight = 80, MinItemLevel = 5,
                AllowedItemTypes = rangedTypes,
                StatModifiers = new Dictionary<string, float> { { "Accuracy", 10f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_swiftbow",
                IsPrefix = true,
                DisplayName = "疾风之",
                Description = "射速大幅提升",
                Weight = 50, MinItemLevel = 25,
                AllowedItemTypes = rangedTypes,
                StatModifiers = new Dictionary<string, float> { { "SpeedRating", 15f } },
                Rarity = "Rare"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_hunters",
                IsPrefix = true,
                DisplayName = "猎人之",
                Description = "伤害+精度提升",
                Weight = 40, MinItemLevel = 20,
                AllowedItemTypes = rangedTypes,
                StatModifiers = new Dictionary<string, float> { { "MissileDamage", 8f }, { "Accuracy", 5f } },
                Rarity = "Magic"
            });
        }

        // --- 防具前缀 ---
        private void RegisterArmorPrefixes()
        {
            var armorTypes = new List<string> { "HeadArmor", "BodyArmor", "LegArmor", "HandArmor", "Cape" };

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_sturdy",
                IsPrefix = true,
                DisplayName = "坚固的",
                Description = "护甲值提升",
                Weight = 100, MinItemLevel = 1,
                AllowedItemTypes = armorTypes,
                StatModifiers = new Dictionary<string, float> { { "Armor", 8f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_forged",
                IsPrefix = true,
                DisplayName = "精锻的",
                Description = "护甲值中幅提升",
                Weight = 60, MinItemLevel = 20,
                AllowedItemTypes = armorTypes,
                StatModifiers = new Dictionary<string, float> { { "Armor", 15f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_impervious",
                IsPrefix = true,
                DisplayName = "坚不可摧之",
                Description = "护甲值大幅提升",
                Weight = 20, MinItemLevel = 45,
                AllowedItemTypes = armorTypes,
                StatModifiers = new Dictionary<string, float> { { "Armor", 30f } },
                Rarity = "Rare"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_lightweight",
                IsPrefix = true,
                DisplayName = "轻量的",
                Description = "重量降低",
                Weight = 70, MinItemLevel = 5,
                AllowedItemTypes = armorTypes,
                StatModifiers = new Dictionary<string, float> { { "Weight", -2f } },
                Rarity = "Magic"
            });
        }

        // --- 通用后缀 ---
        private void RegisterCommonSuffixes()
        {
            var allTypes = new List<string> { "OneHanded", "TwoHanded", "Dagger", "Mace", "Polearm",
                "Bow", "Crossbow", "Shield", "HeadArmor", "BodyArmor", "LegArmor", "HandArmor", "Cape",
                "Horse", "HorseHarness" };

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_wolf",
                IsPrefix = false,
                DisplayName = "狼之",
                Description = "小幅全属性",
                Weight = 50, MinItemLevel = 10,
                AllowedItemTypes = allTypes,
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", 3f }, { "Armor", 3f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_bear",
                IsPrefix = false,
                DisplayName = "熊之",
                Description = "伤害+护甲提升",
                Weight = 40, MinItemLevel = 20,
                AllowedItemTypes = allTypes,
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", 6f }, { "Armor", 6f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_eagle",
                IsPrefix = false,
                DisplayName = "鹰之",
                Description = "速度+精度提升",
                Weight = 45, MinItemLevel = 15,
                AllowedItemTypes = allTypes,
                StatModifiers = new Dictionary<string, float> { { "SpeedRating", 5f }, { "Accuracy", 5f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_dragon",
                IsPrefix = false,
                DisplayName = "龙之",
                Description = "大幅全属性提升",
                Weight = 10, MinItemLevel = 55,
                AllowedItemTypes = allTypes,
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", 12f }, { "Armor", 12f }, { "SpeedRating", 8f } },
                Rarity = "Rare"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_vampire",
                IsPrefix = false,
                DisplayName = "吸血之",
                Description = "攻击回复生命（预留机制）",
                Weight = 15, MinItemLevel = 40,
                AllowedItemTypes = new List<string> { "OneHanded", "TwoHanded", "Polearm" },
                StatModifiers = new Dictionary<string, float> { { "LifeSteal", 3f } },
                Rarity = "Rare"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_giant",
                IsPrefix = false,
                DisplayName = "巨人之",
                Description = "伤害+武器长度",
                Weight = 30, MinItemLevel = 30,
                AllowedItemTypes = new List<string> { "OneHanded", "TwoHanded", "Polearm" },
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", 10f }, { "WeaponLength", 15f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_thorns",
                IsPrefix = false,
                DisplayName = "荆棘之",
                Description = "反伤（预留机制）",
                Weight = 25, MinItemLevel = 25,
                AllowedItemTypes = new List<string> { "BodyArmor", "Shield" },
                StatModifiers = new Dictionary<string, float> { { "ThornsDamage", 5f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_fire",
                IsPrefix = false,
                DisplayName = "火焰之",
                Description = "附加火焰伤害（预留机制）",
                Weight = 35, MinItemLevel = 15,
                AllowedItemTypes = new List<string> { "OneHanded", "TwoHanded", "Bow", "Crossbow" },
                StatModifiers = new Dictionary<string, float> { { "FireDamage", 8f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_ice",
                IsPrefix = false,
                DisplayName = "冰冻之",
                Description = "附加冰冻伤害（预留机制）",
                Weight = 35, MinItemLevel = 15,
                AllowedItemTypes = new List<string> { "OneHanded", "TwoHanded", "Bow", "Crossbow" },
                StatModifiers = new Dictionary<string, float> { { "FrostDamage", 8f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_lightning",
                IsPrefix = false,
                DisplayName = "闪电之",
                Description = "附加闪电伤害（预留机制）",
                Weight = 30, MinItemLevel = 20,
                AllowedItemTypes = new List<string> { "OneHanded", "TwoHanded", "Bow", "Crossbow" },
                StatModifiers = new Dictionary<string, float> { { "LightningDamage", 10f } },
                Rarity = "Magic"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_fool",
                IsPrefix = false,
                DisplayName = "愚人之",
                Description = "伤害降低（诅咒词缀）",
                Weight = 40, MinItemLevel = 1,
                AllowedItemTypes = allTypes,
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", -5f }, { "SpeedRating", -5f } },
                Rarity = "Magic"
            });
        }

        // --- 暗金/独特词缀（超低权重，高等级专属） ---
        private void RegisterUniqueAffixes()
        {
            var meleeTypes = new List<string> { "OneHanded", "TwoHanded", "Polearm" };
            var armorTypes = new List<string> { "HeadArmor", "BodyArmor", "LegArmor", "HandArmor", "Cape" };

            RegisterAffix(new AffixDefinition
            {
                Id = "prefix_doom",
                IsPrefix = true,
                DisplayName = "末日之",
                Description = "传说级伤害提升",
                Weight = 3, MinItemLevel = 70,
                AllowedItemTypes = meleeTypes,
                StatModifiers = new Dictionary<string, float> { { "SwingDamage", 50f }, { "SpeedRating", 15f }, { "WeaponLength", 10f } },
                Rarity = "Unique"
            });

            RegisterAffix(new AffixDefinition
            {
                Id = "suffix_of_immortal",
                IsPrefix = false,
                DisplayName = "不朽之",
                Description = "传说级防御提升",
                Weight = 3, MinItemLevel = 70,
                AllowedItemTypes = armorTypes,
                StatModifiers = new Dictionary<string, float> { { "Armor", 50f }, { "Weight", -5f } },
                Rarity = "Unique"
            });
        }
    }
}
