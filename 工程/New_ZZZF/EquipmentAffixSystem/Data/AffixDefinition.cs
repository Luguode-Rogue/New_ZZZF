using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace New_ZZZF
{
    /// <summary>
    /// 词缀定义：描述一个前缀或后缀的完整属性。
    /// 类似暗黑2的 MagicPrefix / MagicSuffix 概念。
    /// </summary>
    public sealed class AffixDefinition
    {
        /// <summary>唯一ID，如 "affix_cruel"</summary>
        [SaveableProperty(1)]
        public string Id { get; set; } = string.Empty;

        /// <summary>true=前缀, false=后缀</summary>
        [SaveableProperty(2)]
        public bool IsPrefix { get; set; }

        /// <summary>显示名称，如 "残忍的"、"屠夫之"</summary>
        [SaveableProperty(3)]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>描述文本</summary>
        [SaveableProperty(4)]
        public string Description { get; set; } = string.Empty;

        /// <summary>抽取权重（越高越常见）</summary>
        [SaveableProperty(5)]
        public int Weight { get; set; } = 100;

        /// <summary>最小物品等级要求</summary>
        [SaveableProperty(6)]
        public int MinItemLevel { get; set; } = 1;

        /// <summary>最大物品等级（0=无上限）</summary>
        [SaveableProperty(7)]
        public int MaxItemLevel { get; set; }

        /// <summary>
        /// 允许出现的装备类型。
        /// 如 "OneHanded", "TwoHanded", "Bow", "Crossbow", "Shield",
        ///      "HeadArmor", "BodyArmor", "LegArmor", "HandArmor", "Cape",
        ///      "Horse", "HorseHarness"
        /// </summary>
        [SaveableProperty(8)]
        public List<string> AllowedItemTypes { get; set; } = new List<string>();

        /// <summary>与此词缀冲突的其他词缀ID列表（不能同时出现）</summary>
        [SaveableProperty(9)]
        public List<string> ConflictAffixIds { get; set; } = new List<string>();

        /// <summary>属性修正表：属性名 → 修正值（正=增加，负=减少）</summary>
        [SaveableProperty(10)]
        public Dictionary<string, float> StatModifiers { get; set; } = new Dictionary<string, float>();

        /// <summary>品质颜色（暗黑2风格：白/蓝/黄/暗金）</summary>
        [SaveableProperty(11)]
        public string Rarity { get; set; } = "Magic";

        // ===== 便利方法 =====

        /// <summary>检查该词缀是否适用于指定物品类型</summary>
        public bool CanApplyTo(string itemType)
        {
            return AllowedItemTypes.Count == 0 || AllowedItemTypes.Contains(itemType);
        }

        /// <summary>检查是否与给定词缀列表冲突</summary>
        public bool ConflictsWith(IEnumerable<string> existingAffixIds)
        {
            foreach (string existingId in existingAffixIds)
            {
                if (ConflictAffixIds.Contains(existingId))
                    return true;
            }
            return false;
        }

        public override string ToString()
        {
            return $"[{(IsPrefix ? "前缀" : "后缀")}] {DisplayName} ({Id}) Weight={Weight}";
        }
    }
}
