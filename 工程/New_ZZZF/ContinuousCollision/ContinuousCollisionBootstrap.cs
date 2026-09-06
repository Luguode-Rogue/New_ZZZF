using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.ContinuousCollision
{
    /// <summary>
    /// Hooks the existing mission initialization path without modifying the large legacy SubModule file.
    /// </summary>
    [HarmonyPatch(typeof(New_ZZZF.SubModule), nameof(New_ZZZF.SubModule.OnMissionBehaviorInitialize))]
    internal static class ContinuousCollisionBootstrap
    {
        private static void Postfix(Mission mission)
        {
            if (mission == null || mission.GetMissionBehavior<ContinuousCollisionMissionLogic>() != null)
            {
                return;
            }

            mission.AddMissionBehavior(new ContinuousCollisionMissionLogic());
        }
    }
}
