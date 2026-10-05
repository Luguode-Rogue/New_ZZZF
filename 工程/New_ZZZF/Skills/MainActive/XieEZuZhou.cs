using System;
using New_ZZZF.Systems;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using TaleWorlds.Core;

namespace New_ZZZF
{
    internal class XieEZuZhou : SkillBase
    {
        internal const float DebuffDuration = 60f;
        internal const string MarkerId = "XieEZuZhouAllyMarker";
        internal const string DescriptionText = "全图诅咒普通敌军60秒，每秒流失最大生命的1%，降低移速30%、精度20%、物理伤害20%，加速时间乘1.5，并允许其误伤友军。英雄免疫。友军已有施放时NPC不会重复开启。耐力60，冷却90秒。";
        public XieEZuZhou() {
            SkillID = "XieEZuZhou"; Type = SPSkillType.MainActive;
            Cooldown = 90f; ResourceCost = 60f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0053}邪恶诅咒");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0054}" + DescriptionText);
        }
        public override bool IsHudDurationState(string stateId) => false;
        internal static bool IsCursed(Agent agent) =>
            (agent?.IsMount == true ? agent.RiderAgent : agent)?.GetComponent<AgentSkillComponent>()?
                .StateContainer.GetLongestStateDuration("XieEZuZhouBuffToEnemy") > 0f;
        private static bool CanAffect(Agent caster, Agent enemy) => enemy != null && enemy.IsActive() &&
            enemy.IsHuman && !enemy.IsHero && enemy.Health > 0f && caster.IsEnemyOf(enemy) &&
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
                var old = states.GetState("XieEZuZhouBuffToEnemy") as XieEZuZhouBuffToEnemy;
                if (old != null) { old.Duration = Math.Max(old.Duration, DebuffDuration); }
                else states.AddState(new XieEZuZhouBuffToEnemy(DebuffDuration, caster), enemy);
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
            try {
                string voice = "event:/voice/combat/" + (caster.IsFemale ? "female" : "male") + "/0" + MBRandom.RandomInt(1, 5) + "/yell";
                SoundManager.StartOneShotEvent(voice, caster.Position);
            } catch (Exception) { /* 声音资源缺失不影响减益。 */ }
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
    public class XieEZuZhouBuffToEnemy : AgentBuff
    {
        private float _elapsed;
        private Agent _mount;
        public override string BattleHudName => "邪恶诅咒";
        public override bool IsDamageOverTime => true;
        public XieEZuZhouBuffToEnemy(float duration, Agent source) {
            StateId = "XieEZuZhouBuffToEnemy"; Duration = duration; SourceAgent = source;
        }
        private void Refresh(Agent agent) {
            agent.UpdateAgentProperties();
            if (_mount != null && _mount != agent.MountAgent && _mount.IsActive()) _mount.UpdateAgentProperties();
            _mount = agent.MountAgent;
            if (_mount != null && _mount.IsActive()) _mount.UpdateAgentProperties();
        }
        public override void OnApply(Agent agent) { Refresh(agent); }
        public override void OnUpdate(Agent agent, float dt) {
            if (SkillTargetProtection.IsProtected(agent)) { Duration = 0f; return; }
            if (_mount != agent.MountAgent) Refresh(agent);
            _elapsed += dt;
            while (_elapsed >= 1f && agent.Health > 0f) {
                _elapsed -= 1f;
                MagicDamageSystem.ApplyResolvedPeriodicDamage(agent, agent.HealthLimit * 0.01f);
            }
        }
        public override void OnRemove(Agent agent) {
            if (agent != null && agent.IsActive()) Refresh(agent);
            if (_mount != null && _mount.IsActive()) _mount.UpdateAgentProperties();
            _mount = null;
        }
    }
}