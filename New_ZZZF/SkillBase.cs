using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.CampaignSystem.CampaignOptions;

namespace New_ZZZF
{
    /// <summary>
    /// 技能类型枚举
    /// </summary>
    public enum SkillType
    {
        MainActive,    // 主主动技能（E键）
        SubActive,     // 副主动技能（左Alt）
        Passive,       // 被动技能
        Spell,         // 法术栏技能
        CombatArt      // 战技栏技能
    }

    /// <summary>
    /// 技能抽象基类（所有具体技能必须继承此类）
    /// </summary>
    public abstract class SkillBase
    {
        // 新增：技能可附加的状态（通过XML配置）
        public List<AgentBuff> LinkedStates { get; protected set; } = new List<AgentBuff>();
        // ========== 基础属性 ==========
        public string SkillID { get; protected set; } // 技能唯一标识符
        public SkillType Type { get; protected set; } // 技能类型
        public float Cooldown { get; protected set; } // 冷却时间（秒）
        public float ResourceCost { get; protected set; } // 资源消耗（法力/耐力）
        public List<SkillDifficulty> Difficulty { get; protected set; } //使用难度，影响角色是否可以装备该技能。可以为空，可以有多个（任意满足一个即可装备）

        // ========== 核心方法 ==========
        /// <summary>
        /// 激活技能的主逻辑（必须由子类实现）
        /// 激活后返回true，无法使用则为false
        /// </summary>
        public abstract bool Activate(Agent agent);

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
        /// 技能激活条件检查（可被子类重写）
        /// </summary>
        public virtual bool CheckCondition(Agent agent)
        {
            // 默认条件：Agent存活且非坐骑
            return agent.IsActive() && !agent.IsMount;
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