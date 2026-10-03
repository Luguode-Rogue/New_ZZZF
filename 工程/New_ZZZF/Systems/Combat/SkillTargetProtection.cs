using TaleWorlds.MountAndBlade;
namespace New_ZZZF
{
    // 手动技能选取的统一保护规则；不改变原生武器/投射物碰撞。
    internal static class SkillTargetProtection
    {
        public static bool IsProtected(Agent target) {
            Agent owner = target?.IsMount == true ? target.RiderAgent : target;
            AgentBuffContainer states = owner?.GetComponent<AgentSkillComponent>()?.StateContainer;
            return states != null && (states.HasState("BKBBuff") || states.HasState("TianQiBuff"));
        }
        public static bool CanSelect(Agent target) => target != null && target.IsActive() && !IsProtected(target);
    }
}
