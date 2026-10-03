using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
namespace New_ZZZF
{
    internal class YingXiongZhuFu : SkillBase
    {
        internal const float BuffDuration = 60f;
        private const float Range = 50f;
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        internal const string EffectText = "{=ZZZF0036}祝福50米内友军60秒：满血、获得等额护盾及1次复活；武器伤害+40%，每秒耐力+2。射击误差÷4.5，晃动惩罚÷3，装填/攻击准备+40%，操控/加速及坐骑速度/操控+30%。重施刷新较长时间，复活次数独立保留。耐力60，冷却20秒。";
        public YingXiongZhuFu() {
            SkillID = "YingXiongZhuFu"; Type = SPSkillType.MainActive; Cooldown = 20f; ResourceCost = 60f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0035}YingXiongZhuFu");
            Description = new TaleWorlds.Localization.TextObject(EffectText);
        }
        private static bool IsAlly(Agent caster, Agent target) => target != null && target.IsActive() &&
            target.IsHuman && (target == caster || target.IsFriendOf(caster)) &&
            (target.Position - caster.Position).LengthSquared <= Range * Range && target.GetComponent<AgentSkillComponent>() != null;
        public override bool CheckCondition(Agent caster) {
            if (!base.CheckCondition(caster) || caster.Mission == null) return false;
            _nearby.Clear(); caster.Mission.GetNearbyAgents(caster.Position.AsVec2, Range, _nearby);
            int needs = 0; float missing = 0f;
            foreach (Agent target in _nearby) {
                if (!IsAlly(caster, target)) continue;
                float ratio = target.Health / Math.Max(1f, target.HealthLimit);
                if (ratio <= 0.65f) return true;
                missing += Math.Max(0f, 1f - ratio);
                AgentSkillComponent component = target.GetComponent<AgentSkillComponent>();
                if (component.StateContainer.GetLongestStateDuration("YingXiongZhuFuBuff") <= 10f || component._lifeResurgenceCount == 0) needs++;
            }
            // 群体祝福需求优先，不依赖鼓舞回血后剩余的伤势或敌人是否已进入20米。
            return needs >= 2 || missing >= 1f;
        }
        public override bool Activate(Agent caster) {
            if (caster == null || !caster.IsActive() || caster.Mission == null) return FailActivation("施法者不可用。");
            _nearby.Clear(); caster.Mission.GetNearbyAgents(caster.Position.AsVec2, Range, _nearby);
            if (!_nearby.Contains(caster)) _nearby.Add(caster);
            int count = 0;
            foreach (Agent ally in _nearby) {
                if (!IsAlly(caster, ally)) continue;
                AgentBuffContainer states = ally.GetComponent<AgentSkillComponent>().StateContainer;
                float duration = MathF.Max(BuffDuration, states.GetLongestStateDuration("YingXiongZhuFuBuff"));
                states.AddOrReplaceState(new YingXiongZhuFuBuff(duration, caster), ally); count++;
            }
            if (count == 0) return FailActivation("范围内没有友军。");
            string sex = caster.IsFemale ? "female" : "male";
            try { SoundManager.StartOneShotEvent("event:/voice/combat/" + sex + "/0" + (MBRandom.RandomInt(4) + 1) + "/yell", caster.Position); }
            catch (Exception) { }
            return true;
        }
        public class YingXiongZhuFuBuff : AgentBuff
        {
            public override bool BypassesSkillProtection => true;
            public override string BattleHudName => "英雄祝福";
            private float _timer;
            private GuWuFallingEmbersVisual _visual;
            private BlessingNovaVisual _nova;
            private Agent _lastMount;
            public YingXiongZhuFuBuff(float duration, Agent source) { StateId = "YingXiongZhuFuBuff"; Duration = duration; SourceAgent = source; }
            public override void OnApply(Agent agent) {
                AgentSkillComponent component = agent.GetComponent<AgentSkillComponent>(); if (component == null) return;
                float maximum = agent.HealthLimit > 0f ? agent.HealthLimit : component.MaxHP;
                agent.Health = maximum; component._lifeResurgenceCount++; component._shieldStrength += maximum;
                _visual = GuWuFallingEmbersVisual.CreateLingering(agent);
                if (agent == SourceAgent) _nova = BlessingNovaVisual.Create(agent);
                agent.UpdateAgentProperties(); RefreshMount(agent);
            }
            private void RefreshMount(Agent agent) {
                Agent mount = agent.MountAgent; if (mount == _lastMount) return;
                Agent old = _lastMount; _lastMount = mount;
                if (old != null && old.IsActive()) old.UpdateAgentProperties();
                if (mount != null && mount.IsActive()) mount.UpdateAgentProperties();
            }
            public override void OnUpdate(Agent agent, float dt) {
                _visual?.Update(agent, dt); if (_nova != null && !_nova.Update(dt)) _nova = null;
                _timer += dt; if (_timer < 1f) return;
                int seconds = (int)_timer; _timer -= seconds;
                agent.GetComponent<AgentSkillComponent>()?.ChangeStamina(2f * seconds); RefreshMount(agent);
            }
            public override void OnRemove(Agent agent) {
                _visual?.Remove(); _visual = null; _nova?.Remove(); _nova = null;
                // 复活次数属于通用资源，不随本 Buff 移除。
                if (agent != null && agent.IsActive()) agent.UpdateAgentProperties();
                if (_lastMount != null && _lastMount.IsActive()) _lastMount.UpdateAgentProperties(); _lastMount = null;
            }
        }
    }
}
