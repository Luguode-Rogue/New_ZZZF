using System;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AggressiveAi
{
    /// <summary>
    /// 根据目标威胁和附近敌我数量调整 NPC 战斗决策。属性模型重算时保存原值，
    /// AI 决策时刷新目标与群战状态，并将变动同步到原生属性。
    /// </summary>
    internal static class AiDefenseThreatAdjustment
    {
        private const float GroupRadius = 8f;
        private const int MinimumWeakEnemies = 2;
        private const int MinimumLevelAdvantage = 15;

        private sealed class Baseline
        {
            public float Block;
            public float Parry;
            public float ParryOnAttack;
            public float ParryWhileAttacking;
            public float Shield;
            public float ShieldWhileAttacking;
            public float Attack;
            public float DecideAttack;
            public float AttackOnParry;
            public float ContinueAttack;
            public float ContinueAttacking;
            public float DecideInterval;
            public float DoInterval;
            public float HoldingReady;
            public float NoAttackAfterHit;
            public float NoAttackAfterParry;
            public bool Suppressed;
            public bool GroupMode;
            public readonly MBList<Agent> NearbyEnemies = new MBList<Agent>();
        }

        private static readonly ConditionalWeakTable<Agent, Baseline> Baselines =
            new ConditionalWeakTable<Agent, Baseline>();

        public static bool Apply(Agent agent, AgentDrivenProperties properties)
        {
            if (!IsEligible(agent) || properties == null)
                return false;

            Baseline baseline = Baselines.GetValue(agent, _ => new Baseline());
            baseline.Block = properties.AIBlockOnDecideAbility;
            baseline.Parry = properties.AIParryOnDecideAbility;
            baseline.ParryOnAttack = properties.AIParryOnAttackAbility;
            baseline.ParryWhileAttacking = properties.AIParryOnAttackingContinueAbility;
            baseline.Shield = properties.AiDefendWithShieldDecisionChanceValue;
            baseline.ShieldWhileAttacking = properties.AiAttackingShieldDefenseChance;
            baseline.Attack = properties.AIAttackOnDecideChance;
            baseline.DecideAttack = properties.AIDecideOnAttackChance;
            baseline.AttackOnParry = properties.AIAttackOnParryChance;
            baseline.ContinueAttack = properties.AiDecideOnAttackContinueAction;
            baseline.ContinueAttacking = properties.AiDecideOnAttackingContinue;
            baseline.DecideInterval = properties.AiCheckDecideSimpleBehaviorInterval;
            baseline.DoInterval = properties.AiCheckDoSimpleBehaviorInterval;
            baseline.HoldingReady = properties.AIHoldingReadyMaxDuration;
            baseline.NoAttackAfterHit = properties.AISetNoAttackTimerAfterBeingHitAbility;
            baseline.NoAttackAfterParry = properties.AISetNoAttackTimerAfterBeingParriedAbility;
            ApplyCurrentTarget(agent, properties, baseline, true);
            return baseline.Suppressed;
        }

        public static void RefreshForCurrentTarget(Agent agent)
        {
            if (!IsEligible(agent))
                return;
            if (!Baselines.TryGetValue(agent, out Baseline baseline))
            {
                // Agent 初次属性重算可能发生在进入 Active 状态之前。
                if (Apply(agent, agent.AgentDrivenProperties))
                    agent.UpdateCustomDrivenProperties();
                return;
            }
            if (ApplyCurrentTarget(agent, agent.AgentDrivenProperties, baseline, false))
                agent.UpdateCustomDrivenProperties();
        }

        private static bool IsEligible(Agent agent)
        {
            return agent != null && agent.IsHuman && agent.IsActive() &&
                   !agent.IsMainAgent && !agent.IsPlayerControlled;
        }

        private static bool ApplyCurrentTarget(Agent agent, AgentDrivenProperties properties,
            Baseline baseline, bool force)
        {
            if (properties == null) return false;
            bool groupMode = IsDominatingWeakGroup(agent, properties, baseline.NearbyEnemies);
            bool suppress = groupMode || HasArmorAdvantage(agent, properties);
            if (!force && suppress == baseline.Suppressed && groupMode == baseline.GroupMode)
                return false;
            bool enteringGroup = groupMode && !baseline.GroupMode;
            baseline.Suppressed = suppress;
            baseline.GroupMode = groupMode;

            // 护甲占优时压低防御概率；独自面对弱敌群时再从输入层彻底禁止格挡。
            float defendFactor = suppress ? 0f : 1f;
            properties.AIBlockOnDecideAbility = baseline.Block * defendFactor;
            properties.AIParryOnDecideAbility = baseline.Parry * defendFactor;
            properties.AIParryOnAttackAbility = baseline.ParryOnAttack * defendFactor;
            properties.AIParryOnAttackingContinueAbility = baseline.ParryWhileAttacking * defendFactor;
            properties.AiDefendWithShieldDecisionChanceValue = baseline.Shield * defendFactor;
            properties.AiAttackingShieldDefenseChance = baseline.ShieldWhileAttacking * defendFactor;
            properties.AIAttackOnDecideChance = suppress ? 1f : baseline.Attack;
            properties.AIDecideOnAttackChance = suppress ? 0f : baseline.DecideAttack;
            properties.AIAttackOnParryChance = suppress ? 1f : baseline.AttackOnParry;
            properties.AiDecideOnAttackContinueAction = suppress ? 1f : baseline.ContinueAttack;
            properties.AiDecideOnAttackingContinue = suppress ? 1f : baseline.ContinueAttacking;
            // 只在单兵碾压弱敌群时加快原生 AI 的决策频率，避免普遍抬高战场开销。
            properties.AiCheckDecideSimpleBehaviorInterval = groupMode ? 0.1f : baseline.DecideInterval;
            properties.AiCheckDoSimpleBehaviorInterval = groupMode ? 0.1f : baseline.DoInterval;
            properties.AIHoldingReadyMaxDuration = groupMode ? 0f : baseline.HoldingReady;
            properties.AISetNoAttackTimerAfterBeingHitAbility = groupMode ? 0f : baseline.NoAttackAfterHit;
            properties.AISetNoAttackTimerAfterBeingParriedAbility = groupMode ? 0f : baseline.NoAttackAfterParry;
            if (enteringGroup)
            {
                // 回调由多个技能共用，不能在离开此状态时关掉；非群战状态下回调直接返回。
                agent.SetHasOnAiInputSetCallback(true);
                Agent.ActionCodeType action = agent.GetCurrentActionType(1);
                if ((int)action >= (int)Agent.ActionCodeType.DefendAllBegin &&
                    (int)action < (int)Agent.ActionCodeType.DefendAllEnd)
                    agent.SetActionChannel(1, ActionIndexCache.act_none, true,
                        (AnimFlags)0UL, 0f, 1f, 0f, 0f, 0f, false, 0f, 0, false);
            }
            return true;
        }

        public static void SuppressDefenseInput(Agent agent, ref Agent.MovementControlFlag movementFlags)
        {
            if (agent != null && Baselines.TryGetValue(agent, out Baseline baseline) &&
                baseline.GroupMode)
                movementFlags &= ~(Agent.MovementControlFlag.DefendMask |
                                   Agent.MovementControlFlag.DefendBlock);
        }

        private static bool IsDominatingWeakGroup(Agent agent, AgentDrivenProperties properties,
            MBList<Agent> nearbyEnemies)
        {
            if (agent.Mission == null || agent.Team == null || agent.Character == null ||
                agent.MountAgent != null)
                return false;
            WeaponComponentData ownWeapon = agent.WieldedWeapon.CurrentUsageItem;
            if (ownWeapon == null || !ownWeapon.IsMeleeWeapon)
                return false;
            agent.Mission.GetNearbyEnemyAgents(agent.Position.AsVec2, GroupRadius,
                agent.Team, nearbyEnemies);
            int weakEnemies = 0;
            float armor = GetWeightedArmor(properties);
            if (float.IsNaN(armor) || float.IsInfinity(armor))
                return false;
            foreach (Agent enemy in nearbyEnemies)
            {
                if (enemy == null || !enemy.IsActive() || enemy.Health <= 0f)
                    continue;
                if (!enemy.IsHuman || enemy.MountAgent != null || enemy.Character == null)
                    return false;
                // 只要附近出现能够伤到自己的高等级或高伤害敌人，就退出只攻不防。
                WeaponComponentData weapon = enemy.WieldedWeapon.CurrentUsageItem;
                float attack = weapon == null ? 10f :
                    Math.Max(10f, Math.Max(weapon.SwingDamage, weapon.ThrustDamage));
                if (agent.Character.Level < enemy.Character.Level + MinimumLevelAdvantage ||
                    armor * 2f < attack)
                    return false;
                weakEnemies++;
            }
            return weakEnemies >= MinimumWeakEnemies;
        }

        private static bool HasArmorAdvantage(Agent agent, AgentDrivenProperties properties)
        {
            Agent enemy = agent.GetTargetAgent();
            if (enemy == null || !enemy.IsHuman || !enemy.IsActive() || enemy.Health <= 0f ||
                enemy.MountAgent != null || !agent.IsEnemyOf(enemy))
                return false;

            WeaponComponentData weapon = enemy.WieldedWeapon.CurrentUsageItem;
            if (weapon != null && !weapon.IsMeleeWeapon)
                return false;
            float enemyAttack = weapon == null ? 10f :
                Math.Max(10f, Math.Max(weapon.SwingDamage, weapon.ThrustDamage));
            float armor = GetWeightedArmor(properties);
            // 测试阶段放宽到护甲值达到敌方武器伤害的一半，确保重甲对劫匪会进入分支。
            return !float.IsNaN(armor) && !float.IsInfinity(armor) &&
                   armor * 2f >= enemyAttack;
        }

        private static float GetWeightedArmor(AgentDrivenProperties properties)
        {
            return properties.ArmorTorso * 0.55f + properties.ArmorHead * 0.25f +
                   properties.ArmorArms * 0.10f + properties.ArmorLegs * 0.10f;
        }
    }
}
