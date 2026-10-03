using System;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal static class FortitudeDamageRules
    {
        internal static float LimitFinalDamage(Agent victim, float damage) {
            if (damage <= 0f) return damage;
            var states = victim?.GetComponent<AgentSkillComponent>()?.StateContainer;
            if (states == null || states.GetLongestStateDuration(JianRenBuQu.BuffId) <= 0f) return damage;
            return Math.Min(1f, damage);
        }
    }
}