using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace New_ZZZF
{
    // =========================================================================
    // 任务一：数据模型定义 (Model)
    //
    // 核心理念：UI层纯数据模型，与 ItemObject/TextObject/SPItemVM 解耦。
    // 通过 FromGameData 静态工厂方法桥接 SkillBase → SkillUIData，
    // 通过 HeroSkillData 桥接 SkillSet → UI层可绑定数据。
    //
    // v2 新增：
    //   - TargetType 枚举：区分队伍成员/兵种模板/领主NPC三种操作目标
    //   - SkillProficiencyVM：原生技能熟练度展示
    // =========================================================================

    /// <summary>
    /// 技能操作目标类型
    /// </summary>
    public enum TargetType
    {
        PartyMember,   // 队伍成员（默认显示）
        TroopTemplate, // 兵种模板（调试模式解锁）
        LordNPC        // 领主NPC（调试模式解锁）
    }


    /// <summary>
    /// UI层技能难度信息 —— 纯数据，从 SkillDifficulty 提取，供 Gauntlet 绑定
    /// </summary>
    public class SkillDiffInfo
    {
        public int Difficulty { get; set; }
        public string UseAttribute { get; set; } = string.Empty;

        public SkillDiffInfo() { }

        public SkillDiffInfo(int difficulty, string useAttribute)
        {
            Difficulty = difficulty;
            UseAttribute = useAttribute ?? string.Empty;
        }

        /// <summary>
        /// 从游戏逻辑层 SkillDifficulty 创建UI数据
        /// </summary>
        public static SkillDiffInfo FromGameData(SkillDifficulty diff)
        {
            if (diff == null) return null;
            return new SkillDiffInfo(diff.Difficulty, diff.UseAttribute);
        }
    }


    /// <summary>
    /// UI层技能展示数据 —— 纯数据类。
    /// 不持有 ItemObject / TextObject / SPItemVM 引用，
    /// 所有显示字段均为 string/int/float/enum，Gauntlet 可直接绑定。
    /// </summary>
    public class SkillUIData
    {
        /// <summary>技能唯一标识符，对应 SkillBase.SkillID</summary>
        public string SkillId { get; set; } = string.Empty;

        /// <summary>显示名称，从 SkillBase.Text 提取</summary>
        public string SkillName { get; set; } = string.Empty;

        /// <summary>技能描述文本，从 SkillBase.Description 提取</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>图标物品ID，对应 ItemObject.StringId，供 SpriteWidget 使用</summary>
        public string IconItemId { get; set; } = string.Empty;

        /// <summary>技能类型</summary>
        public SPSkillType Type { get; set; } = SPSkillType.None;

        /// <summary>冷却时间（秒）</summary>
        public float Cooldown { get; set; }

        /// <summary>资源消耗（法力/耐力）</summary>
        public float ResourceCost { get; set; }

        /// <summary>使用难度列表</summary>
        public List<SkillDiffInfo> Difficulties { get; set; } = new List<SkillDiffInfo>();

        /// <summary>是否为空技能（NullSkill 占位）</summary>
        public bool IsEmpty => SkillId == "NullSkill" || string.IsNullOrEmpty(SkillId);

        /// <summary>空技能全局单例（始终返回同一实例，避免重复分配）</summary>
        public static readonly SkillUIData Empty = new SkillUIData
        {
            SkillId = "NullSkill",
            SkillName = "空",
            Type = SPSkillType.None
        };

        /// <summary>
        /// 从 SkillBase 游戏逻辑对象创建 UI 展示数据
        /// </summary>
        public static SkillUIData FromSkillBase(SkillBase skill)
        {
            if (skill == null) return Empty;

            var data = new SkillUIData
            {
                SkillId = skill.SkillID ?? string.Empty,
                SkillName = skill.Text?.ToString() ?? skill.SkillID ?? string.Empty,
                Description = skill.Description?.ToString() ?? string.Empty,
                IconItemId = skill.Item?.StringId ?? skill.SkillID ?? string.Empty,
                Type = skill.Type,
                Cooldown = skill.Cooldown,
                ResourceCost = skill.ResourceCost,
                Difficulties = skill.Difficulty?.Select(d => SkillDiffInfo.FromGameData(d)).ToList()
                    ?? new List<SkillDiffInfo>()
            };

            return data;
        }
    }


    /// <summary>
    /// 英雄技能装备配置 —— 某个 Hero 当前装备到各槽位的技能集合。
    /// 从 SkillSet（游戏逻辑层）读取，转换为 UI 可绑定数据。
    /// 保存时写回 SkillConfigManager。
    /// </summary>
    public class HeroSkillData
    {
        /// <summary>英雄ID（对应 CharacterObject.StringId）</summary>
        public string HeroId { get; set; } = string.Empty;

        /// <summary>主主动技能槽</summary>
        public SkillUIData MainActive { get; set; }

        /// <summary>副主动技能槽</summary>
        public SkillUIData SubActive { get; set; }

        /// <summary>被动技能槽</summary>
        public SkillUIData Passive { get; set; }

        /// <summary>战技槽</summary>
        public SkillUIData CombatArt { get; set; }

        /// <summary>法术栏（4槽位）</summary>
        public SkillUIData[] Spells { get; } = new SkillUIData[4];

        /// <summary>
        /// 默认构造函数 —— 所有槽位初始化为空技能
        /// </summary>
        public HeroSkillData()
        {
            var empty = SkillUIData.Empty;
            MainActive = empty;
            SubActive = empty;
            Passive = empty;
            CombatArt = empty;
            for (int i = 0; i < 4; i++)
                Spells[i] = empty;
        }

        /// <summary>
        /// 从游戏逻辑层 SkillSet 创建 UI 层 HeroSkillData
        /// </summary>
        public static HeroSkillData FromSkillSet(string heroId, SkillSet skillSet)
        {
            var data = new HeroSkillData { HeroId = heroId };

            if (skillSet != null)
            {
                data.MainActive = SkillUIData.FromSkillBase(skillSet.MainActive);
                data.SubActive = SkillUIData.FromSkillBase(skillSet.SubActive);
                data.Passive = SkillUIData.FromSkillBase(skillSet.Passive);
                data.CombatArt = SkillUIData.FromSkillBase(skillSet.CombatArt);
                for (int i = 0; i < 4 && i < skillSet.Spells.Length; i++)
                    data.Spells[i] = SkillUIData.FromSkillBase(skillSet.Spells[i]);
            }

            return data;
        }

        /// <summary>
        /// 写回游戏逻辑层 SkillSet（供 SkillConfigManager 持久化）
        /// </summary>
        public SkillSet ToSkillSet()
        {
            var set = new SkillSet();

            set.MainActive = ResolveSkill(MainActive);
            set.SubActive = ResolveSkill(SubActive);
            set.Passive = ResolveSkill(Passive);
            set.CombatArt = ResolveSkill(CombatArt);
            for (int i = 0; i < 4; i++)
                set.Spells[i] = ResolveSkill(Spells[i]);

            return set;
        }

        /// <summary>根据 SkillUIData 查找对应 SkillBase，未找到返回 NullSkill</summary>
        private static SkillBase ResolveSkill(SkillUIData uiData)
        {
            if (uiData == null || uiData.IsEmpty)
            {
                SkillFactory._skillRegistry.TryGetValue("NullSkill", out var ns);
                return ns;
            }

            SkillFactory._skillRegistry.TryGetValue(uiData.SkillId, out var skill);
            return skill ?? GetNullSkill();
        }

        private static SkillBase GetNullSkill()
        {
            SkillFactory._skillRegistry.TryGetValue("NullSkill", out var ns);
            return ns;
        }

        // ---- 便捷读写 ----

        /// <summary>
        /// 从 SkillConfigManager 加载指定英雄的技能配置
        /// </summary>
        public static HeroSkillData LoadForHero(string heroId)
        {
            var skillSet = SkillConfigManager.Instance.GetSkillSetForTroop(heroId);
            return FromSkillSet(heroId, skillSet);
        }

        /// <summary>
        /// 保存当前配置到 SkillConfigManager
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(HeroId))
            {
                Debug.PrintError("[New_ZZZF] HeroSkillData.Save aborted: HeroId is null or empty.");
                return;
            }

            SkillConfigManager.Instance.SetSkillSetForTroop(HeroId, ToSkillSet());
        }
    }


    /// <summary>
    /// 全技能目录 —— 从 SkillFactory 注册表加载所有可用技能。
    /// 供技能选择列表使用，支持按类型筛选和ID查找。
    /// </summary>
    public class SkillCatalog
    {
        /// <summary>所有可用技能（不含 NullSkill）</summary>
        public List<SkillUIData> AllSkills { get; private set; } = new List<SkillUIData>();

        private readonly Dictionary<string, SkillUIData> _skillLookup = new Dictionary<string, SkillUIData>();
        private readonly Dictionary<SPSkillType, List<SkillUIData>> _skillsByType = new Dictionary<SPSkillType, List<SkillUIData>>();

        /// <summary>
        /// 从 SkillFactory 注册表加载全部技能
        /// </summary>
        public static SkillCatalog LoadFromFactory()
        {
            var catalog = new SkillCatalog();

            foreach (var kvp in SkillFactory._skillRegistry)
            {
                // 排除 NullSkill 空占位
                if (kvp.Key == "NullSkill") continue;

                var uiData = SkillUIData.FromSkillBase(kvp.Value);
                catalog.AllSkills.Add(uiData);
                catalog._skillLookup[kvp.Key] = uiData;

                if (!catalog._skillsByType.ContainsKey(uiData.Type))
                    catalog._skillsByType[uiData.Type] = new List<SkillUIData>();
                catalog._skillsByType[uiData.Type].Add(uiData);
            }

            return catalog;
        }

        /// <summary>按技能ID查找</summary>
        public SkillUIData GetSkillById(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return SkillUIData.Empty;
            _skillLookup.TryGetValue(skillId, out var result);
            return result ?? SkillUIData.Empty;
        }

        /// <summary>按技能类型筛选</summary>
        public List<SkillUIData> GetSkillsOfType(SPSkillType type)
        {
            _skillsByType.TryGetValue(type, out var list);
            return list ?? new List<SkillUIData>();
        }
    }

    /// <summary>
    /// 原生技能熟练度 ViewModel。
    /// 显示骑砍2原生技能（单手/双手/弓等）的等级数值。
    /// </summary>
    public class SkillProficiencyVM : ViewModel
    {
        private string _skillName;
        private int _value;
        private string _displayText;

        [DataSourceProperty]
        public string SkillName
        {
            get => _skillName;
            set { if (value != _skillName) { _skillName = value; OnPropertyChangedWithValue(value, nameof(SkillName)); } }
        }

        [DataSourceProperty]
        public int Value
        {
            get => _value;
            set { if (value != _value) { _value = value; OnPropertyChangedWithValue(value, nameof(Value)); } }
        }

        [DataSourceProperty]
        public string DisplayText
        {
            get => _displayText;
            set { if (value != _displayText) { _displayText = value; OnPropertyChangedWithValue(value, nameof(DisplayText)); } }
        }

        public SkillProficiencyVM(string skillName, int value)
        {
            _skillName = skillName ?? string.Empty;
            _value = value;
            _displayText = value > 0 ? value.ToString() : "-";
        }

        /// <summary>
        /// 从 Hero 获取所有原生技能熟练度
        /// </summary>
        public static List<SkillProficiencyVM> GetProficiencies(Hero hero)
        {
            var result = new List<SkillProficiencyVM>();
            if (hero == null) return result;

            var skills = GetNativeSkillObjects();
            foreach (var (name, skillObj) in skills)
            {
                int val = 0;
                try { val = Math.Max(0, hero.GetSkillValue(skillObj)); } catch { }
                result.Add(new SkillProficiencyVM(name, val));
            }
            return result;
        }

        /// <summary>
        /// 从 BasicCharacterObject 获取所有原生技能熟练度（用于兵种模板）
        /// </summary>
        public static List<SkillProficiencyVM> GetProficiencies(BasicCharacterObject character)
        {
            var result = new List<SkillProficiencyVM>();
            if (character == null) return result;

            var skills = GetNativeSkillObjects();
            foreach (var (name, skillObj) in skills)
            {
                int val = 0;
                try { val = Math.Max(0, character.GetSkillValue(skillObj)); } catch { }
                result.Add(new SkillProficiencyVM(name, val));
            }
            return result;
        }

        /// <summary>
        /// 获取所有骑砍2原生技能对象（名称, SkillObject）
        /// </summary>
        private static List<(string Name, SkillObject Skill)> GetNativeSkillObjects()
        {
            return new List<(string, SkillObject)>
            {
                ("单手",   DefaultSkills.OneHanded),
                ("双手",   DefaultSkills.TwoHanded),
                ("长杆",   DefaultSkills.Polearm),
                ("弓",     DefaultSkills.Bow),
                ("弩",     DefaultSkills.Crossbow),
                ("投掷",   DefaultSkills.Throwing),
                ("骑术",   DefaultSkills.Riding),
                ("跑动",   DefaultSkills.Athletics),
                ("锻造",   DefaultSkills.Crafting),
                ("侦察",   DefaultSkills.Scouting),
                ("战术",   DefaultSkills.Tactics),
                ("流氓",   DefaultSkills.Roguery),
                ("魅力",   DefaultSkills.Charm),
                ("统御",   DefaultSkills.Leadership),
                ("交易",   DefaultSkills.Trade),
                ("管理",   DefaultSkills.Steward),
                ("医术",   DefaultSkills.Medicine),
                ("工程",   DefaultSkills.Engineering),
            };
        }
    }
}
