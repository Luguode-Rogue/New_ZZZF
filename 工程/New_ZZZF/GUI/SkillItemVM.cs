using System;
using TaleWorlds.Library;

namespace New_ZZZF
{
    // =========================================================================
    // 技能选择列表中的单个技能项 ViewModel
    // 用于 SkillSelectionPopup 的技能列表 ItemTemplate
    // =========================================================================

    /// <summary>
    /// 技能选择列表中的单个技能项 ViewModel。
    /// 对应 Gauntlet XML 中技能列表的 ItemTemplate。
    /// </summary>
    public class SkillItemVM : ViewModel
    {
        private readonly SkillUIData _skillData;
        private readonly Action<SkillUIData> _onSelect;

        private string _skillId;
        private string _skillName;
        private string _description;
        private string _iconItemId;
        private int _type;
        private string _typeText;
        private string _cooldownText;
        private string _costText;
        private bool _isHighlighted;

        /// <summary>技能唯一标识符</summary>
        [DataSourceProperty]
        public string SkillId
        {
            get => _skillId;
            set
            {
                if (value != _skillId)
                {
                    _skillId = value;
                    OnPropertyChangedWithValue(value, nameof(SkillId));
                }
            }
        }

        /// <summary>技能显示名称</summary>
        [DataSourceProperty]
        public string SkillName
        {
            get => _skillName;
            set
            {
                if (value != _skillName)
                {
                    _skillName = value;
                    OnPropertyChangedWithValue(value, nameof(SkillName));
                }
            }
        }

        /// <summary>技能描述</summary>
        [DataSourceProperty]
        public string Description
        {
            get => _description;
            set
            {
                if (value != _description)
                {
                    _description = value;
                    OnPropertyChangedWithValue(value, nameof(Description));
                }
            }
        }

        /// <summary>图标物品ID</summary>
        [DataSourceProperty]
        public string IconItemId
        {
            get => _iconItemId;
            set
            {
                if (value != _iconItemId)
                {
                    _iconItemId = value;
                    OnPropertyChangedWithValue(value, nameof(IconItemId));
                }
            }
        }

        /// <summary>技能类型</summary>
        [DataSourceProperty]
        public int Type
        {
            get => _type;
            set
            {
                if (value != _type)
                {
                    _type = value;
                    OnPropertyChangedWithValue(value, nameof(Type));
                }
            }
        }

        /// <summary>技能类型显示文本</summary>
        [DataSourceProperty]
        public string TypeText
        {
            get => _typeText;
            set
            {
                if (value != _typeText)
                {
                    _typeText = value;
                    OnPropertyChangedWithValue(value, nameof(TypeText));
                }
            }
        }

        /// <summary>冷却时间文本（用于 XML Text 绑定）</summary>
        [DataSourceProperty]
        public string CooldownText
        {
            get => _cooldownText;
            set
            {
                if (value != _cooldownText)
                {
                    _cooldownText = value;
                    OnPropertyChangedWithValue(value, nameof(CooldownText));
                }
            }
        }

        /// <summary>冷却时间完整标签（如 "冷却: 3.0s"）</summary>
        [DataSourceProperty]
        public string CooldownLabel
        {
            get => string.IsNullOrEmpty(_cooldownText) || _cooldownText == "-" ? "-" : "冷却: " + _cooldownText;
            set { }
        }

        /// <summary>资源消耗文本（用于 XML Text 绑定）</summary>
        [DataSourceProperty]
        public string CostText
        {
            get => _costText;
            set
            {
                if (value != _costText)
                {
                    _costText = value;
                    OnPropertyChangedWithValue(value, nameof(CostText));
                }
            }
        }

        /// <summary>消耗完整标签（如 "消耗: 50"）</summary>
        [DataSourceProperty]
        public string CostLabel
        {
            get => string.IsNullOrEmpty(_costText) || _costText == "-" ? "-" : "消耗: " + _costText;
            set { }
        }

        /// <summary>键盘导航高亮状态（↑↓切换时高亮当前项）</summary>
        [DataSourceProperty]
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (value != _isHighlighted)
                {
                    _isHighlighted = value;
                    OnPropertyChangedWithValue(value, nameof(IsHighlighted));
                }
            }
        }

        /// <summary>原始技能数据（供父级 ViewModel 读取）</summary>
        public SkillUIData SkillData => _skillData;

        /// <summary>
        /// 创建技能项 ViewModel
        /// </summary>
        /// <param name="skillData">技能数据</param>
        /// <param name="onSelect">选择回调</param>
        public SkillItemVM(SkillUIData skillData, Action<SkillUIData> onSelect)
        {
            _skillData = skillData ?? SkillUIData.Empty;
            _onSelect = onSelect;

            // 初始化绑定属性
            _skillId = _skillData.SkillId ?? string.Empty;
            _skillName = _skillData.SkillName ?? string.Empty;
            _description = _skillData.Description ?? string.Empty;
            _iconItemId = _skillData.IconItemId ?? string.Empty;
            _type = (int)_skillData.Type;

            // TypeText
            _typeText = _skillData.Type switch
            {
                SPSkillType.MainActive => "主主动",
                SPSkillType.SubActive => "副主动",
                SPSkillType.Passive => "被动",
                SPSkillType.CombatArt => "战技",
                SPSkillType.Spell => "法术",
                _ => "未知"
            };

            // CooldownText
            float cd = _skillData.Cooldown;
            _cooldownText = cd > 0f ? cd.ToString("F1") + "s" : "-";

            // CostText
            float cost = _skillData.ResourceCost;
            _costText = cost > 0f ? cost.ToString("F0") : "-";

            // DebugConstructorLog();
        }

        // 调试：全局计数器（前3个输出详细日志）-- 暂时停用
        // private static int _debugInstanceCount = 0;
        // private void DebugConstructorLog()
        // {
        //     int num = System.Threading.Interlocked.Increment(ref _debugInstanceCount);
        //     if (num <= 3)
        //     {
        //         SkillDebug.Log($"[SIVM] SkillItemVM[{num}]: Name={_skillName}, Type={_typeText}, ID={_skillId}");
        //     }
        // }

        /// <summary>
        /// 点击选择此技能
        /// </summary>
        public void ExecuteSelect()
        {
            _onSelect?.Invoke(_skillData);
        }
    }
}
