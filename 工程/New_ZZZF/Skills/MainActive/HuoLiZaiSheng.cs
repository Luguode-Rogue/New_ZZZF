using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class HuoLiZaiSheng : SkillBase
    {
        internal const float Radius = 50f;
        internal const float BuffDuration = 30f;
        internal const string BuffId = "HuoLiZaiShengBuff";
        internal const string DescriptionText = "50米内友军与自身获得活力再生，每秒恢复全部已损失生命，持续30秒。重复施放保留较长持续时间。耐力消耗0，冷却40秒。";
        private readonly MBList<Agent> _nearby = new MBList<Agent>();

        public HuoLiZaiSheng()
        {
            SkillID = "HuoLiZaiSheng";
            Type = SPSkillType.MainActive;
            Cooldown = 40f;
            ResourceCost = 0f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0058}活力再生");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0059}" + DescriptionText);
        }

        private static bool CanAffect(Agent caster, Agent ally) =>
            ally != null && ally.IsActive() && ally.IsHuman && ally.Health > 0f &&
            (ally == caster || caster.IsFriendOf(ally)) &&
            (ally.Position - caster.Position).LengthSquared <= Radius * Radius &&
            ally.GetComponent<AgentSkillComponent>() != null;

        private static bool HasBuff(Agent agent) =>
            agent.GetComponent<AgentSkillComponent>()?.StateContainer.GetLongestStateDuration(BuffId) > 0f;

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || caster?.Mission == null || !caster.IsActive()) return false;
            if (CanAffect(caster, caster) && !HasBuff(caster) && caster.Health <= caster.HealthLimit * 0.5f)
                return true;

            _nearby.Clear();
            caster.Mission.GetNearbyAgents(caster.Position.AsVec2, Radius, _nearby);
            int needsHealing = 0;
            foreach (Agent ally in _nearby)
            {
                if (ally == caster || !CanAffect(caster, ally) || HasBuff(ally) ||
                    ally.Health > ally.HealthLimit * 0.75f) continue;
                if (++needsHealing >= 2) return true;
            }
            return false;
        }

        // 特定条件可在较长剩余时间上额外延长；默认不额外增加。
        protected virtual float GetAdditionalRefreshDuration(Agent caster, Agent target, float longestRemaining) => 0f;

        private void ApplyTo(Agent caster, Agent ally)
        {
            var states = ally.GetComponent<AgentSkillComponent>().StateContainer;
            var existing = states.GetState(BuffId) as HuoLiZaiShengBuff;
            float remaining = states.GetLongestStateDuration(BuffId);
            float additional = GetAdditionalRefreshDuration(caster, ally, remaining);
            if (float.IsNaN(additional) || float.IsInfinity(additional)) additional = 0f;
            float duration = Math.Max(BuffDuration, remaining) + Math.Max(0f, additional);
            if (existing != null) existing.Duration = duration; // 保留原回血计时，不重置。
            else states.AddState(new HuoLiZaiShengBuff(duration, caster), ally);
        }

        public override bool Activate(Agent caster)
        {
            if (caster == null || !caster.IsActive() || caster.Mission == null || !CanAffect(caster, caster))
                return FailActivation("施法者不可用。");
            // 显式覆盖自身，不依赖附近查询是否包含查询者。
            ApplyTo(caster, caster);
            _nearby.Clear();
            caster.Mission.GetNearbyAgents(caster.Position.AsVec2, Radius, _nearby);
            foreach (Agent ally in _nearby)
                if (ally != caster && CanAffect(caster, ally)) ApplyTo(caster, ally);
            return true;
        }

        public class HuoLiZaiShengBuff : AgentBuff
        {
            private float _timeSinceLastTick;
            public override bool BypassesSkillProtection => true;
            public override string BattleHudName => "活力再生";
            public HuoLiZaiShengBuff(float duration, Agent source)
            {
                StateId = BuffId;
                Duration = duration;
                SourceAgent = source;
            }
            public override void OnApply(Agent agent) { }
            public override void OnUpdate(Agent agent, float dt)
            {
                if (agent == null || !agent.IsActive() || agent.Health <= 0f) return;
                _timeSinceLastTick += dt;
                if (_timeSinceLastTick < 1f) return;
                _timeSinceLastTick %= 1f;
                // 回满血无需补做多次同帧结算，不依赖施法者是否仍然存活。
                if (agent.HealthLimit > 0f && agent.Health < agent.HealthLimit)
                    agent.Health = agent.HealthLimit;
            }
            public override void OnRemove(Agent agent) { }
        }
    }
}