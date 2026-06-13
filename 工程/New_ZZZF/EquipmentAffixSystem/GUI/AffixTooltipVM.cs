using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace New_ZZZF
{
    /// <summary>
    /// 词缀 Tooltip ViewModel。
    /// 用于在物品提示框中展示前缀/后缀信息和最终属性。
    ///
    /// 使用方式：
    ///   var vm = new AffixTooltipVM();
    ///   vm.SetAffixItem(itemObject, affixInstance);
    ///   然后将 vm 绑定到 Gauntlet XML 中的 DataSource。
    /// </summary>
    public class AffixTooltipVM : ViewModel
    {
        // ========== 数据源属性 ==========

        private string _itemName = string.Empty;
        private string _prefixText = string.Empty;
        private string _suffixText = string.Empty;
        private string _rarityText = string.Empty;
        private uint _rarityColor = 0xFFFFFFFF;
        private bool _hasAffix;
        private MBBindingList<AffixStatLineVM> _statLines = new MBBindingList<AffixStatLineVM>();

        [DataSourceProperty]
        public string ItemName
        {
            get => _itemName;
            set
            {
                if (_itemName != value)
                {
                    _itemName = value;
                    OnPropertyChangedWithValue(value, nameof(ItemName));
                }
            }
        }

        [DataSourceProperty]
        public string PrefixText
        {
            get => _prefixText;
            set
            {
                if (_prefixText != value)
                {
                    _prefixText = value;
                    OnPropertyChangedWithValue(value, nameof(PrefixText));
                }
            }
        }

        [DataSourceProperty]
        public string SuffixText
        {
            get => _suffixText;
            set
            {
                if (_suffixText != value)
                {
                    _suffixText = value;
                    OnPropertyChangedWithValue(value, nameof(SuffixText));
                }
            }
        }

        [DataSourceProperty]
        public string RarityText
        {
            get => _rarityText;
            set
            {
                if (_rarityText != value)
                {
                    _rarityText = value;
                    OnPropertyChangedWithValue(value, nameof(RarityText));
                }
            }
        }

        [DataSourceProperty]
        public uint RarityColor
        {
            get => _rarityColor;
            set
            {
                if (_rarityColor != value)
                {
                    _rarityColor = value;
                    OnPropertyChangedWithValue(value, nameof(RarityColor));
                }
            }
        }

        [DataSourceProperty]
        public bool HasAffix
        {
            get => _hasAffix;
            set
            {
                if (_hasAffix != value)
                {
                    _hasAffix = value;
                    OnPropertyChangedWithValue(value, nameof(HasAffix));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<AffixStatLineVM> StatLines
        {
            get => _statLines;
            set
            {
                if (_statLines != value)
                {
                    _statLines = value;
                    OnPropertyChangedWithValue(value, nameof(StatLines));
                }
            }
        }

        // ========== 公开方法 ==========

        /// <summary>设置要展示的物品和词缀</summary>
        public void SetAffixItem(ItemObject item, AffixInstance? affix)
        {
            _statLines.Clear();

            if (item == null)
            {
                ItemName = string.Empty;
                PrefixText = string.Empty;
                SuffixText = string.Empty;
                RarityText = string.Empty;
                RarityColor = AffixCampaignBehavior.GetRarityColor("Normal");
                HasAffix = false;
                return;
            }

            if (affix != null && affix.HasAnyAffix)
            {
                HasAffix = true;

                // 完整名称
                ItemName = affix.BuildFullName(item.Name.ToString());

                // 前缀
                var prefixes = affix.GetPrefixDefinitions();
                PrefixText = prefixes.Count > 0
                    ? string.Join(" ", prefixes.Select(p => p.DisplayName))
                    : string.Empty;

                // 后缀
                var suffixes = affix.GetSuffixDefinitions();
                SuffixText = suffixes.Count > 0
                    ? string.Join(" ", suffixes.Select(s => s.DisplayName))
                    : string.Empty;

                // 稀有度
                RarityText = affix.Rarity switch
                {
                    "Normal" => "普通",
                    "Magic" => "魔法",
                    "Rare" => "稀有",
                    "Unique" => "暗金",
                    _ => affix.Rarity
                };
                RarityColor = AffixCampaignBehavior.GetRarityColor(affix.Rarity);

                // 词缀修正属性行
                foreach (var kv in affix.FinalStatModifiers)
                {
                    _statLines.Add(new AffixStatLineVM
                    {
                        StatName = FormatStatName(kv.Key),
                        StatValue = kv.Value,
                        IsPositive = kv.Value >= 0
                    });
                }
            }
            else
            {
                // 无词缀物品
                HasAffix = false;
                ItemName = item.Name.ToString();
                PrefixText = string.Empty;
                SuffixText = string.Empty;
                RarityText = "普通";
                RarityColor = AffixCampaignBehavior.GetRarityColor("Normal");
            }
        }

        /// <summary>清除所有数据</summary>
        public void Clear()
        {
            _statLines.Clear();
            ItemName = string.Empty;
            PrefixText = string.Empty;
            SuffixText = string.Empty;
            RarityText = string.Empty;
            RarityColor = AffixCampaignBehavior.GetRarityColor("Normal");
            HasAffix = false;
        }

        // ========== 辅助 ==========

        private static string FormatStatName(string key)
        {
            return key switch
            {
                "SwingDamage" => "挥砍伤害",
                "ThrustDamage" => "穿刺伤害",
                "MissileDamage" => "远程伤害",
                "SpeedRating" => "攻击速度",
                "WeaponLength" => "武器长度",
                "Accuracy" => "精度",
                "Armor" => "护甲值",
                "Weight" => "重量",
                "LifeSteal" => "吸血",
                "ThornsDamage" => "荆棘反伤",
                "FireDamage" => "火焰伤害",
                "FrostDamage" => "冰冻伤害",
                "LightningDamage" => "闪电伤害",
                _ => key
            };
        }
    }

    /// <summary>
    /// 单条属性修正行的 ViewModel（用于列表绑定）
    /// </summary>
    public class AffixStatLineVM : ViewModel
    {
        private string _statName = string.Empty;
        private float _statValue;
        private bool _isPositive;

        [DataSourceProperty]
        public string StatName
        {
            get => _statName;
            set
            {
                if (_statName != value)
                {
                    _statName = value;
                    OnPropertyChangedWithValue(value, nameof(StatName));
                }
            }
        }

        [DataSourceProperty]
        public float StatValue
        {
            get => _statValue;
            set
            {
                if (Math.Abs(_statValue - value) > 0.001f)
                {
                    _statValue = value;
                    OnPropertyChangedWithValue(value, nameof(StatValue));
                }
            }
        }

        [DataSourceProperty]
        public bool IsPositive
        {
            get => _isPositive;
            set
            {
                if (_isPositive != value)
                {
                    _isPositive = value;
                    OnPropertyChangedWithValue(value, nameof(IsPositive));
                }
            }
        }

        /// <summary>格式化显示文本："+15" 或 "-5"</summary>
        [DataSourceProperty]
        public string DisplayValue => IsPositive ? $"+{StatValue:F0}" : $"{StatValue:F0}";
    }
}
