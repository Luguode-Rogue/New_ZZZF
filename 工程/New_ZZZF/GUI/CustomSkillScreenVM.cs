using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace New_ZZZF
{
    // =========================================================================
    // v2 视图模型架构
    //
    // HeroVM            — 目标列表项（队伍成员/兵种模板/领主NPC通用）
    // SkillSlotVM       — 单个技能槽位（不变）
    // CustomSkillScreenVM — 主界面 ViewModel
    //
    // v2 新增：
    //   - TargetType 三模式切换（队伍/兵种/领主）
    //   - DebugMode（F12切换，解锁兵种模板+领主NPC）
    //   - 熟练度面板 MBBindingList<SkillProficiencyVM>
    //   - 原位技能目录 MBBindingList<SkillItemVM>（替代弹窗）
    //   - IsInCatalogView 显隐切换
    //
    // 不依赖 InventoryLogic，不与物品系统耦合。
    // =========================================================================


    /// <summary>
    /// 目标列表项 ViewModel —— 通用角色选择器。
    /// v2 扩展：支持 Hero（队伍成员/领主NPC）和 CharacterObject（兵种模板）两种来源。
    /// </summary>
    public class HeroVM : ViewModel
    {
        private string _heroId;
        private string _heroName;
        private string _subtitle;           // v2: 等级/类型标注
        private bool _isSelected;
        private readonly Action<HeroVM> _onSelect;

        /// <summary>关联的 Hero（队伍成员/领主NPC时非null）</summary>
        public readonly Hero Hero;

        /// <summary>关联的 CharacterObject（兵种模板时非null）</summary>
        public readonly BasicCharacterObject Character;

        // ---- 队伍成员/领主NPC 构造函数 ----
        public HeroVM(Hero hero, Action<HeroVM> onSelect)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            Character = hero.CharacterObject;
            _heroId = hero.CharacterObject?.StringId ?? hero.Name?.ToString() ?? string.Empty;
            _heroName = hero.Name?.ToString() ?? hero.CharacterObject?.Name?.ToString() ?? _heroId;
            _subtitle = $"Lv.{hero.Level}";
            _isSelected = false;
            _onSelect = onSelect;
        }

        // ---- 兵种模板构造函数 (v2) ----
        public HeroVM(CharacterObject character, Action<HeroVM> onSelect)
        {
            Hero = null;
            Character = character ?? throw new ArgumentNullException(nameof(character));
            _heroId = character.StringId ?? string.Empty;
            _heroName = character.Name?.ToString() ?? _heroId;
            _subtitle = character.IsBasicTroop ? "基础兵种" : "升级兵种";
            _isSelected = false;
            _onSelect = onSelect;
        }

        /// <summary>目标标识（CharacterObject.StringId）</summary>
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

        /// <summary>目标显示名称</summary>
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

        /// <summary>v2: 副标题（等级/兵种类型）</summary>
        [DataSourceProperty]
        public string Subtitle
        {
            get => _subtitle;
            set
            {
                if (value != _subtitle)
                {
                    _subtitle = value;
                    OnPropertyChangedWithValue(value, nameof(Subtitle));
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

        /// <summary>点击选择此目标（由 Gauntlet ButtonWidget.Command.Click 触发）</summary>
        public void ExecuteSelect()
        {
            _onSelect?.Invoke(this);
        }
    }


    /// <summary>
    /// 技能槽位 ViewModel —— 表示 MainActive / SubActive / Passive / CombatArt / Spell 等槽位。
    /// 每个槽位可以装备一个 SkillUIData（或空）。
    /// v2: 新增 IsActiveSlot 属性（目录视图打开时标识当前编辑的槽位）。
    /// </summary>
    public class SkillSlotVM : ViewModel
    {
        private string _slotId;
        private string _slotLabel;
        private SkillUIData _skill;
        private Action<SkillSlotVM> _onClickSlot;
        private bool _isActiveSlot;

        /// <summary>槽位标识符，如 "MainActive", "Spell2"</summary>
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
            set { /* 只读，setter 满足 DataSourceProperty 模式 */ }
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

        /// <summary>此槽位是否已装备技能（= !IsEmpty，供XML绑定）</summary>
        [DataSourceProperty]
        public bool IsEquipped => _skill != null && !_skill.IsEmpty;

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

        /// <summary>v2: 是否为当前编辑中的槽位（目录视图打开时高亮）</summary>
        [DataSourceProperty]
        public bool IsActiveSlot
        {
            get => _isActiveSlot;
            set
            {
                if (value != _isActiveSlot)
                {
                    _isActiveSlot = value;
                    OnPropertyChangedWithValue(value, nameof(IsActiveSlot));
                }
            }
        }

        /// <summary>槽位对应的技能类型（用于过滤可选技能列表）</summary>
        public SPSkillType SlotFilterType { get; private set; } = SPSkillType.None;

        /// <summary>当前装备的技能数据（非绑定，供逻辑层读写）</summary>
        public SkillUIData Skill => _skill;

        /// <summary>
        /// 创建一个技能槽位
        /// </summary>
        public SkillSlotVM(string slotId, string slotLabel, SPSkillType filterType, Action<SkillSlotVM> onClickSlot)
        {
            _slotId = slotId ?? string.Empty;
            _slotLabel = slotLabel ?? string.Empty;
            SlotFilterType = filterType;
            _onClickSlot = onClickSlot;
            _skill = SkillUIData.Empty;
            _isActiveSlot = false;
        }

        /// <summary>设置槽位中的技能（并刷新绑定属性）</summary>
        public void SetSkill(SkillUIData skillData)
        {
            _skill = skillData ?? SkillUIData.Empty;
            OnPropertyChangedWithValue(_skill.SkillName, nameof(SkillName));
            OnPropertyChangedWithValue(_skill.IconItemId, nameof(SkillIcon));
            OnPropertyChangedWithValue(_skill.IsEmpty, nameof(IsEmpty));
            OnPropertyChangedWithValue(!_skill.IsEmpty, nameof(IsEquipped));
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
    /// 技能界面主 ViewModel (v2)。
    /// 
    /// 数据绑定关系：
    ///   CustomSkillScreen (Gauntlet XML)
    ///     ├── DataSource="{Roster}"           → MBBindingList&lt;HeroVM&gt;  (当前目标类型的列表)
    ///     ├── DataSource="{TroopTemplates}"   → MBBindingList&lt;HeroVM&gt;  (兵种模板，调试模式)
    ///     ├── DataSource="{LordNPCs}"         → MBBindingList&lt;HeroVM&gt;  (领主NPC，调试模式)
    ///     ├── DataSource="{Skills}"           → MBBindingList&lt;SkillSlotVM&gt;
    ///     ├── DataSource="{Proficiencies}"    → MBBindingList&lt;SkillProficiencyVM&gt;
    ///     └── DataSource="{CatalogItems}"     → MBBindingList&lt;SkillItemVM&gt; (原位目录)
    /// 
    /// 生命周期：
    ///   构造 → 加载 SkillCatalog → 填充 Roster/TroopTemplates/LordNPCs →
    ///   创建技能槽 → 选中默认目标 → 加载技能+熟练度
    ///   切换目标 → SelectTarget() → LoadSkillsForTarget() + LoadProficiencies()
    ///   点击槽位 → 打开目录视图 → IsInCatalogView = true
    /// </summary>
    public class CustomSkillScreenVM : ViewModel
    {
        // =====================================================================
        // 绑定属性 (v1.1 保留)
        // =====================================================================

        private MBBindingList<HeroVM> _roster;
        private MBBindingList<SkillSlotVM> _skills;
        private HeroVM _currentHero;
        private string _currentHeroId;

        /// <summary>当前目标类型的角色列表</summary>
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

        /// <summary>当前目标的技能槽位列表（8个槽位）</summary>
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

        /// <summary>当前选中的目标 VM</summary>
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

        /// <summary>当前目标 ID（便捷属性）</summary>
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

        /// <summary>是否有未保存的更改</summary>
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

        // =====================================================================
        // v2 新增绑定属性
        // =====================================================================

        private bool _debugMode;
        private TargetType _currentTargetType = TargetType.PartyMember;
        private MBBindingList<SkillProficiencyVM> _proficiencies;
        private MBBindingList<SkillItemVM> _catalogItems;
        private bool _isInCatalogView;
        private SkillSlotVM _activeSlot;
        private MBBindingList<HeroVM> _troopTemplates;
        private MBBindingList<HeroVM> _lordNPCs;
        private string _searchText = string.Empty;
        private int _catalogSelectedIndex = -1;
        private int _catalogGridColumns = 4;

        /// <summary>v2: 调试模式（F12切换，解锁兵种模板+领主NPC）</summary>
        [DataSourceProperty]
        public bool DebugMode
        {
            get => _debugMode;
            set
            {
                if (value != _debugMode)
                {
                    _debugMode = value;
                    OnPropertyChangedWithValue(value, nameof(DebugMode));
                    // 切换调试模式时刷新左侧列表
                    RefreshTargetLists();
                }
            }
        }

        /// <summary>v2: 当前目标类型（Tab循环）</summary>
        [DataSourceProperty]
        public int CurrentTargetTypeInt
        {
            get => (int)_currentTargetType;
            set
            {
                var newType = (TargetType)value;
                if (newType != _currentTargetType && (newType == TargetType.PartyMember || _debugMode))
                {
                    _currentTargetType = newType;
                    OnPropertyChangedWithValue(value, nameof(CurrentTargetTypeInt));
                    SwitchTargetType(_currentTargetType);
                }
            }
        }

        /// <summary>当前目标类型（C#枚举，非绑定）</summary>
        public TargetType CurrentTargetType
        {
            get => _currentTargetType;
            private set
            {
                if (value != _currentTargetType)
                {
                    _currentTargetType = value;
                    OnPropertyChangedWithValue((int)value, nameof(CurrentTargetTypeInt));
                }
            }
        }

        /// <summary>v2: 目标类型显示文本</summary>
        [DataSourceProperty]
        public string TargetTypeText
        {
            get => _currentTargetType switch
            {
                TargetType.PartyMember => "队伍成员",
                TargetType.TroopTemplate => "兵种模板",
                TargetType.LordNPC => "领主NPC",
                _ => "未知"
            };
        }

        /// <summary>v2: 原生技能熟练度列表</summary>
        [DataSourceProperty]
        public MBBindingList<SkillProficiencyVM> Proficiencies
        {
            get => _proficiencies;
            set
            {
                if (value != _proficiencies)
                {
                    _proficiencies = value;
                    OnPropertyChangedWithValue(value, nameof(Proficiencies));
                }
            }
        }

        /// <summary>v2: 技能目录项列表（原位网格视图）</summary>
        [DataSourceProperty]
        public MBBindingList<SkillItemVM> CatalogItems
        {
            get => _catalogItems;
            set
            {
                if (value != _catalogItems)
                {
                    _catalogItems = value;
                    OnPropertyChangedWithValue(value, nameof(CatalogItems));
                }
            }
        }

        /// <summary>v2: 是否在技能目录视图（原位切换，替代弹窗）</summary>
        [DataSourceProperty]
        public bool IsInCatalogView
        {
            get => _isInCatalogView;
            set
            {
                if (value != _isInCatalogView)
                {
                    _isInCatalogView = value;
                    OnPropertyChangedWithValue(value, nameof(IsInCatalogView));
                    // 同步逆属性（用于XML显隐绑定：GauntletUI不支持!@否定语法）
                    OnPropertyChangedWithValue(!value, nameof(IsShowingSlots));
                }
            }
        }

        /// <summary>v2: 是否显示技能槽视图（= !IsInCatalogView，供XML绑定）</summary>
        [DataSourceProperty]
        public bool IsShowingSlots => !_isInCatalogView;

        /// <summary>v2: 当前编辑中的技能槽位（目录视图的上下文）</summary>
        [DataSourceProperty]
        public SkillSlotVM ActiveSlot
        {
            get => _activeSlot;
            set
            {
                if (value != _activeSlot)
                {
                    // 取消旧槽位的高亮
                    if (_activeSlot != null)
                        _activeSlot.IsActiveSlot = false;
                    _activeSlot = value;
                    // 高亮新槽位
                    if (_activeSlot != null)
                        _activeSlot.IsActiveSlot = true;
                    OnPropertyChangedWithValue(value, nameof(ActiveSlot));
                }
            }
        }

        /// <summary>v2: 兵种模板列表（调试模式显示）</summary>
        [DataSourceProperty]
        public MBBindingList<HeroVM> TroopTemplates
        {
            get => _troopTemplates;
            set
            {
                if (value != _troopTemplates)
                {
                    _troopTemplates = value;
                    OnPropertyChangedWithValue(value, nameof(TroopTemplates));
                }
            }
        }

        /// <summary>v2: 领主NPC列表（调试模式显示）</summary>
        [DataSourceProperty]
        public MBBindingList<HeroVM> LordNPCs
        {
            get => _lordNPCs;
            set
            {
                if (value != _lordNPCs)
                {
                    _lordNPCs = value;
                    OnPropertyChangedWithValue(value, nameof(LordNPCs));
                }
            }
        }

        /// <summary>v2: 搜索文本（目录过滤）</summary>
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
                    FilterCatalog();
                }
            }
        }

        /// <summary>v2: 目录视图下键盘导航高亮索引</summary>
        public int CatalogSelectedIndex => _catalogSelectedIndex;

        /// <summary>v2: 目录网格列数（供XML布局参考）</summary>
        [DataSourceProperty]
        public int CatalogGridColumns
        {
            get => _catalogGridColumns;
            set
            {
                if (value != _catalogGridColumns)
                {
                    _catalogGridColumns = value;
                    OnPropertyChangedWithValue(value, nameof(CatalogGridColumns));
                }
            }
        }

        // =====================================================================
        // 废弃属性 (v2: 不再使用弹窗，保留属性避免XML绑定报错)
        // =====================================================================

        [DataSourceProperty]
        public object SkillSelectionPopup
        {
            get => null;
            set { /* v2: 弹窗已废弃 */ }
        }

        /// <summary>v2: 兼容旧接口，始终为false</summary>
        public bool IsPopupOpen => false;

        // =====================================================================
        // 非绑定字段
        // =====================================================================

        /// <summary>全技能目录</summary>
        public readonly SkillCatalog Catalog;

        /// <summary>当前目标的技能配置（可编辑副本）</summary>
        private HeroSkillData _currentHeroSkillData;

        /// <summary>脏标记</summary>
        private bool _isDirty;

        /// <summary>关闭回调</summary>
        private Action _onClose;

        /// <summary>槽位ID → SkillSlotVM 快速查找</summary>
        private readonly Dictionary<string, SkillSlotVM> _slotMap = new Dictionary<string, SkillSlotVM>();

        /// <summary>所有可用技能的 SkillItemVM 缓存（避免重复创建）</summary>
        private readonly List<SkillItemVM> _allSkillItemVMs = new List<SkillItemVM>();

        // =====================================================================
        // 构造函数
        // =====================================================================

        public CustomSkillScreenVM()
        {
            Catalog = SkillCatalog.LoadFromFactory();

            Roster = new MBBindingList<HeroVM>();
            Skills = new MBBindingList<SkillSlotVM>();
            Proficiencies = new MBBindingList<SkillProficiencyVM>();
            CatalogItems = new MBBindingList<SkillItemVM>();
            TroopTemplates = new MBBindingList<HeroVM>();
            LordNPCs = new MBBindingList<HeroVM>();

            // 预创建所有技能项 VM（复用，不每次重建）
            BuildAllSkillItemVMs();

            // 创建固定槽位
            CreateSkillSlots();

            // 填充所有目标列表
            PopulateRoster();
            PopulateTroopTemplates();
            PopulateLordNPCs();
        }

        // =====================================================================
        // 初始化方法
        // =====================================================================

        /// <summary>预创建所有技能的 SkillItemVM</summary>
        private void BuildAllSkillItemVMs()
        {
            _allSkillItemVMs.Clear();
            if (Catalog?.AllSkills == null) return;
            foreach (var skill in Catalog.AllSkills)
            {
                _allSkillItemVMs.Add(new SkillItemVM(skill, OnCatalogSkillSelected));
            }
        }

        /// <summary>从玩家部队填充 Roster 列表</summary>
        private void PopulateRoster()
        {
            Roster.Clear();
            var playerClan = Clan.PlayerClan;
            if (playerClan == null) return;

            int comeOfAge = Campaign.Current.Models?.AgeModel?.HeroComesOfAge ?? 18;
            foreach (var hero in playerClan.Heroes)
            {
                if (hero == null) continue;
                if (!hero.IsAlive) continue;
                if (hero.Age < comeOfAge) continue;
                if (hero.HeroState != Hero.CharacterStates.Active && hero != Hero.MainHero) continue;
                Roster.Add(new HeroVM(hero, OnTargetSelected));
            }

            if (Roster.Count > 0)
            {
                SelectTarget(Roster[0]);
            }
        }

        /// <summary>v2: 填充兵种模板列表（调试模式）</summary>
        private void PopulateTroopTemplates()
        {
            TroopTemplates.Clear();
            try
            {
                // 收集所有文化的基础兵种和升级兵种
                var addedIds = new HashSet<string>();
                foreach (var culture in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
                {
                    if (culture == null) continue;
                    var basicTroop = culture.BasicTroop;
                    if (basicTroop != null && addedIds.Add(basicTroop.StringId))
                        TroopTemplates.Add(new HeroVM(basicTroop, OnTargetSelected));
                    var eliteTroop = culture.EliteBasicTroop;
                    if (eliteTroop != null && addedIds.Add(eliteTroop.StringId))
                        TroopTemplates.Add(new HeroVM(eliteTroop, OnTargetSelected));
                }
            }
            catch { /* 静默失败，调试模式数据源可能不完整 */ }
        }

        /// <summary>v2: 填充领主NPC列表（调试模式）</summary>
        private void PopulateLordNPCs()
        {
            LordNPCs.Clear();
            try
            {
                foreach (var clan in Clan.All)
                {
                    if (clan == null || clan.IsEliminated) continue;
                    foreach (var hero in clan.Heroes)
                    {
                        if (hero == null || !hero.IsAlive || hero.Age < 18) continue;
                        LordNPCs.Add(new HeroVM(hero, OnTargetSelected));
                    }
                }
            }
            catch { /* 静默失败 */ }
        }

        /// <summary>创建8个技能槽位</summary>
        private void CreateSkillSlots()
        {
            Skills.Clear();
            _slotMap.Clear();

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

        /// <summary>设置关闭回调</summary>
        public void SetCloseAction(Action onClose)
        {
            _onClose = onClose;
        }

        // =====================================================================
        // v2: 目标类型切换
        // =====================================================================

        /// <summary>F12 切换调试模式</summary>
        public void ExecuteToggleDebug()
        {
            DebugMode = !DebugMode;
        }

        /// <summary>Tab 循环目标类型</summary>
        public void ExecuteCycleTargetType()
        {
            if (!DebugMode)
            {
                // 非调试模式只有队伍成员
                CurrentTargetTypeInt = (int)TargetType.PartyMember;
                return;
            }

            int next = ((int)_currentTargetType + 1) % 3;
            CurrentTargetTypeInt = next;
        }

        /// <summary>刷新目标列表显示（响应DebugMode变化）</summary>
        private void RefreshTargetLists()
        {
            // 重新填充列表（构造时已经填充好，这里触发属性通知让XML显隐生效）
            OnPropertyChangedWithValue(TroopTemplates, nameof(TroopTemplates));
            OnPropertyChangedWithValue(LordNPCs, nameof(LordNPCs));

            // 如果当前模式是非调试专属模式且退出调试，回到PartyMember
            if (!DebugMode && _currentTargetType != TargetType.PartyMember)
            {
                SwitchTargetType(TargetType.PartyMember);
            }
        }

        /// <summary>切换到指定目标类型</summary>
        private void SwitchTargetType(TargetType targetType)
        {
            // 更新类型字段并通知绑定（必须在此处更新，确保 TargetTypeText 正确）
            _currentTargetType = targetType;
            OnPropertyChangedWithValue((int)targetType, nameof(CurrentTargetTypeInt));
            OnPropertyChangedWithValue(TargetTypeText, nameof(TargetTypeText));

            // 清空当前选中
            if (CurrentHero != null)
                CurrentHero.IsSelected = false;

            IsInCatalogView = false;
            ActiveSlot = null;

            // 选择对应列表的第一个
            MBBindingList<HeroVM> targetList = GetTargetList(targetType);
            if (targetList != null && targetList.Count > 0)
            {
                SelectTarget(targetList[0]);
            }
        }

        /// <summary>获取当前目标类型的列表</summary>
        private MBBindingList<HeroVM> GetTargetList(TargetType targetType)
        {
            return targetType switch
            {
                TargetType.PartyMember => Roster,
                TargetType.TroopTemplate => TroopTemplates,
                TargetType.LordNPC => LordNPCs,
                _ => Roster
            };
        }

        /// <summary>获取当前目标类型的列表（对于Roster绑定）</summary>
        private MBBindingList<HeroVM> GetCurrentTargetList()
        {
            return GetTargetList(_currentTargetType);
        }

        // =====================================================================
        // 目标选择逻辑
        // =====================================================================

        /// <summary>选中指定目标（由 HeroVM.ExecuteSelect 回调触发）</summary>
        private void OnTargetSelected(HeroVM selectedTarget)
        {
            if (selectedTarget == null) return;
            if (CurrentHero != null)
                CurrentHero.IsSelected = false;

            // 关闭目录视图
            IsInCatalogView = false;
            ActiveSlot = null;

            SelectTarget(selectedTarget);
        }

        private void SelectTarget(HeroVM target)
        {
            IsDirty = false;
            CurrentHero = target;
            CurrentHeroId = target.HeroId;
            target.IsSelected = true;

            // 同步 Roster 绑定到当前目标列表
            var currentList = GetCurrentTargetList();
            if (Roster != currentList)
            {
                Roster = currentList;
            }

            LoadSkillsForTarget(target.HeroId);
            LoadProficiencies(target);
        }

        // =====================================================================
        // 键盘导航：目标列表
        // =====================================================================

        public void SelectNextHero()
        {
            var list = GetCurrentTargetList();
            if (list == null || list.Count <= 1) return;
            int currentIndex = GetCurrentHeroIndex();
            int nextIndex = (currentIndex + 1) % list.Count;
            if (CurrentHero != null)
                CurrentHero.IsSelected = false;
            SelectTarget(list[nextIndex]);
        }

        public void SelectPrevHero()
        {
            var list = GetCurrentTargetList();
            if (list == null || list.Count <= 1) return;
            int currentIndex = GetCurrentHeroIndex();
            int prevIndex = currentIndex - 1;
            if (prevIndex < 0) prevIndex = list.Count - 1;
            if (CurrentHero != null)
                CurrentHero.IsSelected = false;
            SelectTarget(list[prevIndex]);
        }

        private int GetCurrentHeroIndex()
        {
            if (CurrentHero == null) return -1;
            var list = GetCurrentTargetList();
            if (list == null) return -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == CurrentHero) return i;
            }
            return -1;
        }

        // =====================================================================
        // 键盘导航：技能槽位
        // =====================================================================

        /// <summary>键盘 1~8 —— 直接打开对应槽位的技能目录视图</summary>
        public void SelectSlotByIndex(int index)
        {
            if (Skills == null || index < 0 || index >= Skills.Count) return;
            OnSlotClicked(Skills[index]);
        }

        // =====================================================================
        // v2: 废弃弹窗相关方法（保留空壳兼容旧调用）
        // =====================================================================

        public void PopupSelectNextSkill() { /* v2: 弹窗已废弃 */ }
        public void PopupSelectPrevSkill() { /* v2: 弹窗已废弃 */ }
        public void PopupSelectCurrentSkill() { /* v2: 弹窗已废弃 */ }

        // =====================================================================
        // 技能加载与配置
        // =====================================================================

        /// <summary>从 SkillConfigManager 加载指定目标的技能配置</summary>
        private void LoadSkillsForTarget(string targetId)
        {
            _currentHeroSkillData = HeroSkillData.LoadForHero(targetId);

            SetSlotSkill("MainActive", _currentHeroSkillData.MainActive);
            SetSlotSkill("SubActive", _currentHeroSkillData.SubActive);
            SetSlotSkill("Passive", _currentHeroSkillData.Passive);
            SetSlotSkill("CombatArt", _currentHeroSkillData.CombatArt);

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

        /// <summary>v2: 加载当前目标的原生技能熟练度</summary>
        private void LoadProficiencies(HeroVM target)
        {
            Proficiencies.Clear();
            if (target == null) return;

            List<SkillProficiencyVM> profs = null;
            if (target.Hero != null)
            {
                profs = SkillProficiencyVM.GetProficiencies(target.Hero);
            }
            else if (target.Character != null)
            {
                profs = SkillProficiencyVM.GetProficiencies(target.Character);
            }

            if (profs != null)
            {
                foreach (var p in profs)
                    Proficiencies.Add(p);
            }
        }

        // =====================================================================
        // v2: 原位技能目录视图（替代弹窗）
        // =====================================================================

        /// <summary>
        /// 点击技能槽位 —— 打开原位技能目录视图。
        /// v2: 不再创建弹窗，改为设置 ActiveSlot + 切换 IsInCatalogView。
        /// </summary>
        private void OnSlotClicked(SkillSlotVM slotVM)
        {
            if (slotVM == null || Catalog == null) return;

            // 设置当前编辑槽位
            ActiveSlot = slotVM;

            // 过滤 + 填充目录项
            PopulateCatalogForSlot(slotVM);

            // 切换到目录视图
            IsInCatalogView = true;
            _catalogSelectedIndex = -1;
        }

        /// <summary>根据当前槽位过滤并填充目录项列表</summary>
        private void PopulateCatalogForSlot(SkillSlotVM slotVM)
        {
            CatalogItems.Clear();
            if (slotVM == null) return;

            var filterType = slotVM.SlotFilterType;
            string searchLower = (_searchText ?? string.Empty).Trim().ToLower();

            foreach (var itemVM in _allSkillItemVMs)
            {
                // 类型过滤
                if (itemVM.SkillData.Type != filterType && filterType != SPSkillType.None)
                    continue;

                // 搜索过滤
                if (!string.IsNullOrEmpty(searchLower))
                {
                    if (!itemVM.SkillName.ToLower().Contains(searchLower) &&
                        !itemVM.Description.ToLower().Contains(searchLower))
                        continue;
                }

                CatalogItems.Add(itemVM);
            }
        }

        /// <summary>搜索过滤目录</summary>
        private void FilterCatalog()
        {
            if (ActiveSlot == null) return;
            PopulateCatalogForSlot(ActiveSlot);
            _catalogSelectedIndex = -1;
        }

        /// <summary>在目录中选择技能（由 SkillItemVM 点击触发）</summary>
        private void OnCatalogSkillSelected(SkillUIData skillData)
        {
            if (ActiveSlot == null) return;
            AssignSkillToSlot(ActiveSlot, skillData);
            IsInCatalogView = false;
            ActiveSlot = null;
        }

        /// <summary>键盘 Enter：选择当前高亮的目录项</summary>
        public void ExecuteSelectFromCatalog()
        {
            if (CatalogItems == null || _catalogSelectedIndex < 0 || _catalogSelectedIndex >= CatalogItems.Count)
                return;

            var item = CatalogItems[_catalogSelectedIndex];
            if (item != null)
                OnCatalogSkillSelected(item.SkillData);
        }

        /// <summary>键盘 Esc：关闭目录视图（返回技能槽视图）</summary>
        public void ExecuteCloseCatalog()
        {
            IsInCatalogView = false;
            ActiveSlot = null;
            _catalogSelectedIndex = -1;
        }

        /// <summary>目录键盘 ↓ —— 选择下一项</summary>
        public void SelectNextCatalogItem()
        {
            if (CatalogItems == null || CatalogItems.Count == 0) return;
            _catalogSelectedIndex = (_catalogSelectedIndex + 1) % CatalogItems.Count;
            RefreshCatalogHighlight();
        }

        /// <summary>目录键盘 ↑ —— 选择上一项</summary>
        public void SelectPrevCatalogItem()
        {
            if (CatalogItems == null || CatalogItems.Count == 0) return;
            _catalogSelectedIndex--;
            if (_catalogSelectedIndex < 0) _catalogSelectedIndex = CatalogItems.Count - 1;
            RefreshCatalogHighlight();
        }

        /// <summary>目录键盘 ← —— 选择上一列项（左移一列）</summary>
        public void SelectPrevCatalogRow()
        {
            if (CatalogItems == null || CatalogItems.Count == 0) return;
            int cols = Math.Max(1, _catalogGridColumns);
            _catalogSelectedIndex -= cols;
            if (_catalogSelectedIndex < 0)
            {
                // 循环：跳到当前列的最后
                int col = (CatalogItems.Count % cols + cols) % cols;
                _catalogSelectedIndex = CatalogItems.Count - 1 - ((CatalogItems.Count - 1) % cols + col) % cols;
                if (_catalogSelectedIndex < 0) _catalogSelectedIndex = CatalogItems.Count - 1;
            }
            RefreshCatalogHighlight();
        }

        /// <summary>目录键盘 → —— 选择下一列项（右移一列）</summary>
        public void SelectNextCatalogRow()
        {
            if (CatalogItems == null || CatalogItems.Count == 0) return;
            int cols = Math.Max(1, _catalogGridColumns);
            _catalogSelectedIndex += cols;
            if (_catalogSelectedIndex >= CatalogItems.Count)
            {
                // 循环：跳到下一列的第一个
                _catalogSelectedIndex = (_catalogSelectedIndex - CatalogItems.Count) % cols;
            }
            RefreshCatalogHighlight();
        }

        /// <summary>刷新目录项的高亮状态</summary>
        private void RefreshCatalogHighlight()
        {
            for (int i = 0; i < CatalogItems.Count; i++)
            {
                CatalogItems[i].IsHighlighted = (i == _catalogSelectedIndex);
            }
        }

        // =====================================================================
        // 技能分配与保存
        // =====================================================================

        /// <summary>将指定技能分配给槽位</summary>
        public void AssignSkillToSlot(SkillSlotVM slotVM, SkillUIData skillData)
        {
            if (slotVM == null) return;
            slotVM.SetSkill(skillData);

            if (_currentHeroSkillData != null)
            {
                switch (slotVM.SlotId)
                {
                    case "MainActive": _currentHeroSkillData.MainActive = skillData; break;
                    case "SubActive": _currentHeroSkillData.SubActive = skillData; break;
                    case "Passive": _currentHeroSkillData.Passive = skillData; break;
                    case "CombatArt": _currentHeroSkillData.CombatArt = skillData; break;
                    case "Spell0": if (_currentHeroSkillData.Spells.Length > 0) _currentHeroSkillData.Spells[0] = skillData; break;
                    case "Spell1": if (_currentHeroSkillData.Spells.Length > 1) _currentHeroSkillData.Spells[1] = skillData; break;
                    case "Spell2": if (_currentHeroSkillData.Spells.Length > 2) _currentHeroSkillData.Spells[2] = skillData; break;
                    case "Spell3": if (_currentHeroSkillData.Spells.Length > 3) _currentHeroSkillData.Spells[3] = skillData; break;
                }
            }

            IsDirty = true;
        }

        public void ClearSkillSlot(SkillSlotVM slotVM)
        {
            AssignSkillToSlot(slotVM, SkillUIData.Empty);
        }

        public void SaveCurrentHeroSkills()
        {
            if (_currentHeroSkillData != null)
            {
                _currentHeroSkillData.Save();
            }
        }

        public void ExecuteApply()
        {
            if (!_isDirty) return;
            SaveCurrentHeroSkills();
            IsDirty = false;
        }

        public void ExecuteUndoChanges()
        {
            if (!_isDirty) return;
            if (CurrentHero != null)
                LoadSkillsForTarget(CurrentHero.HeroId);
            IsDirty = false;
        }

        // =====================================================================
        // 刷新与关闭
        // =====================================================================

        /// <summary>刷新队伍列表</summary>
        public void RefreshRoster()
        {
            string previouslySelectedId = CurrentHeroId;
            PopulateRoster();
            if (!string.IsNullOrEmpty(previouslySelectedId))
            {
                foreach (var target in Roster)
                {
                    if (target.HeroId == previouslySelectedId)
                    {
                        SelectTarget(target);
                        return;
                    }
                }
            }
        }

        public void ExecuteClose()
        {
            _onClose?.Invoke();
        }

        public void OnFinalize()
        {
            _onClose = null;
            _currentHeroSkillData = null;
            _allSkillItemVMs.Clear();
        }
    }
}
