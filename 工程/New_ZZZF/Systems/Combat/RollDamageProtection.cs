using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal static class RollDamageProtection
    {
        internal static bool IsActive(Agent agent) => RushMovementMissionLogic.Current?.IsRolling(agent) == true;
    }
    [HarmonyPatch(typeof(Agent), "HandleBlow")]
    internal static class RollBlowImmunityPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool Prefix(Agent __instance, ref Blow b, ref AttackCollisionData collisionData)
        {
            if (!RollDamageProtection.IsActive(__instance) || WeaponCombatMissionLogic.DeliveringBleed) return true;
            b.InflictedDamage = 0;
            collisionData.InflictedDamage = 0;
            return false;
        }
    }
}
