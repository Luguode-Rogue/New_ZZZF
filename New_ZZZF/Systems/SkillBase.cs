using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.CampaignSystem.CampaignOptions;

namespace New_ZZZF
{
    /// <summary>
    /// 技能类型枚举
    /// </summary>
    public enum SkillType
    {
        None = 0,
        /// <summary>
        /// 主主动技能
        /// </summary>
        MainActive,
        /// <summary>
        /// 副主动技能
        /// </summary>
        SubActive,
        /// <summary>
        /// 被动技能
        /// </summary>
        Passive,
        /// <summary>
        /// 法术栏技能
        /// </summary>
        Spell,
        /// <summary>
        /// 战技栏技能
        /// </summary>
        CombatArt,
        /// <summary>
        /// 可放在法术栏的被动
        /// </summary>
        Passive_Spell,//
        /// <summary>
        /// 可放在法术栏的战技
        /// </summary>
        CombatArt_Spell,//
        /// <summary>
        /// 可放在战技栏的法术
        /// </summary>
        Spell_CombatArt//
    }

    /// <summary>
    /// 技能抽象基类（所有具体技能必须继承此类）
    /// </summary>
    public abstract class SkillBase
    {

        // ========== 基础属性 ==========
        /// <summary>
        /// 技能唯一标识符
        /// </summary>
        public string SkillID { get; protected set; } // 
        /// <summary>
        /// 技能类型
        /// </summary>
        public SkillType Type { get; protected set; } // 
        /// <summary>
        /// 冷却时间（秒）
        /// </summary>
        public float Cooldown { get; protected set; } // 
        /// <summary>
        /// 资源消耗（法力/耐力）
        /// </summary>
        public float ResourceCost { get; protected set; } // 
        /// <summary>
        /// 使用难度，影响角色是否可以装备该技能。可以为空，可以有多个（任意满足一个即可装备）
        /// </summary>
        public List<SkillDifficulty> Difficulty { get; protected set; } //
        /// <summary>
        /// 技能对应的显示物品
        /// </summary>
        public ItemObject Item { get;  set; } //
        /// <summary>
        /// 技能对应的物品名称，用于创建物品
        /// </summary>
        public TextObject Text { get;  set; } //
        /// <summary>
        /// 物品说明-技能说明
        /// </summary>
        public TextObject Description { get; set; } 
        // ========== 核心方法 ==========
        /// <summary>
        /// 激活技能的主逻辑（必须由子类实现）
        /// 激活后返回true，无法使用则为false
        /// </summary>
        public abstract bool Activate(Agent casterAgent);

        // ========== 被动技能专用 ==========
        /// <summary>
        /// 当技能被装备时触发（用于被动技能初始化）
        /// </summary>
        public virtual void OnEquip(Agent agent)
        {
            // 示例：注册事件监听
            // agent.OnAttack += HandleAttack;
        }

        /// <summary>
        /// 当技能被卸下时触发（用于被动技能清理）
        /// </summary>
        public virtual void OnUnequip(Agent agent)
        {
            // 示例：注销事件监听
            // agent.OnAttack -= HandleAttack;
        }

        // ========== 条件检查 ==========
        /// <summary>
        /// 技能激活条件检查（可被子类重写）用于士兵ai
        /// </summary>
        public virtual bool CheckCondition(Agent caster)
        {
            // 默认条件：Agent存活且非坐骑
            return caster.IsActive() && !caster.IsMount;
        }
    }

    public class SkillDifficulty
    {
        public int Difficulty { get; set; }
        public String UseAttribute { get; set; }
        public SkillDifficulty(int difficulty, String UseAttribute)
        {
            this.Difficulty = difficulty;
            this.UseAttribute = UseAttribute;
        }
    }
}