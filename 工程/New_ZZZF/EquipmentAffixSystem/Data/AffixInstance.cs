using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.SaveSystem;

namespace New_ZZZF
{
    /// <summary>
    /// 单件物品的词缀实例。
    /// 记录一件具体装备在当前存档中随机到的前缀/后缀组合。
    /// 必须支持序列化（SaveSystem），否则读档后会丢失随机结果。
    /// </summary>
    public class AffixInstance
    {
        // ========== 存档键：用于在字典中唯一定位 ==========
        /// <summary>生成时的唯一标识（Guid字符串）</summary>
        [SaveableProperty(1)]
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>基础物品的 ItemObject.StringId</summary>
        [SaveableProperty(2)]
        public string BaseItemId { get; set; } = string.Empty;

        /// <summary>生成时的物品等级，影响词缀池选取</summary>
        [SaveableProperty(3)]
        public int ItemLevel { get; set; } = 1;

        // ========== 词缀数据 ==========
        /// <summary>前缀ID列表（通常0-3个）</summary>
        [SaveableProperty(4)]
        public List<string> PrefixIds { get; set; } = new List<string>();

        /// <summary>后缀ID列表（通常0-3个）</summary>
        [SaveableProperty(5)]
        public List<string> SuffixIds { get; set; } = new List<string>();

        // ========== 已结算属性（生成时计算，存档保存，避免每次读档重算） ==========
        /// <summary>最终属性：属性名 → 经过词缀修正后的总值</summary>
        [SaveableProperty(6)]
        public Dictionary<string, float> FinalStatModifiers { get; set; } = new Dictionary<string, float>();

        [SaveableProperty(7)]
        public string Rarity { get; set; } = "Normal";

        // ========== 非存档辅助字段 ==========
        /// <summary>缓存：前缀定义列表（运行时填充，不存档）</summary>
        [NonSerialized]
        private List<AffixDefinition>? _cachedPrefixDefs;

        /// <summary>缓存：后缀定义列表（运行时填充，不存档）</summary>
        [NonSerialized]
        private List<AffixDefinition>? _cachedSuffixDefs;

        public AffixInstance()
        {
            InstanceId = Guid.NewGuid().ToString();
        }

        // ===== 便利方法 =====

        /// <summary>是否有任何词缀</summary>
        public bool HasAnyAffix => PrefixIds.Count > 0 || SuffixIds.Count > 0;

        /// <summary>获取所有词缀ID（前缀+后缀）</summary>
        public IEnumerable<string> AllAffixIds => PrefixIds.Concat(SuffixIds);

        /// <summary>填充缓存的词缀定义（由数据库在合适时机调用）</summary>
        public void ResolveDefinitions(AffixDatabase database)
        {
            _cachedPrefixDefs = PrefixIds
                .Select(id => database.GetDefinition(id))
                .Where(def => def != null)
                .Cast<AffixDefinition>()
                .ToList();

            _cachedSuffixDefs = SuffixIds
                .Select(id => database.GetDefinition(id))
                .Where(def => def != null)
                .Cast<AffixDefinition>()
                .ToList();
        }

        /// <summary>获取已解析的前缀定义（需先调用 ResolveDefinitions）</summary>
        public IReadOnlyList<AffixDefinition> GetPrefixDefinitions()
            => _cachedPrefixDefs ?? (IReadOnlyList<AffixDefinition>)Array.Empty<AffixDefinition>();

        /// <summary>获取已解析的后缀定义（需先调用 ResolveDefinitions）</summary>
        public IReadOnlyList<AffixDefinition> GetSuffixDefinitions()
            => _cachedSuffixDefs ?? (IReadOnlyList<AffixDefinition>)Array.Empty<AffixDefinition>();

        /// <summary>构建完整显示名称：暗黑2风格 "残忍之狼的长剑"</summary>
        public string BuildFullName(string baseItemName)
        {
            var prefixes = GetPrefixDefinitions();
            var suffixes = GetSuffixDefinitions();
            bool hasPrefix = prefixes.Count > 0;
            bool hasSuffix = suffixes.Count > 0;

            if (!hasPrefix && !hasSuffix)
                return baseItemName;

            // 全部前后缀去末尾"的/之"后拼接（无空格），用于"X之Y的Z"的X/Y部分
            string prefixStem = hasPrefix ? string.Join("", prefixes.Select(p => StripConnector(p.DisplayName))) : null;
            string suffixStem = hasSuffix ? string.Join("", suffixes.Select(p => StripConnector(p.DisplayName))) : null;

            // 原始显示名（保留"的/之"），用于仅前/后缀时的单独显示
            string prefixName = hasPrefix ? string.Join(" ", prefixes.Select(p => p.DisplayName)) : null;
            string suffixName = hasSuffix ? string.Join(" ", suffixes.Select(p => p.DisplayName)) : null;

            if (hasPrefix && hasSuffix)
            {
                // 暗黑风格：前缀词干之 后缀词干的 基础名
                return $"{prefixStem}之{suffixStem}的{baseItemName}";
            }
            if (hasPrefix)
            {
                return $"{prefixName} {baseItemName}";
            }
            return $"{suffixName} {baseItemName}";
        }

        /// <summary>去掉中文词缀名末尾的"的"或"之"</summary>
        private static string StripConnector(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.EndsWith("的") || name.EndsWith("之"))
                return name.Substring(0, name.Length - 1);
            return name;
        }

        public override string ToString()
        {
            return $"[{Rarity}] {BaseItemId} Lv{ItemLevel} | Pre:{PrefixIds.Count} Suf:{SuffixIds.Count} | ID:{InstanceId}";
        }
    }
}
