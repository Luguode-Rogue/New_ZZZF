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

            // 取第一个（主要）前后缀的显示名
            string prefixName = hasPrefix ? prefixes[0].DisplayName : null;
            string suffixName = hasSuffix ? suffixes[0].DisplayName : null;

            // 去掉末尾的连接词（"的"/"之"），用于拼接
            string prefixStem = StripConnector(prefixName);
            string suffixStem = StripConnector(suffixName);

            if (hasPrefix && hasSuffix)
            {
                // 暗黑2风格：前缀之 后缀的 基础名 → "残忍之狼的长剑"
                return $"{prefixStem}之{suffixStem}的{baseItemName}";
            }
            if (hasPrefix)
            {
                // 仅前缀：保持原样 "残忍的 长剑"
                return $"{prefixName} {baseItemName}";
            }
            // 仅后缀：后缀 + 基础名 → "狼之 长剑"
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
