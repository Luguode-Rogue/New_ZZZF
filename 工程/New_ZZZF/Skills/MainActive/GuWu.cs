using New_ZZZF.Systems;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class GuWu : SkillBase
    {
        private const float BuffDuration = 30f;
        private const int Range = 50;
        private const float AiRefreshThreshold = 10f;
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

        public GuWu()
        {
            SkillID = "GuWu";
            Type = SPSkillType.MainActive;
            Cooldown = 60f;
            ResourceCost = 50f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0021}GuWu");
            Description = new TaleWorlds.Localization.TextObject(
                "{=ZZZF0022}使50米内友军获得鼓舞：射击误差降低20%，每秒恢复1点耐力和5%已损生命值，持续30秒。重复施放时保留较长的剩余时间。施放者立即恢复30%最大生命值。消耗耐力：50。冷却时间：60秒。");
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster))
                return false;

            List<Agent> allies = GetAlliesInRange(caster);
            int needingBuff = 0;
            foreach (Agent ally in allies)
            {
                AgentSkillComponent component = ally.GetComponent<AgentSkillComponent>();
                if (component == null)
                    continue;
                GuWuBuff current = component.StateContainer.GetState("GuWuBuff") as GuWuBuff;
                if (current == null || current.Duration <= AiRefreshThreshold)
                    needingBuff++;
                if (needingBuff >= 2)
                    return true;
            }
            return false;
        }

        public override bool Activate(Agent agent)
        {
            if (agent == null || !agent.IsActive())
                return FailActivation("施法者不可用。");

            List<Agent> allies = GetAlliesInRange(agent);
            int affected = 0;
            foreach (Agent ally in allies)
            {
                AgentSkillComponent component = ally.GetComponent<AgentSkillComponent>();
                if (component == null)
                    continue;

                GuWuBuff current = component.StateContainer.GetState("GuWuBuff") as GuWuBuff;
                float longestRemaining = component.StateContainer.GetLongestStateDuration("GuWuBuff");
                float newDuration = MathF.Max(BuffDuration, longestRemaining);
                if (current != null)
                    newDuration += MathF.Max(0f,
                        GetAdditionalRefreshDuration(agent, ally, longestRemaining));

                component.StateContainer.AddOrReplaceState(new GuWuBuff(newDuration, agent), ally);
                affected++;
            }
            if (affected == 0)
                return FailActivation("范围内没有可鼓舞的单位。");

            string[] yells = agent.IsFemale ? FemaleYells : MaleYells;
            try { SoundManager.StartOneShotEvent(yells[MBRandom.RandomInt(yells.Length)], agent.Position); }
            catch (Exception) { /* 音频资源失效时不影响鼓舞生效。 */ }

            float maximumHealth = agent.HealthLimit > 0f
                ? agent.HealthLimit : agent.GetComponent<AgentSkillComponent>()?.MaxHP ?? agent.Health;
            if (agent.Health < maximumHealth)
                agent.Health = MathF.Min(maximumHealth,
                    agent.Health + maximumHealth * 0.3f);
            return true;
        }

        /// <summary>
        /// 特殊刷新条件的扩展点：先取旧剩余时间和新施放时间的较长者，再加返回的秒数。
        /// 默认不额外延长；只在目标已有鼓舞时调用。
        /// </summary>
        protected virtual float GetAdditionalRefreshDuration(
            Agent caster, Agent target, float longestRemaining)
        {
            return 0f;
        }

        private static List<Agent> GetAlliesInRange(Agent caster)
        {
            List<Agent> allies = Script.GetTargetedInRange(
                caster, caster.GetEyeGlobalPosition(), Range, true) ?? new List<Agent>();
            // 不依赖 IsFriendOf 对自身的判定；手动施放至少能鼓舞施法者。
            if (caster.IsHuman && !allies.Contains(caster))
                allies.Add(caster);
            return allies;
        }

        public class GuWuBuff : AgentBuff
        {
            private float _timeSinceLastTick;
            private GuWuFallingEmbersVisual _visual;
            private GuWuFallingEmbersVisual _lingeringVisual;

            public GuWuBuff(float duration, Agent source)
            {
                StateId = "GuWuBuff";
                Duration = duration;
                SourceAgent = source;
            }

            public override void OnApply(Agent agent)
            {
                agent?.UpdateAgentProperties();
                _visual = GuWuFallingEmbersVisual.Create(agent);
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                if (agent == null || !agent.IsActive() || dt <= 0f)
                    return;

                if (_visual != null && !_visual.Update(agent, dt))
                {
                    _visual = null;
                    _lingeringVisual = GuWuFallingEmbersVisual.CreateLingering(agent);
                }
                _lingeringVisual?.Update(agent, dt);

                AgentSkillComponent component = agent.GetComponent<AgentSkillComponent>();
                if (component == null)
                    return;

                _timeSinceLastTick += dt;
                while (_timeSinceLastTick >= 1f)
                {
                    _timeSinceLastTick -= 1f;
                    component.ChangeStamina(1f);
                    float maximumHealth = agent.HealthLimit > 0f
                        ? agent.HealthLimit : component.MaxHP;
                    float missingHealth = MathF.Max(0f, maximumHealth - agent.Health);
                    if (missingHealth > 0f)
                        agent.Health = MathF.Min(maximumHealth,
                            agent.Health + missingHealth * 0.05f);
                }
            }

            public override void OnRemove(Agent agent)
            {
                _visual?.Remove();
                _visual = null;
                _lingeringVisual?.Remove();
                _lingeringVisual = null;
                if (agent != null && agent.IsActive())
                    agent.UpdateAgentProperties();
            }
        }
    }
}
