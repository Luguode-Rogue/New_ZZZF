using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>仅在天神下凡结束后到该次冷却完成之间缩小，不产生额外属性惩罚。</summary>
    internal sealed class BkbCooldownShrinkBuff : AgentBuff
    {
        private readonly SkillBase _skill;
        private readonly float _originalScale;
        private bool _restored;
        public override bool IsMarker => true;
        public override bool AffectsOwner => false;
        internal BkbCooldownShrinkBuff(Agent owner, SkillBase skill, float duration, float originalScale)
        {
            StateId = "BkbCooldownShrink";
            SourceAgent = owner;
            TargetAgent = owner;
            Duration = duration;
            _skill = skill;
            _originalScale = originalScale;
        }
        public override void OnApply(Agent agent) => BkbTransformationVisual.ApplyScale(agent, _originalScale * 0.75f);
        public override void OnUpdate(Agent agent, float dt)
        {
            float remaining = agent.GetComponent<AgentSkillComponent>()?.GetSkillCooldownForDisplay(_skill) ?? 0f;
            if (remaining <= 0f) { Cancel(agent); return; }
            Duration = remaining;
        }
        internal void Cancel(Agent agent)
        {
            Duration = 0f;
            if (_restored) return;
            _restored = true;
            BkbTransformationVisual.ApplyScale(agent, _originalScale);
        }
        public override void OnRemove(Agent agent) => Cancel(agent);
    }
}
