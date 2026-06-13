using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;

namespace New_ZZZF
{
    // =========================================================================
    // 技能选择弹窗 ViewModel
    // 显示符合指定类型的技能列表，支持搜索过滤
    // =========================================================================

    /// <summary>
    /// 技能选择弹窗 ViewModel。
    /// 显示符合指定类型的技能列表，支持搜索过滤。
    /// </summary>
    public class SkillSelectionVM : ViewModel
    {
        // ---- 绑定属性 ----

        private MBBindingList<SkillItemVM> _availableSkills;
        private MBBindingList<SkillItemVM> _filteredSkills;
        private string _searchText;
        private string _title;
        private bool _isVisible;

        /// <summary>所有可用技能列表（未过滤）</summary>
        private List<SkillUIData> _allSkills;

        /// <summary>选择回调（选择技能后调用）</summary>
        private Action<SkillUIData> _onSkillSelected;

        /// <summary>关闭回调（取消选择后调用）</summary>
        private Action _onClosed;

        /// <summary>技能类型过滤条件</summary>
        private SPSkillType _filterType;

        /// <summary>过滤后的技能列表（用于绑定到 XML）</summary>
        [DataSourceProperty]
        public MBBindingList<SkillItemVM> FilteredSkills
        {
            get => _filteredSkills;
            set
            {
                if (value != _filteredSkills)
                {
                    _filteredSkills = value;
                    OnPropertyChangedWithValue(value, nameof(FilteredSkills));
                }
            }
        }

        /// <summary>搜索文本（用于过滤技能名称）</summary>
        [DataSourceProperty]
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (value != _searchText)
                {
                    _searchText = value;
                    OnPropertyChangedWithValue(value, nameof(SearchText));
                    FilterSkills(); // 实时过滤
                }
            }
        }

        /// <summary>弹窗标题（如"选择主主动技能"）</summary>
        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set
            {
                if (value != _title)
                {
                    _title = value;
                    OnPropertyChangedWithValue(value, nameof(Title));
                }
            }
        }

        /// <summary>弹窗是否可见</summary>
        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (value != _isVisible)
                {
                    _isVisible = value;
                    OnPropertyChangedWithValue(value, nameof(IsVisible));
                }
            }
        }

        // ---- 构造函数 ----

        /// <summary>
        /// 创建技能选择弹窗 ViewModel
        /// </summary>
        /// <param name="catalog">技能目录</param>
        /// <param name="filterType">技能类型过滤条件</param>
        /// <param name="onSkillSelected">选择回调</param>
        /// <param name="onClosed">关闭回调</param>
        public SkillSelectionVM(SkillCatalog catalog, SPSkillType filterType, Action<SkillUIData> onSkillSelected, Action onClosed)
        {
            _filterType = filterType;
            _onSkillSelected = onSkillSelected;
            _onClosed = onClosed;
            _searchText = string.Empty;
            IsVisible = true;

            // 设置标题
            Title = $"选择{GetTypeDisplayName(filterType)}技能";

            // 从目录加载符合类型的技能
            _allSkills = catalog.GetSkillsOfType(filterType);
            _availableSkills = new MBBindingList<SkillItemVM>();
            FilteredSkills = new MBBindingList<SkillItemVM>();

            // 创建 SkillItemVM 列表
            foreach (var skill in _allSkills)
            {
                _availableSkills.Add(new SkillItemVM(skill, OnSkillSelected));
            }

            // 初始显示全部
            ResetFilteredList();

            // SkillDebug.Log($"[SSVM] 构造完成: 类型={GetTypeDisplayName(filterType)}, _allSkills.Count={_allSkills.Count}, _availableSkills.Count={_availableSkills.Count}, FilteredSkills.Count={FilteredSkills.Count}");
            // 输出前3项技能名称
            // for (int i = 0; i < System.Math.Min(3, _filteredSkills.Count); i++)
            // {
            //     SkillDebug.Log($"[SSVM]   技能[{i}]: {_filteredSkills[i].SkillName} (ID={_filteredSkills[i].SkillId})");
            // }
        }

        // ---- 私有方法 ----

        /// <summary>
        /// 根据搜索文本过滤技能列表
        /// </summary>
        private void FilterSkills()
        {
            _filteredSkills.Clear();

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                // 搜索框为空，显示全部
                ResetFilteredList();
            }
            else
            {
                // 过滤技能名称（不区分大小写）
                var searchLower = _searchText.ToLower();
                var filtered = _availableSkills.Where(item =>
                    item.SkillName.ToLower().Contains(searchLower) ||
                    item.Description.ToLower().Contains(searchLower)
                );

                foreach (var item in filtered)
                {
                    _filteredSkills.Add(item);
                }
                // SkillDebug.Log($"[SSVM] FilterSkills: 搜索='{_searchText}', 结果数={_filteredSkills.Count}");
            }
        }

        /// <summary>
        /// 重置过滤列表（显示全部）
        /// </summary>
        private void ResetFilteredList()
        {
            _filteredSkills.Clear();
            foreach (var item in _availableSkills)
            {
                _filteredSkills.Add(item);
            }
            // SkillDebug.Log($"[SSVM] ResetFilteredList: _filteredSkills.Count={_filteredSkills.Count}");
        }

        /// <summary>
        /// 技能类型显示名称
        /// </summary>
        private string GetTypeDisplayName(SPSkillType type)
        {
            return type switch
            {
                SPSkillType.MainActive => "主主动",
                SPSkillType.SubActive => "副主动",
                SPSkillType.Passive => "被动",
                SPSkillType.CombatArt => "战技",
                SPSkillType.Spell => "法术",
                _ => "未知"
            };
        }

        // ---- 关键导航字段 ----

        private int _selectedIndex = -1;

        /// <summary>当前键盘导航高亮索引（-1 = 无高亮）</summary>
        public int SelectedIndex => _selectedIndex;

        // ---- 命令方法 ----

        /// <summary>
        /// 技能项点击回调（由 SkillItemVM.ExecuteSelect 触发）
        /// </summary>
        private void OnSkillSelected(SkillUIData skillData)
        {
            _onSkillSelected?.Invoke(skillData);
            IsVisible = false;
        }

        /// <summary>
        /// 选择指定技能（由 XML 中的 SkillItem 按钮触发）
        /// </summary>
        public void ExecuteSelectSkill(SkillItemVM skillItem)
        {
            if (skillItem == null) return;

            // 直接从 SkillItemVM 获取原始 SkillUIData
            var skillData = skillItem.SkillData;

            _onSkillSelected?.Invoke(skillData);
            IsVisible = false;
        }

        /// <summary>
        /// 关闭弹窗（取消选择）
        /// </summary>
        public void ExecuteClose()
        {
            _onClosed?.Invoke();
            IsVisible = false;
        }

        /// <summary>
        /// 搜索（由搜索框触发，实际过滤在 SearchText setter 中完成）
        /// </summary>
        public void ExecuteSearch()
        {
            FilterSkills();
        }

        // ---- 键盘导航方法 ----

        /// <summary>
        /// 键盘 ↓ —— 选择下一个技能项，支持循环
        /// </summary>
        public void SelectNextSkill()
        {
            if (FilteredSkills == null || FilteredSkills.Count == 0) return;

            _selectedIndex++;
            if (_selectedIndex >= FilteredSkills.Count)
                _selectedIndex = 0;

            RefreshSkillHighlight();
        }

        /// <summary>
        /// 键盘 ↑ —— 选择上一个技能项，支持循环
        /// </summary>
        public void SelectPrevSkill()
        {
            if (FilteredSkills == null || FilteredSkills.Count == 0) return;

            _selectedIndex--;
            if (_selectedIndex < 0)
                _selectedIndex = FilteredSkills.Count - 1;

            RefreshSkillHighlight();
        }

        /// <summary>
        /// 键盘 Enter —— 选择当前高亮技能项
        /// </summary>
        public void ExecuteSelectCurrentSkill()
        {
            if (FilteredSkills == null || _selectedIndex < 0 || _selectedIndex >= FilteredSkills.Count)
                return;

            var item = FilteredSkills[_selectedIndex];
            if (item != null)
                OnSkillSelected(item.SkillData);
        }

        /// <summary>
        /// 刷新所有技能项的高亮状态
        /// </summary>
        private void RefreshSkillHighlight()
        {
            for (int i = 0; i < FilteredSkills.Count; i++)
            {
                FilteredSkills[i].IsHighlighted = (i == _selectedIndex);
            }
        }

        // ---- 清理 ----

        /// <summary>
        /// 清理资源
        /// </summary>
        public void OnFinalize()
        {
            _onSkillSelected = null;
            _onClosed = null;
            _allSkills = null;
            _availableSkills?.Clear();
            _filteredSkills?.Clear();
        }
    }
}
