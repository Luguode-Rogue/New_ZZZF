using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace New_ZZZF
{
    // =========================================================================
    // 任务二：视图模型编写 (ViewModel)
    //
    // HeroVM        — 队伍列表中的单个英雄项
    // SkillSlotVM   — 单个技能槽位（绑定到 Gauntlet XML）
    // CustomSkillScreenVM — 主界面 ViewModel，管理 Roster + Skills + 英雄切换
    //
    // 不依赖 InventoryLogic，不与物品系统耦合。
    // =========================================================================


    /// <summary>
    /// 队伍成员列表项 ViewModel。
    /// 对应 Gauntlet XML 中 Roster 列表的 ItemTemplate。
    /// </summary>
    public class HeroVM : ViewModel
    {
        private string _heroId;
        private string _heroName;
        private bool _isSelected;
        private readonly Action<HeroVM> _onSelect;

        public HeroVM(Hero hero, Action<HeroVM> onSelect)
        {
            _heroId = hero.CharacterObject?.StringId ?? hero.Name?.ToString() ?? string.Empty;
            _heroName = hero.Name?.ToString() ?? hero.CharacterObject?.Name?.ToString() ?? _heroId;
            _isSelected = false;
            _onSelect = onSelect;
        }

        /// <summary>英雄标识（CharacterObject.StringId）</summary>
        [DataSourceProperty]
        public string HeroId
        {
            get => _heroId;
            set
            {
                if (value != _heroId)
                {
                    _heroId = value;
                    OnPropertyChangedWithValue(value, nameof(HeroId));
                }
            }
        }

        /// <summary>英雄显示名称</summary>
        [DataSourceProperty]
        public string HeroName
        {
            get => _heroName;
            set
            {
                if (value != _heroName)
                {
                    _heroName = value;
                    OnPropertyChangedWithValue(value, nameof(HeroName));
                }
            }
        }

        /// <summary>是否当前选中</summary>
        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    OnPropertyChangedWithValue(value, nameof(IsSelected));
                }
            }
        }

        /// <summary>点击选择此英雄（由 Gauntlet ButtonWidget.Command.Click 触发）</summary>
        public void ExecuteSelect()
        {
            _onSelect?.Invoke(this);
        }
    }


    /// <summary>
    /// 技能槽位 ViewModel —— 表示 MainActive / SubActive / Passive / CombatArt / Spell 等槽位。
    /// 每个槽位可以装备一个 SkillUIData（或空）。
    /// </summary>
    public class SkillSlotVM : ViewModel
    {
        private string _slotId;
        private string _slotLabel;
        private SkillUIData _skill;
        private Action<SkillSlotVM> _onClickSlot;

        /// <summary>
        /// 槽位标识符，如 "MainActive", "Spell2"
        /// </summary>
        [DataSourceProperty]
        public string SlotId
        {
            get => _slotId;
            set
            {
                if (value != _slotId)
                {
                    _slotId = value;
                    OnPropertyChangedWithValue(value, nameof(SlotId));
                }
            }
        }

        /// <summary>槽位显示标签，如 "主主动", "法术①"</summary>
        [DataSourceProperty]
        public string SlotLabel
        {
            get => _slotLabel;
            set
            {
                if (value != _slotLabel)
                {
                    _slotLabel = value;
                    OnPropertyChangedWithValue(value, nameof(SlotLabel));
                }
            }
        }

        /// <summary>当前装备的技能名称（空槽显示 "空"）</summary>
        [DataSourceProperty]
        public string SkillName
        {
            get => _skill?.SkillName ?? string.Empty;
            set
            {
                // 只读，但需要 setter 满足 DataSourceProperty 模式
            }
        }

        /// <summary>图标物品 ID，供 SpriteWidget 使用</summary>
        [DataSourceProperty]
        public string SkillIcon
        {
            get => _skill?.IconItemId ?? string.Empty;
            set { }
        }

        /// <summary>此槽位是否为空（未装备技能）</summary>
        [DataSourceProperty]
        public bool IsEmpty
        {
            get => _skill == null || _skill.IsEmpty;
            set { }
        }

        /// <summary>冷却时间文本（如 "3.0s" 或 "-"）</summary>
        [DataSourceProperty]
        public string CooldownText
        {
            get
            {
                if (_skill == null || _skill.IsEmpty) return "-";
                float cd = _skill.Cooldown;
                return cd > 0f ? cd.ToString("F1") + "s" : "-";
            }
            set { }
        }

        /// <summary>资源消耗文本（如 "50" 或 "-"）</summary>
        [DataSourceProperty]
        public string CostText
        {
            get
            {
                if (_skill == null || _skill.IsEmpty) return "-";
                float cost = _skill.ResourceCost;
                return cost > 0f ? cost.ToString("F0") : "-";
            }
            set { }
        }

        /// <summary>槽位对应的技能类型（用于过滤可选技能列表）</summary>
        public SPSkillType SlotFilterType { get; private set; } = SPSkillType.None;

        /// <summary>当前装备的技能数据（非绑定，供逻辑层读写）</summary>
        public SkillUIData Skill => _skill;

        /// <summary>
        /// 创建一个技能槽位
        /// </summary>
        /// <param name="slotId">槽位标识符（如 "MainActive"）</param>
        /// <param name="slotLabel">显示标签（如 "主主动"）</param>
        /// <param name="filterType">此槽位接受的技能类型</param>
        /// <param name="onClickSlot">点击回调</param>
        public SkillSlotVM(string slotId, string slotLabel, SPSkillType filterType, Action<SkillSlotVM> onClickSlot)
        {
            _slotId = slotId ?? string.Empty;
            _slotLabel = slotLabel ?? string.Empty;
            SlotFilterType = filterType;
            _onClickSlot = onClickSlot;
            _skill = SkillUIData.Empty;
        }

        /// <summary>
        /// 设置槽位中的技能（并刷新绑定属性）
        /// </summary>
        public void SetSkill(SkillUIData skillData)
        {
            _skill = skillData ?? SkillUIData.Empty;
            OnPropertyChangedWithValue(_skill.SkillName, nameof(SkillName));
            OnPropertyChangedWithValue(_skill.IconItemId, nameof(SkillIcon));
            OnPropertyChangedWithValue(_skill.IsEmpty, nameof(IsEmpty));
            // 冷却和消耗文本
            float cd = _skill.Cooldown;
            string cdText = (_skill.IsEmpty || cd <= 0f) ? "-" : cd.ToString("F1") + "s";
            OnPropertyChangedWithValue(cdText, nameof(CooldownText));
            float cost = _skill.ResourceCost;
            string costText = (_skill.IsEmpty || cost <= 0f) ? "-" : cost.ToString("F0");
            OnPropertyChangedWithValue(costText, nameof(CostText));
        }

        /// <summary>清空此槽位</summary>
        public void ClearSkill()
        {
            SetSkill(SkillUIData.Empty);
        }

        /// <summary>点击槽位 —— 触发技能选择/更换</summary>
        public void ExecuteClick()
        {
            _onClickSlot?.Invoke(this);
        }
    }


    /// <summary>
    /// 技能界面主 ViewModel。
    /// 
    /// 数据绑定关系：
    ///   CustomSkillScreen (Gauntlet XML)
    ///     ├── DataSource="{Roster}"    → MBBindingList&lt;HeroVM&gt;
    ///     └── DataSource="{Skills}"    → MBBindingList&lt;SkillSlotVM&gt;
    /// 
    /// 生命周期：
    ///   构造 → 加载 SkillCatalog → 填充 Roster → 创建技能槽 → 选中默认英雄
    ///   切换英雄 → LoadSkillsForHero(heroId) → 刷新各 SkillSlotVM
    /// </summary>
    public class CustomSkillScreenVM : ViewModel
    {
        // ---- 绑定属性 ----

        private MBBindingList<HeroVM> _roster;
        private MBBindingList<SkillSlotVM> _skills;
        private HeroVM _currentHero;
        private string _currentHeroId;

        /// <summary>队伍成员列表</summary>
        [DataSourceProperty]
        public MBBindingList<HeroVM> Roster
        {
            get => _roster;
            set
            {
                if (value != _roster)
                {
                    _roster = value;
                    OnPropertyChangedWithValue(value, nameof(Roster));
                }
            }
        }

        /// <summary>当前英雄的技能槽位列表（8个槽位）</summary>
        [DataSourceProperty]
        public MBBindingList<SkillSlotVM> Skills
        {
            get => _skills;
            set
            {
                if (value != _skills)
                {
                    _skills = value;
                    OnPropertyChangedWithValue(value, nameof(Skills));
                }
            }
        }

        /// <summary>当前选中的英雄 VM</summary>
        [DataSourceProperty]
        public HeroVM CurrentHero
        {
            get => _currentHero;
            set
            {
                if (value != _currentHero)
                {
                    _currentHero = value;
                    OnPropertyChangedWithValue(value, nameof(CurrentHero));
                }
            }
        }

        /// <summary>当前英雄 ID（便捷属性）</summary>
        [DataSourceProperty]
        public string CurrentHeroId
        {
            get => _currentHeroId;
            set
            {
                if (value != _currentHeroId)
                {
                    _currentHeroId = value;
                    OnPropertyChangedWithValue(value, nameof(CurrentHeroId));
                }
            }
        }

        /// <summary>是否有未保存的更改（控制"应用"按钮可用性）</summary>
        [DataSourceProperty]
        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (value != _isDirty)
                {
                    _isDirty = value;
                    OnPropertyChangedWithValue(value, nameof(IsDirty));
                }
            }
        }

        /// <summary>技能选择弹窗 ViewModel（为 null 时弹窗不显示）</summary>
        private SkillSelectionVM _skillSelectionPopup;
        [DataSourceProperty]
        public SkillSelectionVM SkillSelectionPopup
        {
            get => _skillSelectionPopup;
            set
            {
                if (value != _skillSelectionPopup)
                {
                    // SkillDebug.Log($"[CSVM] SkillSelectionPopup 变更: 旧值={(_skillSelectionPopup != null)}, 新值={(value != null)}");
                    _skillSelectionPopup = value;
                    OnPropertyChangedWithValue(value, nameof(SkillSelectionPopup));
                }
            }
        }

        // ---- 非绑定字段 ----

        /// <summary>全技能目录（供技能选择列表使用）</summary>
        public readonly SkillCatalog Catalog;

        /// <summary>当前英雄的技能配置（可编辑副本）</summary>
        private HeroSkillData _currentHeroSkillData;

        /// <summary>是否有未保存的更改（脏标记）</summary>
        private bool _isDirty;

        /// <summary>关闭回调（由 Screen 注入）</summary>
        private Action _onClose;

        /// <summary>槽位ID → SkillSlotVM 快速查找</summary>
        private readonly Dictionary<string, SkillSlotVM> _slotMap = new Dictionary<string, SkillSlotVM>();

        // ---- 构造函数 ----

        public CustomSkillScreenVM()
        {
            // 一次性加载全技能目录
            Catalog = SkillCatalog.LoadFromFactory();

            Roster = new MBBindingList<HeroVM>();
            Skills = new MBBindingList<SkillSlotVM>();

            // 创建固定槽位
            CreateSkillSlots();

            // 填充队伍列表
            PopulateRoster();

            // SkillDebug.Log($"[CSVM] 构造完成: Catalog技能总数={Catalog?.AllSkills?.Count}, Roster.Count={Roster.Count}, Skills.Count={Skills.Count}");
        }

        // ---- 初始化方法 ----

        /// <summary>
        /// 从玩家部队填充 Roster 列表
        /// </summary>
        private void PopulateRoster()
        {
            Roster.Clear();

            var playerClan = Clan.PlayerClan;
            if (playerClan == null) return;

            // 玩家部队中的英雄（包含同伴、家族成员等）
            int comeOfAge = Campaign.Current.Models?.AgeModel?.HeroComesOfAge ?? 18;

            foreach (var hero in playerClan.Heroes)
            {
                if (hero == null) continue;

                // 严格过滤：存活 + 成年 + 行动自由（排除婴儿/死者/被俘/经商/总督）
                if (!hero.IsAlive) continue;
                if (hero.Age < comeOfAge) continue;
                if (hero.HeroState != Hero.CharacterStates.Active && hero != Hero.MainHero) continue;

                Roster.Add(new HeroVM(hero, OnHeroSelected));
            }

            // 默认选中第一个
            if (Roster.Count > 0)
            {
                SelectHero(Roster[0]);
            }

            // SkillDebug.Log($"[CSVM] PopulateRoster 完成: Clan={playerClan?.Name}, 过滤后Roster.Count={Roster.Count}");
        }

        /// <summary>
        /// 创建8个技能槽位 ViewModel：
        ///   MainActive / SubActive / Passive / CombatArt / Spell0~3
        /// </summary>
        private void CreateSkillSlots()
        {
            Skills.Clear();
            _slotMap.Clear();

            // 定义槽位配置：(slotId, slotLabel, filterType)
            var slotDefs = new (string id, string label, SPSkillType filterType)[]
            {
                ("MainActive",  "主主动",   SPSkillType.MainActive),
                ("SubActive",   "副主动",   SPSkillType.SubActive),
                ("Passive",     "被动",     SPSkillType.Passive),
                ("CombatArt",   "战技",     SPSkillType.CombatArt),
                ("Spell0",      "法术①",   SPSkillType.Spell),
                ("Spell1",      "法术②",   SPSkillType.Spell),
                ("Spell2",      "法术③",   SPSkillType.Spell),
                ("Spell3",      "法术④",   SPSkillType.Spell),
            };

            foreach (var def in slotDefs)
            {
                var slotVM = new SkillSlotVM(def.id, def.label, def.filterType, OnSlotClicked);
                Skills.Add(slotVM);
                _slotMap[def.id] = slotVM;
            }
        }

        /// <summary>
        /// 设置关闭回调（由 Screen 注入）
        /// </summary>
        public void SetCloseAction(Action onClose)
        {
            _onClose = onClose;
        }

        // ---- 英雄选择逻辑 ----

        /// <summary>
        /// 选中指定英雄（由 HeroVM.ExecuteSelect 回调触发）
        /// </summary>
        private void OnHeroSelected(HeroVM selectedHero)
        {
            if (selectedHero == null) return;

            // 取消之前选中的高亮
            if (CurrentHero != null)
                CurrentHero.IsSelected = false;

            SelectHero(selectedHero);
        }

        private void SelectHero(HeroVM hero)
        {
            // 切换英雄时丢弃当前英雄的未保存更改
            IsDirty = false;

            CurrentHero = hero;
            CurrentHeroId = hero.HeroId;
            hero.IsSelected = true;
            LoadSkillsForHero(hero.HeroId);
        }

        // ---- 键盘导航：英雄列表 ----

        /// <summary>
        /// 键盘 ↓ —— 选择下一个英雄
        /// </summary>
        public void SelectNextHero()
        {
            if (Roster == null || Roster.Count <= 1) return;

            int currentIndex = GetCurrentHeroIndex();
            int nextIndex = currentIndex + 1;
            if (nextIndex >= Roster.Count)
                nextIndex = 0;

            if (CurrentHero != null)
                CurrentHero.IsSelected = false;

            SelectHero(Roster[nextIndex]);
        }

        /// <summary>
        /// 键盘 ↑ —— 选择上一个英雄
        /// </summary>
        public void SelectPrevHero()
        {
            if (Roster == null || Roster.Count <= 1) return;

            int currentIndex = GetCurrentHeroIndex();
            int prevIndex = currentIndex - 1;
            if (prevIndex < 0)
                prevIndex = Roster.Count - 1;

            if (CurrentHero != null)
                CurrentHero.IsSelected = false;

            SelectHero(Roster[prevIndex]);
        }

        private int GetCurrentHeroIndex()
        {
            if (CurrentHero == null || Roster == null) return -1;
            for (int i = 0; i < Roster.Count; i++)
            {
                if (Roster[i] == CurrentHero)
                    return i;
            }
            return -1;
        }

        // ---- 键盘导航：技能槽位 ----

        /// <summary>
        /// 键盘 1~8 —— 直接打开对应槽位的技能选择弹窗
        /// </summary>
        /// <param name="index">0-based 槽位索引 (0=主主动, 1=副主动, ..., 7=法术④)</param>
        public void SelectSlotByIndex(int index)
        {
            if (Skills == null || index < 0 || index >= Skills.Count) return;
            OnSlotClicked(Skills[index]);
        }

        // ---- 弹窗键盘导航委托 ----

        /// <summary>弹窗是否打开</summary>
        public bool IsPopupOpen => SkillSelectionPopup != null;

        /// <summary>弹窗内键盘 ↓</summary>
        public void PopupSelectNextSkill()
        {
            SkillSelectionPopup?.SelectNextSkill();
        }

        /// <summary>弹窗内键盘 ↑</summary>
        public void PopupSelectPrevSkill()
        {
            SkillSelectionPopup?.SelectPrevSkill();
        }

        /// <summary>弹窗内键盘 Enter</summary>
        public void PopupSelectCurrentSkill()
        {
            SkillSelectionPopup?.ExecuteSelectCurrentSkill();
        }

        /// <summary>
        /// 从 SkillConfigManager 加载指定英雄的技能配置，填充到各 SkillSlotVM
        /// </summary>
        private void LoadSkillsForHero(string heroId)
        {
            _currentHeroSkillData = HeroSkillData.LoadForHero(heroId);
            // SkillDebug.Log($"[CSVM] LoadSkillsForHero: heroId={heroId}, _currentHeroSkillData={(_currentHeroSkillData != null)}");

            // 非法术槽位
            SetSlotSkill("MainActive", _currentHeroSkillData.MainActive);
            SetSlotSkill("SubActive",  _currentHeroSkillData.SubActive);
            SetSlotSkill("Passive",    _currentHeroSkillData.Passive);
            SetSlotSkill("CombatArt",  _currentHeroSkillData.CombatArt);

            // 4个法术槽位
            for (int i = 0; i < 4; i++)
            {
                if (i < _currentHeroSkillData.Spells.Length)
                    SetSlotSkill($"Spell{i}", _currentHeroSkillData.Spells[i]);
            }
        }

        private void SetSlotSkill(string slotId, SkillUIData skill)
        {
            if (_slotMap.TryGetValue(slotId, out var slotVM))
            {
                slotVM.SetSkill(skill);
            }
        }

        // ---- 技能槽操作 ----

        /// <summary>
        /// 点击技能槽位（由 SkillSlotVM.ExecuteClick 触发）。
        /// 打开技能选择弹窗，选择后分配到槽位。
        /// </summary>
        private void OnSlotClicked(SkillSlotVM slotVM)
        {
            if (slotVM == null) return;
            if (Catalog == null) return;

            // SkillDebug.Log($"[CSVM] 槽位点击: {slotVM.SlotId} (类型: {slotVM.SlotFilterType})");

            // 创建技能选择弹窗 ViewModel
            var skillSelectionVM = new SkillSelectionVM(
                Catalog,
                slotVM.SlotFilterType,
                // 选择回调：将选中的技能分配到槽位
                selectedSkill =>
                {
                    AssignSkillToSlot(slotVM, selectedSkill);
                    SkillSelectionPopup = null; // 关闭弹窗
                },
                // 关闭回调：取消选择
                () =>
                {
                    SkillSelectionPopup = null; // 关闭弹窗
                }
            );

            // 显示弹窗
            SkillSelectionPopup = skillSelectionVM;
            // SkillDebug.Log($"[CSVM] OnSlotClicked 完成: SkillSelectionPopup 已设置, FilteredSkills.Count={skillSelectionVM.FilteredSkills?.Count}, IsVisible={skillSelectionVM.IsVisible}");
        }

        /// <summary>
        /// 将指定技能分配给槽位并保存
        /// </summary>
        public void AssignSkillToSlot(SkillSlotVM slotVM, SkillUIData skillData)
        {
            if (slotVM == null) return;

            // SkillDebug.Log($"[CSVM] AssignSkillToSlot: slot={slotVM.SlotId}, skill={skillData?.SkillName ?? "(空)"}, skillId={skillData?.SkillId ?? "(空)"}");
            slotVM.SetSkill(skillData);

            // 同步到 HeroSkillData
            if (_currentHeroSkillData != null)
            {
                switch (slotVM.SlotId)
                {
                    case "MainActive": _currentHeroSkillData.MainActive = skillData; break;
                    case "SubActive":  _currentHeroSkillData.SubActive  = skillData; break;
                    case "Passive":    _currentHeroSkillData.Passive    = skillData; break;
                    case "CombatArt":  _currentHeroSkillData.CombatArt  = skillData; break;
                    case "Spell0":     if (_currentHeroSkillData.Spells.Length > 0) _currentHeroSkillData.Spells[0] = skillData; break;
                    case "Spell1":     if (_currentHeroSkillData.Spells.Length > 1) _currentHeroSkillData.Spells[1] = skillData; break;
                    case "Spell2":     if (_currentHeroSkillData.Spells.Length > 2) _currentHeroSkillData.Spells[2] = skillData; break;
                    case "Spell3":     if (_currentHeroSkillData.Spells.Length > 3) _currentHeroSkillData.Spells[3] = skillData; break;
                }
            }

            // 标记脏数据，等待用户点击"应用"确认
            IsDirty = true;
        }

        /// <summary>
        /// 清空指定槽位
        /// </summary>
        public void ClearSkillSlot(SkillSlotVM slotVM)
        {
            AssignSkillToSlot(slotVM, SkillUIData.Empty);
        }

        /// <summary>
        /// 保存当前英雄的技能配置到 SkillConfigManager
        /// </summary>
        public void SaveCurrentHeroSkills()
        {
            if (_currentHeroSkillData != null)
            {
                _currentHeroSkillData.Save();
            }
        }

        /// <summary>
        /// 应用更改：将脏数据持久化到 SkillConfigManager（由"确认应用"按钮触发）
        /// </summary>
        public void ExecuteApply()
        {
            if (!_isDirty) return;

            SaveCurrentHeroSkills();
            IsDirty = false;
        }

        /// <summary>
        /// 撤销当前英雄的所有未保存更改，恢复为配置中的原始状态
        /// </summary>
        public void ExecuteUndoChanges()
        {
            if (!_isDirty) return;

            if (CurrentHero != null)
                LoadSkillsForHero(CurrentHero.HeroId);

            IsDirty = false;
        }

        // ---- 刷新与关闭 ----

        /// <summary>
        /// 刷新队伍列表（英雄增删后调用）
        /// </summary>
        public void RefreshRoster()
        {
            string previouslySelectedId = CurrentHeroId;
            PopulateRoster();

            // 尝试恢复之前的选中
            if (!string.IsNullOrEmpty(previouslySelectedId))
            {
                foreach (var hero in Roster)
                {
                    if (hero.HeroId == previouslySelectedId)
                    {
                        SelectHero(hero);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 关闭技能界面
        /// </summary>
        public void ExecuteClose()
        {
            _onClose?.Invoke();
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void OnFinalize()
        {
            _onClose = null;
            _currentHeroSkillData = null;
        }
    }
}
