using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class BKB : SkillBase
    {
        public const float DamageWindowSeconds = 3f;
        private struct DamageSample { public float Time; public float Damage; }
        private sealed class DamageHistory { public readonly Queue<DamageSample> Samples = new Queue<DamageSample>(); public float Total; }
        private static readonly ConditionalWeakTable<Agent, DamageHistory> Histories = new ConditionalWeakTable<Agent, DamageHistory>();

        public BKB()
        {
            SkillID = "BKB";
            Type = SPSkillType.MainActive;
            Cooldown = 40f;
            ResourceCost = 60f;
            Text = new TextObject("{=ZZZF0070}BKB");
            Description = new TextObject("{=ZZZF0071}天神下凡：体型增大50%，武器伤害提高100%，移速降低50%，自身法术伤害降低90%。免疫技能法术伤害及手动技能选取，免除受击硬直，上劈必定突破格挡并无限贯穿单位，持续净化敌方负面状态。持续结束后体型缩小至原尺寸75%，冷却结束恢复。持续30秒，消耗耐力60，冷却40秒。");

        }

        public override bool Activate(Agent agent)
        {
            AgentSkillComponent component = agent?.GetComponent<AgentSkillComponent>();
            if (agent == null || !agent.IsActive() || component == null) return FailActivation("施法者状态不可用。");
            // 缩小阶段再施放时先恢复基准尺寸；旧状态延后清理时不能覆盖新的巨化。
            (component.StateContainer.GetState("BkbCooldownShrink") as BkbCooldownShrinkBuff)?.Cancel(agent);
            if (component.StateContainer.GetState("BKBBuff") is BKBBuff existing) {
                existing.Duration = TaleWorlds.Library.MathF.Max(existing.Duration, 30f);
                component.StateContainer.ExpireEnemyStates(agent);
                agent.UpdateAgentProperties();
            } else {
                component.StateContainer.AddState(new BKBBuff(30f, agent, this) { TargetAgent = agent }, agent);
            }
            CastSoundEvent = agent.IsFemale ? "event:/voice/combat/female/01/yell" : "event:/voice/combat/male/01/yell";
            Histories.Remove(agent);
            return component.StateContainer.HasState("BKBBuff");
        }

        // 在法术最终伤害扣血前调用，不绕过资源/冷却，也不对玩家自动施法。
        internal static bool TryActivateForMagicHit(Agent victim, float damage)
        {
            if (victim == null || !victim.IsActive() || !victim.IsAIControlled || victim.IsMount ||
                damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return false;
            AgentSkillComponent component = victim.GetComponent<AgentSkillComponent>();
            if (!(component?.MainActiveSkill is BKB) || component.StateContainer.HasState("BKBBuff")) return false;
            float maximum = victim.HealthLimit;
            if (maximum <= 0f) return false;
            float now = victim.Mission.CurrentTime;
            DamageHistory history = Histories.GetValue(victim, _ => new DamageHistory());
            while (history.Samples.Count > 0 && now - history.Samples.Peek().Time > DamageWindowSeconds)
                history.Total -= history.Samples.Dequeue().Damage;
            history.Samples.Enqueue(new DamageSample { Time = now, Damage = damage });
            history.Total += damage;
            if (damage <= maximum * 0.2f && history.Total < maximum * 0.3f && victim.Health >= maximum * 0.5f)
                return false;
            return component.TryActivateBkbForMagicHit();
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || !caster.IsAIControlled || caster.IsMount ||
                caster.GetComponent<AgentSkillComponent>()?.StateContainer.HasState("BKBBuff") != false) return false;
            // 主动增伤只在确实准备出手/交手时触发，不依赖编队命令。
            Agent target = caster.GetTargetAgent();
            if (target == null || !target.IsActive() || !caster.IsEnemyOf(target)) return false;
            Agent.ActionCodeType type = caster.GetCurrentActionType(1);
            bool aiming = type == Agent.ActionCodeType.ReadyRanged || type == Agent.ActionCodeType.ReleaseRanged;
            bool melee = type == Agent.ActionCodeType.ReadyMelee || type == Agent.ActionCodeType.ReleaseMelee;
            MissionWeapon weapon = caster.WieldedWeapon;
            float reach = weapon.CurrentUsageItem == null ? 2f : TaleWorlds.Library.MathF.Max(2f, weapon.CurrentUsageItem.WeaponLength * 0.01f + 1f);
            return aiming || (melee && (target.Position - caster.Position).LengthSquared <= reach * reach);
        }

        public class BKBBuff : AgentBuff
        {
            private float _cleanseTimer;
            private BkbTransformationVisual _visual;
            private readonly SkillBase _skill;
            public override string BattleHudName => "天神下凡";
            public BKBBuff(float duration, Agent source, SkillBase skill = null) { StateId = "BKBBuff"; Duration = duration; SourceAgent = source; _skill = skill; }
            public override void OnApply(Agent agent)
            {
                agent.GetComponent<AgentSkillComponent>()?.StateContainer.ExpireEnemyStates(agent);
                agent.UpdateAgentProperties();
                _visual = new BkbTransformationVisual();
                _visual.Apply(agent);
            }
            public override void OnUpdate(Agent agent, float dt)
            {
                _cleanseTimer += dt;
                if (_cleanseTimer < 1f) return;
                _cleanseTimer %= 1f;
                if (agent.GetComponent<AgentSkillComponent>()?.StateContainer.ExpireEnemyStates(agent) > 0)
                    agent.UpdateAgentProperties();
                _visual?.Refresh(agent);
            }
            public override void OnRemove(Agent agent)
            {
                float originalScale = _visual?.OriginalScale ?? 1f;
                _visual?.Remove(agent);
                AgentSkillComponent component = agent?.GetComponent<AgentSkillComponent>();
                float remaining = component?.GetSkillCooldownForDisplay(_skill) ?? 0f;
                if (agent != null && agent.IsActive() && remaining > 0f && !component.StateContainer.HasState("BKBBuff"))
                    component.StateContainer.AddState(new BkbCooldownShrinkBuff(agent, _skill, remaining, originalScale), agent);
                _visual = null;
                if (agent != null && agent.IsActive()) agent.UpdateAgentProperties();
            }
        }
    }
}
