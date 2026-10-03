using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class JianRenBuQu : SkillBase
    {
        internal const float Radius = 50f;
        internal const float BuffDuration = 45f;
        internal const string BuffId = "JianRenBuQuuBuff";
        internal const string DescriptionText = "保护自身与50米内友军45秒，普通攻击、物理与爆炸伤害最终最多为1，其他法术和持续伤害不受影响。重施保留较长持续时间。耐力60，冷却30秒。";
        private readonly MBList<Agent> _nearby = new MBList<Agent>();

        public JianRenBuQu()
        {
            SkillID = "JianRenBuQu";
            Type = SPSkillType.MainActive;
            Cooldown = 30f;
            ResourceCost = 60f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0060}坚韧不屈");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0061}" + DescriptionText);
        }

        private static bool CanAffect(Agent caster, Agent ally) =>
            ally != null && ally.IsActive() && ally.IsHuman && ally.Health > 0f &&
            (ally == caster || caster.IsFriendOf(ally)) &&
            (ally.Position - caster.Position).LengthSquared <= Radius * Radius &&
            ally.GetComponent<AgentSkillComponent>() != null;

        private static bool HasBuff(Agent agent) =>
            agent.GetComponent<AgentSkillComponent>()?.StateContainer.GetLongestStateDuration(BuffId) > 0f;

        public override bool IsHudDurationState(string stateId) => stateId == BuffId;
        private static bool IsAttacking(Agent agent) {
            var action = agent.GetCurrentActionType(1);
            return action == Agent.ActionCodeType.ReadyMelee || action == Agent.ActionCodeType.ReleaseMelee ||
                action == Agent.ActionCodeType.ReadyRanged || action == Agent.ActionCodeType.ReleaseRanged ||
                action == Agent.ActionCodeType.ReleaseThrowing;
        }
        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || caster?.Mission == null || !caster.IsActive()) return false;
            _nearby.Clear();
            caster.Mission.GetNearbyAgents(caster.Position.AsVec2, Radius, _nearby);
            bool needsProtection = CanAffect(caster, caster) && !HasBuff(caster);
            bool engaged = IsAttacking(caster);
            float now = caster.Mission.CurrentTime;
            foreach (Agent target in _nearby) {
                if (target == null || !target.IsActive() || !target.IsHuman || target.Health <= 0f ||
                    (target.Position - caster.Position).LengthSquared > Radius * Radius) continue;
                if (caster.IsEnemyOf(target)) engaged = true;
                if (!CanAffect(caster, target)) continue;
                if (!HasBuff(target)) needsProtection = true;
                if (IsAttacking(target) ||
                    (target.LastRecievedMeleeHitTime > 0f && now - target.LastRecievedMeleeHitTime <= 5f) ||
                    (target.LastRecievedRangedHitTime > 0f && now - target.LastRecievedRangedHitTime <= 5f)) engaged = true;
            }
            // 战斗接近或开始瞄准/攻击就施放，完全不依赖血量与编队命令。
            return engaged && needsProtection;
        }
        // 特定条件可在较长剩余时间上额外延长；默认不额外增加。
        protected virtual float GetAdditionalRefreshDuration(Agent caster, Agent target, float longestRemaining) => 0f;

        private void ApplyTo(Agent caster, Agent ally)
        {
            var states = ally.GetComponent<AgentSkillComponent>().StateContainer;
            var existing = states.GetState(BuffId) as JianRenBuQuuBuff;
            float remaining = states.GetLongestStateDuration(BuffId);
            float additional = GetAdditionalRefreshDuration(caster, ally, remaining);
            if (float.IsNaN(additional) || float.IsInfinity(additional)) additional = 0f;
            float duration = Math.Max(BuffDuration, remaining) + Math.Max(0f, additional);
            if (existing != null) existing.Duration = duration;
            else states.AddState(new JianRenBuQuuBuff(duration, caster), ally);
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

        public class JianRenBuQuuBuff : AgentBuff
        {
            public override bool BypassesSkillProtection => true;
            public override string BattleHudName => "坚韧不屈";
            public JianRenBuQuuBuff(float duration, Agent source) {
                StateId = BuffId; Duration = duration; SourceAgent = source;
            }
            public override void OnApply(Agent agent) { }
            public override void OnUpdate(Agent agent, float dt) { }
            public override void OnRemove(Agent agent) { }
        }
    }
}