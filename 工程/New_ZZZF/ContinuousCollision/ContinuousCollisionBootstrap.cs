using System;
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
            if (mission == null)
            {
                ContinuousCollisionLog.Warn("Bootstrap skipped: mission is null.");
                return;
            }

            if (mission.GetMissionBehavior<ContinuousCollisionMissionLogic>() != null)
            {
                ContinuousCollisionLog.Trace("Bootstrap skipped: ContinuousCollisionMissionLogic already exists.");
                return;
            }

            try
            {
                mission.AddMissionBehavior(new ContinuousCollisionMissionLogic());
                ContinuousCollisionLog.Info("Bootstrap injected ContinuousCollisionMissionLogic into mission.");
            }
            catch (Exception ex)
            {
                ContinuousCollisionLog.Error("Bootstrap failed to inject ContinuousCollisionMissionLogic.", ex);
            }
        }
    }
}
