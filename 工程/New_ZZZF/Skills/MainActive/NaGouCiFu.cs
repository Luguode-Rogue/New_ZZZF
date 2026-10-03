using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class NaGouCiFu : SkillBase
    {
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        public NaGouCiFu()
        {
            SkillID = "NaGouCiFu"; Type = SPSkillType.MainActive; Cooldown = 60f; ResourceCost = 60f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0039}NaGouCiFu");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0040}赐福50米内友军60秒：获得3次通用复活；护甲腐朽，无视目标50%护甲，移速-30%。每秒将最大生命10%转为等额护盾，生命至少保留1，首次耗尽后额外产盾5秒。受到物理伤害时50%概率免伤并回复该次应受伤害量。重施刷新较长时间。耐力60，冷却60秒。");
        }
        private static bool IsAlly(Agent caster, Agent target) => target != null && target.IsActive() && target.IsHuman &&
            (target == caster || caster.IsFriendOf(target)) && (target.Position - caster.Position).LengthSquared <= 2500f &&
            target.GetComponent<AgentSkillComponent>() != null;
        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || caster.Mission == null) return false;
            _nearby.Clear(); caster.Mission.GetNearbyAgents(caster.Position.AsVec2, 50f, _nearby);
            int needs = 0; bool fighting = false; bool injured = false;
            foreach (Agent target in _nearby)
            {
                if (target == null || !target.IsActive()) continue;
                if (caster.IsEnemyOf(target) && (target.Position - caster.Position).LengthSquared <= 625f) fighting = true;
                if (!IsAlly(caster, target)) continue;
                AgentSkillComponent component = target.GetComponent<AgentSkillComponent>();
                bool needsBuff = component.StateContainer.GetLongestStateDuration("NaGouCiFuBuff") <= 10f || component._lifeResurgenceCount == 0;
                if (!needsBuff) continue;
                needs++;
                injured |= target.Health < target.HealthLimit * 0.65f;
                fighting |= target.IsPerformingAction() && component._beHitTime > 0f;
            }
            return fighting && (needs >= 2 || injured && needs > 0);
        }
        public override bool Activate(Agent caster)
        {
            if (caster == null || !caster.IsActive() || caster.Mission == null) return FailActivation("施法者不可用。");
            _nearby.Clear(); caster.Mission.GetNearbyAgents(caster.Position.AsVec2, 50f, _nearby);
            if (!_nearby.Contains(caster)) _nearby.Add(caster);
            int count = 0;
            foreach (Agent target in _nearby)
            {
                if (!IsAlly(caster, target)) continue;
                AgentBuffContainer states = target.GetComponent<AgentSkillComponent>().StateContainer;
                var old = states.GetState("NaGouCiFuBuff") as NaGouCiFuBuff;
                states.AddOrReplaceState(new NaGouCiFuBuff(Math.Max(60f, states.GetLongestStateDuration("NaGouCiFuBuff")), caster,
                    old?.ExtraShieldSeconds ?? 0, old?.ReachedMinimum ?? false), target);
                count++;
            }
            if (count == 0) return FailActivation("范围内没有友军。");
            try { SoundManager.StartOneShotEvent("event:/voice/combat/" + (caster.IsFemale ? "female" : "male") + "/0" + (MBRandom.RandomInt(4) + 1) + "/yell", caster.Position); }
            catch (Exception) { }
            return true;
        }
        public class NaGouCiFuBuff : AgentBuff
        {
            internal int ExtraShieldSeconds;
            internal bool ReachedMinimum;
            private float _timer;
            private NaGouFallingEmbersVisual _visual;
            public override bool BypassesSkillProtection => true;
            public override string BattleHudName => "纳垢赐福";
            public NaGouCiFuBuff(float duration, Agent source, int extraSeconds = 0, bool reachedMinimum = false)
            { StateId = "NaGouCiFuBuff"; Duration = duration; SourceAgent = source; ExtraShieldSeconds = extraSeconds; ReachedMinimum = reachedMinimum; }
            public override void OnApply(Agent agent)
            {
                agent.GetComponent<AgentSkillComponent>()._lifeResurgenceCount += 3;
                agent.UpdateAgentProperties();
                _visual = NaGouFallingEmbersVisual.CreateLingering(agent);
            }
            public override void OnUpdate(Agent agent, float dt)
            {
                _visual?.Update(agent, dt);
                _timer += dt;
                int ticks = (int)_timer; _timer -= ticks;
                AgentSkillComponent component = agent.GetComponent<AgentSkillComponent>();
                if (component == null) return;
                float maximum = agent.HealthLimit > 0f ? agent.HealthLimit : component.MaxHP;
                for (int i = 0; i < ticks; i++)
                {
                    bool extraWindow = ReachedMinimum && ExtraShieldSeconds < 5;
                    if (ReachedMinimum && ExtraShieldSeconds < 5) ExtraShieldSeconds++;
                    float payment = Math.Min(Math.Max(0f, agent.Health - 1f), maximum * 0.1f);
                    if (payment > 0f)
                    { agent.Health -= payment; component._shieldStrength += payment; }
                    else if (extraWindow)
                    { component._shieldStrength += maximum * 0.1f; }
                    if (agent.Health <= 1f) ReachedMinimum = true;
                }
            }
            public override void OnRemove(Agent agent)
            { _visual?.Remove(); _visual = null; if (agent != null && agent.IsActive()) agent.UpdateAgentProperties(); }
        }
    }
}