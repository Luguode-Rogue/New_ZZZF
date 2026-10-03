using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal static class FriendlyMeleePassThroughRules
    {
        internal static bool ShouldSkipFriendlyHit(Agent attacker, Agent victim)
        {
            if (attacker == null || victim == null || attacker == victim || !attacker.IsFriendOf(victim)) return false;
            if (XieEZuZhou.IsCursed(attacker)) return false; return !KongNueCiFu.IsBlessed(attacker) || KongNueFriendlyFireRules.IsExempt(victim);
        }
    }
}