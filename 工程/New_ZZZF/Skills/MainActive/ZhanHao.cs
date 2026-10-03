using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class ZhanHao : SkillBase
    {
        private const int Range = 50;
        private const float BuffDuration = 60f;
        private const float PhysicalReduction = 0.20f;
        private const float EngagementRange = 80f;
        private static readonly string[] MaleYells =
        {
            "event:/voice/combat/male/01/yell", "event:/voice/combat/male/02/yell",
            "event:/voice/combat/male/03/yell", "event:/voice/combat/male/04/yell",
            "event:/voice/combat/male/05/yell"
        };
        private static readonly string[] FemaleYells =
        {
            "event:/voice/combat/female/01/yell", "event:/voice/combat/female/02/yell",
            "event:/voice/combat/female/03/yell", "event:/voice/combat/female/04/yell",
            "event:/voice/combat/female/05/yell"
        };

        public ZhanHao()
        {
            SkillID = "ZhanHao";
            Type = SPSkillType.MainActive;
            Cooldown = 30f;
            ResourceCost = 50f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0029}ZhanHao");
            Description = new TaleWorlds.Localization.TextObject(
                "{=ZZZF0030}使50米内友军获得战嚎：武器伤害、攻速、装填、移动和护甲提高20%，物理伤害减免20%。持续60秒，重复施放会刷新持续时间。消耗耐力：50。冷却时间：30秒。");
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster))
                return false;

            // 已有战嚎也允许再次施放：后续可在此扩展重复施放的额外收益。
            if (caster.HealthLimit > 0f && caster.Health < caster.HealthLimit - 1f)
                return true;
            Agent target = caster.GetTargetAgent();
            if (target != null && target.IsActive() && (target.IsEnemyOf(caster) && !SkillTargetProtection.IsProtected(target)) &&
                target.Position.Distance(caster.Position) <= EngagementRange)
                return true;
            return AiBattleOrderGate.AllowsAggressiveSkill(caster);
        }

        public override bool Activate(Agent agent)
        {
            if (agent == null || !agent.IsActive())
                return FailActivation("施法者不可用。");

            List<Agent> allies = Script.GetTargetedInRange(
                agent, agent.GetEyeGlobalPosition(), Range, true) ?? new List<Agent>();
            if (agent.IsHuman && !allies.Contains(agent))
                allies.Add(agent);

            int affected = 0;
            foreach (Agent ally in allies)
            {
                AgentSkillComponent component = ally.GetComponent<AgentSkillComponent>();
                if (component == null)
                    continue;
                float longestRemaining = component.StateContainer.GetLongestStateDuration("ZhanHaoBuff");
                float duration = MathF.Max(BuffDuration, longestRemaining);
                component.StateContainer.AddOrReplaceState(new ZhanHaoBuff(duration, agent), ally);
                affected++;
            }
            if (affected == 0)
                return FailActivation("范围内没有可受益的单位。");

            PlayCastPresentation(agent);
            return true;
        }

        private static void PlayCastPresentation(Agent agent)
        {
            string[] yells = agent.IsFemale ? FemaleYells : MaleYells;
            try { SoundManager.StartOneShotEvent(yells[MBRandom.RandomInt(yells.Length)], agent.Position); }
            catch (Exception) { /* 音效失效不影响技能结算。 */ }

            WeaponClass weaponClass = agent.WieldedWeapon.CurrentUsageItem?.WeaponClass ?? WeaponClass.Undefined;
            string action = weaponClass == WeaponClass.Bow || weaponClass == WeaponClass.Crossbow
                ? "act_taunt_cheer_1_bow" : "act_taunt_cheer_1";
            try { agent.SetActionChannel(1, ActionIndexCache.Create(action), true); }
            catch (Exception) { /* 动作失效不影响状态。 */ }
        }

        internal static void ApplyDrivenProperties(Agent agent, AgentDrivenProperties properties)
        {
            if (agent == null || properties == null ||
                !SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index,
                    out AgentSkillComponent component) ||
                !component.StateContainer.HasState("ZhanHaoBuff"))
                return;

            properties.SwingSpeedMultiplier *= 1.2f;
            properties.ThrustOrRangedReadySpeedMultiplier *= 1.2f;
            properties.HandlingMultiplier *= 1.2f;
            properties.ReloadSpeed *= 1.2f;
            properties.MissileSpeedMultiplier *= 1.2f;
            properties.WeaponInaccuracy /= 1.2f;
            properties.WeaponMaxMovementAccuracyPenalty /= 1.2f;
            properties.WeaponMaxUnsteadyAccuracyPenalty /= 1.2f;
            properties.WeaponBestAccuracyWaitTime /= 1.2f;
            properties.ArmorEncumbrance /= 1.2f;
            properties.WeaponsEncumbrance /= 1.2f;
            properties.ArmorHead *= 1.2f;
            properties.ArmorTorso *= 1.2f;
            properties.ArmorLegs *= 1.2f;
            properties.ArmorArms *= 1.2f;
            properties.AttributeRiding *= 1.2f;
            properties.AttributeShield *= 1.2f;
            properties.AttributeShieldMissileCollisionBodySizeAdder *= 1.2f;
            properties.ShieldBashStunDurationMultiplier *= 1.2f;
            properties.KickStunDurationMultiplier *= 1.2f;
            properties.TopSpeedReachDuration /= 1.2f;
            properties.MaxSpeedMultiplier *= 1.2f;
            properties.CombatMaxSpeedMultiplier *= 1.2f;
            properties.AttributeHorseArchery *= 1.2f;
            properties.AttributeCourage *= 1.2f;
        }

        public sealed class ZhanHaoBuff : AgentBuff
        {
            private ZhanHaoRisingDropsVisual _burst;
            private ZhanHaoRisingDropsVisual _lingering;

            public ZhanHaoBuff(float duration, Agent source)
            {
                StateId = "ZhanHaoBuff";
                Duration = duration;
                SourceAgent = source;
            }

            public override void OnApply(Agent agent)
            {
                if (agent == null || !agent.IsActive())
                    return;
                agent.GetComponent<AgentSkillComponent>()?.SetPhysicalDamageReduction(
                    StateId, PhysicalReduction);
                agent.UpdateAgentProperties();
                _burst = ZhanHaoRisingDropsVisual.Create(agent, false);
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                if (agent == null || !agent.IsActive() || dt <= 0f)
                    return;
                if (_burst != null && !_burst.Update(agent, dt))
                {
                    _burst = null;
                    _lingering = ZhanHaoRisingDropsVisual.Create(agent, true);
                }
                _lingering?.Update(agent, dt);
            }

            public override void OnRemove(Agent agent)
            {
                _burst?.Remove();
                _burst = null;
                _lingering?.Remove();
                _lingering = null;
                agent?.GetComponent<AgentSkillComponent>()?.RemovePhysicalDamageReduction(StateId);
                if (agent != null && agent.IsActive())
                    agent.UpdateAgentProperties();
            }
        }
    }
}
