using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal static class BkbCombatRules
    {
        internal static bool IsActive(Agent agent) => agent?.GetComponent<AgentSkillComponent>()?.StateContainer.HasState("BKBBuff") == true;
        internal static bool IsOverhead(Agent attacker, Agent.UsageDirection direction, StrikeType strikeType) =>
            IsActive(attacker) && direction == Agent.UsageDirection.AttackUp && strikeType == StrikeType.Swing &&
            attacker.WieldedWeapon.CurrentUsageItem?.IsMeleeWeapon == true;
    }
}
