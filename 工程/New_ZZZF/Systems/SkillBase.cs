using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.CampaignSystem.CampaignOptions;

namespace New_ZZZF
{    /// <summary>
     /// 伤害元素类型枚举
     /// </summary>
    public enum DamageType
    {
        None = 0,
        ICE_DAMAGE,
        FIRE_DAMAGE,
        ELECTRICITY_DAMAGE,
        TOXIN_DAMAGE,
        ICE_ENHANCEMENT_FREEZING,
        FIRE_ENHANCEMENT_BLASTING,
        ELECTRICITY_ENHANCEMENT_PARALYZING,
        TOXIN_ENHANCEMENT_CORRUPTING

    }
    /// <summary>
    /// 技能类型枚举
    /// </summary>
    public enum SPSkillType
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

    /// <summary>一次技能发动的费用与个人冷却规则；法术公共冷却仍由统一入口处理。</summary>
    public struct SkillActivationPolicy
    {
        public float ResourceCost;
        public bool IgnoreSkillCooldown;
        public float CooldownOnSuccess;

        public SkillActivationPolicy(float resourceCost, bool ignoreSkillCooldown, float cooldownOnSuccess)
        {
            ResourceCost = resourceCost;
            IgnoreSkillCooldown = ignoreSkillCooldown;
            CooldownOnSuccess = cooldownOnSuccess;
        }
    }

    /// <summary>技能实际伤害区域的水平形状。Sphere 在结算时还检查完整三维距离。</summary>
    public enum SkillDamageAreaShape
    {
        Sphere,
        Capsule,
        SampledCone
    }

    /// <summary>
    /// 可由技能按施法者当前属性实时计算的伤害尺寸。Capsule 的 Length 是中轴线长度，
    /// Radius 是两侧半宽；总长为 Length + 2 * Radius。投射物自身碰撞半径不属于此结构。
    /// </summary>
    public struct SkillDamageArea
    {
        public SkillDamageAreaShape Shape;
        public float Radius;
        public float Length;
        public float HeightTolerance;
        /// <summary>仅 SampledCone 使用：左右最大采样角度。</summary>
        public float HalfAngleDegrees;

        public bool IsValid => Radius > 0f && !float.IsNaN(Radius) && !float.IsInfinity(Radius) &&
            (Shape == SkillDamageAreaShape.Sphere ||
             (Shape == SkillDamageAreaShape.Capsule && Length > 0f &&
              HeightTolerance > 0f && !float.IsNaN(Length) && !float.IsInfinity(Length) &&
              !float.IsNaN(HeightTolerance) && !float.IsInfinity(HeightTolerance)) ||
             (Shape == SkillDamageAreaShape.SampledCone && Length > 0f &&
              HalfAngleDegrees > 0f && HalfAngleDegrees < 180f &&
              !float.IsNaN(Length) && !float.IsInfinity(Length)));
    }

    /// <summary>
    /// 技能抽象基类（所有具体技能必须继承此类）
    /// </summary>
    public abstract class SkillBase
    {
        private bool? _hasAiConditionOverride;

        // ========== 基础属性 ==========
        /// <summary>
        /// 技能唯一标识符
        /// </summary>
        public string SkillID { get; protected set; } // 
        /// <summary>
        /// 技能类型
        /// </summary>
        public SPSkillType Type { get; protected set; } // 
        /// <summary>
        /// 冷却时间（秒）
        /// </summary>
        public float Cooldown { get; protected set; } // 
        /// <summary>
        /// 资源消耗（法力/耐力）
        /// </summary>
        public float ResourceCost { get; protected set; } // 
        /// <summary>发动成功后在施法者位置播放的可选三维音效；失败或仅检查条件时不播放。</summary>
        public string CastSoundEvent { get; protected set; }
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
        /// <summary>最近一次 Activate 返回 false 时的可读原因，供战场 Display 输出。</summary>
        public string LastActivationFailureReason { get; protected set; }

        public void ResetActivationFailureReason()
        {
            LastActivationFailureReason = null;
        }

        protected bool FailActivation(string reason)
        {
            LastActivationFailureReason = reason;
            return false;
        }

        /// <summary>技能是否有效（默认可用；组合法术可重写为“至少一个投射物”）。</summary>
        public virtual bool IsValid => true;
        /// <summary>是否允许在原生攻击、瞄准等动作进行期间发动。默认关闭。</summary>
        public virtual bool CanActivateWhilePerformingAction => false;
        /// <summary>
        /// 发动前读取当前阶段的费用和个人冷却策略。共享的技能实例不得保存施法者的阶段；
        /// 多阶段技能应读取该施法者的状态，并返回本次施法的策略快照。
        /// </summary>
        public virtual SkillActivationPolicy GetActivationPolicy(Agent caster)
        {
            return new SkillActivationPolicy(ResourceCost, false, Cooldown);
        }
        /// <summary>战斗 HUD 识别本技能施加在施法者身上的持续状态；特殊命名可重写。</summary>
        public virtual bool IsHudDurationState(string stateId)
        {
            if (string.IsNullOrEmpty(SkillID) || string.IsNullOrEmpty(stateId))
                return false;
            return string.Equals(stateId, SkillID + "Buff", StringComparison.Ordinal) ||
                   string.Equals(stateId, SkillID + "BuffToSelf", StringComparison.Ordinal) ||
                   string.Equals(stateId, SkillID + "BuffApplyToSelf", StringComparison.Ordinal);
        }
        /// <summary>
        /// 查询当前施法者的第 index 个伤害区域，供 Shift 指示、伤害结算和其他机制共用。
        /// 默认没有区域；未来属性/Buff 改变范围时在技能重写中实时计算，不缓存到共享技能实例。
        /// 多段伤害可以依次提供 index=0、1……，以 false 结束。
        /// </summary>
        public virtual bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area)
        {
            area = default;
            return false;
        }
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
        /// <summary>
        /// 剑气斩之类的技能通过这个函数实现
        /// </summary>
        public virtual void GameEntityDamage(GameEntity missileEntity) { }

        // ========== 条件检查 ==========
        /// <summary>
        /// 技能激活条件检查（可被子类重写）用于士兵ai
        /// </summary>
        public virtual bool CheckCondition(Agent caster)
        {
            if (caster == null || !caster.IsActive() || caster.IsMount)
                return false;

            // 派生技能的 override 常会调用 base.CheckCondition，因此不能简单把默认值改为 false。
            // 按类型只反射一次：没有专门实现 AI 条件的半成品技能不参与自动施法。
            if (!_hasAiConditionOverride.HasValue)
            {
                MethodInfo method = GetType().GetMethod(nameof(CheckCondition), new[] { typeof(Agent) });
                _hasAiConditionOverride = method != null && method.DeclaringType != typeof(SkillBase);
            }
            return _hasAiConditionOverride.Value;
        }

    }

    /// <summary>只允许主动进攻的编队命令触发突进类技能的 NPC 自动施放。</summary>
    internal static class AiBattleOrderGate
    {
        public static bool AllowsAggressiveSkill(Agent agent)
        {
            if (agent?.Formation == null)
                return false;

            MovementOrder.MovementOrderEnum order =
                agent.Formation.GetReadonlyMovementOrderReference().OrderEnum;
            return order == MovementOrder.MovementOrderEnum.Charge ||
                   order == MovementOrder.MovementOrderEnum.ChargeToTarget ||
                   order == MovementOrder.MovementOrderEnum.Advance ||
                   order == MovementOrder.MovementOrderEnum.AttackEntity ||
                   (agent.Formation.IsAIControlled &&
                    (agent.Formation.AI.ActiveBehavior is BehaviorCharge ||
                     agent.Formation.AI.ActiveBehavior is BehaviorAdvance ||
                     agent.Formation.AI.ActiveBehavior is BehaviorCautiousAdvance ||
                     agent.Formation.AI.ActiveBehavior is BehaviorAssaultWalls ||
                     agent.Formation.AI.ActiveBehavior is BehaviorSallyOut ||
                     agent.Formation.AI.ActiveBehavior is BehaviorEliminateEnemyInsideCastle));
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
