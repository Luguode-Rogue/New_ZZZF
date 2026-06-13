using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace New_ZZZF
{
    /// <summary>
    /// 词缀生成器。
    /// 负责在掉落/商店刷新/战利品生成时，为物品随机抽取前缀和后缀。
    /// 核心流程：选池 → 权重抽取 → 冲突检查 → 属性结算。
    /// </summary>
    public static class AffixGenerator
    {
        // ========== 公开接口 ==========

        /// <summary>
        /// 为指定物品生成词缀实例。
        /// </summary>
        /// <param name="baseItemId">基础物品ID（ItemObject.StringId）</param>
        /// <param name="itemType">装备类型字符串（如 "OneHanded", "Bow", "BodyArmor"）</param>
        /// <param name="itemLevel">物品等级（决定词缀池范围）</param>
        /// <returns>生成的词缀实例；如果池为空则返回无词缀实例</returns>
        public static AffixInstance Generate(string baseItemId, string itemType, int itemLevel)
        {
            int seed = (int)(DateTime.UtcNow.Ticks % int.MaxValue);

            // 确保数据库已初始化
            AffixDatabase db = AffixDatabase.Instance;
            db.Initialize();

            return GenerateInternal(db, baseItemId, itemType, itemLevel, seed);
        }

        /// <summary>
        /// 为指定物品生成词缀实例（指定随机种子，用于可复现的生成）。
        /// </summary>
        public static AffixInstance GenerateSeeded(string baseItemId, string itemType, int itemLevel, int seed)
        {
            AffixDatabase db = AffixDatabase.Instance;
            db.Initialize();
            return GenerateInternal(db, baseItemId, itemType, itemLevel, seed);
        }

        /// <summary>
        /// 强制生成带词缀物品（Debug 用），跳过普通(Normal)判定，至少为魔法品质。
        /// </summary>
        public static AffixInstance GenerateForceAffix(string baseItemId, string itemType, int itemLevel, int seed)
        {
            AffixDatabase db = AffixDatabase.Instance;
            db.Initialize();
            return GenerateInternal(db, baseItemId, itemType, itemLevel, seed, forceAffix: true);
        }

        // ========== 内部实现 ==========

        private static AffixInstance GenerateInternal(
            AffixDatabase db, string baseItemId, string itemType, int itemLevel, int seed,
            bool forceAffix = false)
        {
            // 初始化随机种子（让同一物品的多次生成可复现）
            // 注意：MBRandom 不支持直接设置种子，这里使用 System.Random 做权重抽取
            var sysRandom = new Random(seed);

            var instance = new AffixInstance
            {
                BaseItemId = baseItemId,
                ItemLevel = itemLevel,
            };

            // 获取可用前缀池
            List<AffixDefinition> prefixPool = db.GetAvailablePrefixes(itemType, itemLevel);
            // 获取可用后缀池
            List<AffixDefinition> suffixPool = db.GetAvailableSuffixes(itemType, itemLevel);

            // 决定词缀数量（参考暗黑2规则：魔法1-2个，稀有3-6个）
            int maxPrefixCount = 0;
            int maxSuffixCount = 0;

            // 根据物品等级决定稀有度
            int rarityRoll = sysRandom.Next(1, 101);
            string rarity = forceAffix
                ? DetermineRarity(itemLevel, 1)    // 跳过Normal判定
                : DetermineRarity(itemLevel, rarityRoll);

            switch (rarity)
            {
                case "Normal":
                    maxPrefixCount = 0;
                    maxSuffixCount = 0;
                    break;
                case "Magic":
                    maxPrefixCount = 1;
                    maxSuffixCount = 1;
                    break;
                case "Rare":
                    maxPrefixCount = sysRandom.Next(1, 4); // 1-3个前缀
                    maxSuffixCount = sysRandom.Next(1, 4); // 1-3个后缀
                    break;
                case "Unique":
                    maxPrefixCount = 1;
                    maxSuffixCount = 1;
                    break;
            }

            instance.Rarity = rarity;

            // 抽取前缀
            var selectedPrefixes = new List<AffixDefinition>();
            RollAffixes(prefixPool, maxPrefixCount, selectedPrefixes, instance.PrefixIds, sysRandom);

            // 抽取后缀
            var selectedSuffixes = new List<AffixDefinition>();
            RollAffixes(suffixPool, maxSuffixCount, selectedSuffixes, instance.SuffixIds, sysRandom);

            // 结算最终属性
            ComputeFinalStats(instance, selectedPrefixes, selectedSuffixes);

            // 缓存词缀定义
            instance.ResolveDefinitions(db);

            return instance;
        }

        // ========== 权重抽取 ==========

        private static void RollAffixes(
            List<AffixDefinition> pool,
            int maxCount,
            List<AffixDefinition> selectedDefs,
            List<string> selectedIds,
            Random sysRandom)
        {
            if (pool.Count == 0 || maxCount <= 0) return;

            // 复制池子（避免修改原列表）
            var remainingPool = new List<AffixDefinition>(pool);

            for (int i = 0; i < maxCount && remainingPool.Count > 0; i++)
            {
                // 移除与已选词缀冲突的候选项
                remainingPool.RemoveAll(def => def.ConflictsWith(selectedIds));

                if (remainingPool.Count == 0) break;

                // 计算总权重
                int totalWeight = remainingPool.Sum(def => def.Weight);
                if (totalWeight <= 0) break;

                // 权重随机抽取
                int roll = sysRandom.Next(0, totalWeight);
                AffixDefinition? selected = null;
                int cumulative = 0;

                foreach (var def in remainingPool)
                {
                    cumulative += def.Weight;
                    if (roll < cumulative)
                    {
                        selected = def;
                        break;
                    }
                }

                if (selected != null)
                {
                    selectedDefs.Add(selected);
                    selectedIds.Add(selected.Id);
                    remainingPool.Remove(selected);
                }
            }
        }

        // ========== 属性结算 ==========

        /// <summary>将所有词缀的属性修正叠加到 FinalStatModifiers 中</summary>
        private static void ComputeFinalStats(
            AffixInstance instance,
            List<AffixDefinition> prefixes,
            List<AffixDefinition> suffixes)
        {
            var finalStats = new Dictionary<string, float>();

            foreach (var prefix in prefixes)
            {
                foreach (var kv in prefix.StatModifiers)
                {
                    finalStats.TryGetValue(kv.Key, out float current);
                    finalStats[kv.Key] = current + kv.Value;
                }
            }

            foreach (var suffix in suffixes)
            {
                foreach (var kv in suffix.StatModifiers)
                {
                    finalStats.TryGetValue(kv.Key, out float current);
                    finalStats[kv.Key] = current + kv.Value;
                }
            }

            instance.FinalStatModifiers = finalStats;
        }

        // ========== 稀有度判定 ==========

        private static string DetermineRarity(int itemLevel, int roll)
        {
            // 基础概率（随等级变化）
            float normalChance = Math.Max(0f, 90f - itemLevel * 0.5f);
            float magicChance = Math.Min(40f, 8f + itemLevel * 0.4f);
            float rareChance = Math.Min(15f, 1f + itemLevel * 0.15f);
            float uniqueChance = Math.Min(2f, itemLevel * 0.01f);

            // 确保总和不超过100
            float total = normalChance + magicChance + rareChance + uniqueChance;
            if (total > 100f)
            {
                normalChance = Math.Max(0f, normalChance - (total - 100f));
            }

            int threshold = 0;

            // Unique
            threshold += (int)Math.Floor(uniqueChance);
            if (roll <= threshold) return "Unique";

            // Rare
            threshold += (int)Math.Floor(rareChance);
            if (roll <= threshold) return "Rare";

            // Magic
            threshold += (int)Math.Floor(magicChance);
            if (roll <= threshold) return "Magic";

            // Normal
            return "Normal";
        }

        // ========== 工具方法 ==========

        /// <summary>快速预览一次生成的词缀组合（调试用）</summary>
        public static string Preview(string baseItemId, string itemType, int itemLevel, int seed = 0)
        {
            if (seed == 0) seed = Guid.NewGuid().GetHashCode();
            var instance = GenerateSeeded(baseItemId, itemType, itemLevel, seed);
            return instance.ToString();
        }
    }
}
