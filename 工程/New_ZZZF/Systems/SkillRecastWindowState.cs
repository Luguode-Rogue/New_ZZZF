using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 通用的按施法者隔离的再施法窗口。首次施法成功后挂载；持续期间技能可通过
    /// GetActivationPolicy 改写本次费用和个人冷却，不影响法术公共冷却。
    /// </summary>
    public sealed class SkillRecastWindowState : AgentBuff
    {
        private const string StatePrefix = "SkillRecastWindow:";

        public SkillRecastWindowState(string skillId, float duration, Agent caster)
        {
            StateId = StatePrefix + skillId;
            Duration = duration;
            TargetAgent = caster;
        }

        public static bool IsActive(Agent caster, string skillId)
        {
            AgentSkillComponent component = caster?.GetComponent<AgentSkillComponent>();
            AgentBuff state = component?.StateContainer.GetState(StatePrefix + skillId);
            return state != null && state.Duration > 0f;
        }

        public override void OnApply(Agent agent)
        {
            agent?.GetComponent<AgentSkillComponent>()?.NotifySkillAvailabilityChanged();
        }

        public override void OnUpdate(Agent agent, float dt) { }

        public override void OnRemove(Agent agent)
        {
            agent?.GetComponent<AgentSkillComponent>()?.NotifySkillAvailabilityChanged();
        }
    }
}
