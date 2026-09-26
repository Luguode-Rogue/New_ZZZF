using New_ZZZF.Systems;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class TianQi : SkillBase
    {
        private const float HeroDuration = 30f;
        private const float HeroCooldown = 60f;
        private const float StaminaCost = 50f;

        public TianQi()
        {
            SkillID = "TianQi";
            Type = SPSkillType.MainActive;
            Cooldown = HeroCooldown;
            ResourceCost = StaminaCost;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0019}TianQi");
            Description = new TaleWorlds.Localization.TextObject(
                "{=ZZZF0020}开启时回满生命，持续期间免疫物理与魔法伤害，造成的伤害提高100%。装备天启且冷却就绪时，受到致命攻击会自动无消耗触发并抵消该次伤害，持续时间减半、冷却时间加倍。消耗耐力：50。英雄持续30秒、冷却60秒；非英雄持续15秒、冷却30秒。");
        }

        public override SkillActivationPolicy GetActivationPolicy(Agent caster)
        {
            return new SkillActivationPolicy(StaminaCost, false,
                caster != null && !caster.IsHero ? HeroCooldown * 0.5f : HeroCooldown);
        }

        public override bool Activate(Agent agent)
        {
            AgentSkillComponent component = agent?.GetComponent<AgentSkillComponent>();
            if (component == null || component.StateContainer.HasState("TianQiBuff"))
                return FailActivation("天启已经生效或技能组件不可用。");
            return ApplyBuff(agent, component, GetDuration(agent));
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster)) return false;
            AgentSkillComponent component = caster.GetComponent<AgentSkillComponent>();
            if (component == null || component.StateContainer.HasState("TianQiBuff") ||
                caster.Health > GetMaximumHealth(caster, component) * 0.5f)
                return false;

            Agent target = caster.GetTargetAgent();
            return target != null && target.IsActive() && target.Health > 0f &&
                   caster.IsEnemyOf(target) &&
                   (target.Position.AsVec2 - caster.Position.AsVec2).LengthSquared <= 225f;
        }

        internal static bool IsProtected(Agent agent)
        {
            return agent?.GetComponent<AgentSkillComponent>()?.StateContainer
                .HasState("TianQiBuff") == true;
        }

        /// <summary>由物理命中结算或魔法伤害脚本在扣血之前调用。</summary>
        internal static bool TryTriggerEmergency(Agent victim, float incomingDamage)
        {
            if (victim == null || !victim.IsActive() || victim.Health <= 0f ||
                incomingDamage <= 0f || float.IsNaN(incomingDamage) ||
                float.IsInfinity(incomingDamage) || incomingDamage < victim.Health)
                return false;

            AgentSkillComponent component = victim.GetComponent<AgentSkillComponent>();
            if (component == null || !(component.MainActiveSkill is TianQi skill) ||
                component.StateContainer.HasState("TianQiBuff") ||
                component._cooldownTimers.TryGetValue(skill, out float remaining) && remaining > 0f)
                return false;

            if (!ApplyBuff(victim, component, GetDuration(victim) * 0.5f))
                return false;

            component._cooldownTimers[skill] = skill.GetActivationPolicy(victim).CooldownOnSuccess * 2f;
            component.NotifySkillAvailabilityChanged();
            return true;
        }

        private static bool ApplyBuff(Agent agent, AgentSkillComponent component, float duration)
        {
            if (agent == null || component == null || !agent.IsActive() ||
                component.StateContainer.HasState("TianQiBuff"))
                return false;
            component.StateContainer.AddState(new TianQiBuff(duration, agent), agent);
            agent.Health = GetMaximumHealth(agent, component);
            return true;
        }

        private static float GetDuration(Agent agent)
        {
            return agent != null && !agent.IsHero ? HeroDuration * 0.5f : HeroDuration;
        }

        private static float GetMaximumHealth(Agent agent, AgentSkillComponent component)
        {
            return agent.HealthLimit > 0f ? agent.HealthLimit :
                System.Math.Max(1f, component.MaxHP);
        }

        public class TianQiBuff : AgentBuff
        {
            private GameEntity _shield;

            public TianQiBuff(float duration, Agent source)
            {
                StateId = "TianQiBuff";
                Duration = duration;
                SourceAgent = source;
            }

            public override void OnApply(Agent agent)
            {
                _shield = Script.CreateEggShellVisual(agent,
                    new TaleWorlds.Library.Color(1f, 0.79f, 0.2f, 0.34f));
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                // 场景实体暂不可用时，下一帧再尝试创建。
                if (_shield == null)
                    _shield = Script.CreateEggShellVisual(agent,
                        new TaleWorlds.Library.Color(1f, 0.79f, 0.2f, 0.34f));
                else
                    Script.UpdateEggShellVisual(_shield, agent);
            }

            public override void OnRemove(Agent agent)
            {
                if (_shield != null)
                    _shield.Remove(0);
                _shield = null;
            }
        }
    }
}
