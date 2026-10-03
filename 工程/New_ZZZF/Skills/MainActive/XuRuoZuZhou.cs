using System;
using New_ZZZF.Systems;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using TaleWorlds.Core;

namespace New_ZZZF
{
    internal class XuRuoZuZhou : SkillBase
    {
        internal const float DebuffDuration = 60f;
        internal const string MarkerId = "XuRuoZuZhouAllyMarker";
        internal const string DescriptionText = "全图虚弱诅咒60秒，武器伤害降低50%，每秒耐力恢复减少5。友军已有施放时NPC不会重复开启。耐力60，冷却90秒。";
        public XuRuoZuZhou() {
            SkillID = "XuRuoZuZhou"; Type = SPSkillType.MainActive;
            Cooldown = 90f; ResourceCost = 60f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0056}虚弱诅咒");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0057}" + DescriptionText);
        }
        public override bool IsHudDurationState(string stateId) => false;
        internal static bool IsCursed(Agent agent) =>
            (agent?.IsMount == true ? agent.RiderAgent : agent)?.GetComponent<AgentSkillComponent>()?
                .StateContainer.GetLongestStateDuration("XuRuoZuZhouBuffToEnemy") > 0f;
        private static bool CanAffect(Agent caster, Agent enemy) => enemy != null && enemy.IsActive() &&
            enemy.IsHuman && enemy.Health > 0f && caster.IsEnemyOf(enemy) &&
            !SkillTargetProtection.IsProtected(enemy) && enemy.GetComponent<AgentSkillComponent>() != null;
        public override bool CheckCondition(Agent caster) {
            if (!base.CheckCondition(caster) || caster?.Mission == null) return false;
            if (caster.GetComponent<AgentSkillComponent>()?.StateContainer.HasState(MarkerId) == true) return false;
            foreach (Agent enemy in caster.Mission.Agents)
                if (CanAffect(caster, enemy)) return true;
            return false;
        }
        public override bool Activate(Agent caster) {
            if (caster == null || !caster.IsActive() || caster.Mission == null) return FailActivation("施法者不可用。");
            bool affected = false;
            foreach (Agent enemy in caster.Mission.Agents) {
                if (!CanAffect(caster, enemy)) continue;
                AgentBuffContainer states = enemy.GetComponent<AgentSkillComponent>().StateContainer;
                var old = states.GetState("XuRuoZuZhouBuffToEnemy") as XuRuoZuZhouBuffToEnemy;
                if (old != null) { old.Duration = Math.Max(old.Duration, DebuffDuration); }
                else states.AddState(new XuRuoZuZhouBuffToEnemy(DebuffDuration, caster), enemy);
                affected = true;
            }
            if (!affected) return FailActivation("没有可诅咒的敌军。");
            foreach (Agent ally in caster.Mission.Agents) {
                if (ally == null || !ally.IsActive() || !ally.IsHuman || (ally != caster && !caster.IsFriendOf(ally))) continue;
                var states = ally.GetComponent<AgentSkillComponent>()?.StateContainer;
                if (states == null) continue;
                var old = states.GetState(MarkerId);
                if (old != null) old.Duration = Math.Max(old.Duration, DebuffDuration);
                else states.AddState(new AllyMarker(caster), ally);
            }

            return true;
        }
        private sealed class AllyMarker : AgentBuff {
            public override bool IsMarker => true;
            public override bool BypassesSkillProtection => true;
            public AllyMarker(Agent source) { StateId = MarkerId; SourceAgent = source; Duration = DebuffDuration; }
            public override void OnApply(Agent agent) { }
            public override void OnUpdate(Agent agent, float dt) { }
            public override void OnRemove(Agent agent) { }
        }
    }
    public class XuRuoZuZhouBuffToEnemy : AgentBuff
    {
        public override string BattleHudName => "虚弱诅咒";
        public XuRuoZuZhouBuffToEnemy(float duration, Agent source) {
            StateId = "XuRuoZuZhouBuffToEnemy"; Duration = duration; SourceAgent = source;
        }
        public override void OnApply(Agent agent) { }
        public override void OnUpdate(Agent agent, float dt) {
            if (SkillTargetProtection.IsProtected(agent)) Duration = 0f;
        }
        public override void OnRemove(Agent agent) { }
    }
}