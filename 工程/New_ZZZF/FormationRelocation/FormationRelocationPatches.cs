using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.FormationRelocation
{
    internal static class FormationRelocationPatches
    {
        // This prefix is registered explicitly by FormationRelocationBootstrap, not PatchAll.
        internal static bool BeforeLineMove(OrderController __instance, OrderType orderType,
            WorldPosition position1, WorldPosition position2)
        {
            if (orderType != OrderType.MoveToLineSegment &&
                orderType != OrderType.MoveToLineSegmentWithHorizontalLayout)
                return true;

            FormationRelocationMissionLogic logic = FormationRelocationMissionLogic.Active;
            if (logic == null || !logic.Owns(__instance) ||
                !logic.HasSelectedGroup(__instance)) return true;

            logic.Issue(__instance, position1, position2);
            return false;
        }

        internal static bool BeforePointMove(OrderController __instance, OrderType orderType,
            WorldPosition orderPosition)
        {
            if (orderType != OrderType.Move) return true;
            FormationRelocationMissionLogic logic = FormationRelocationMissionLogic.Active;
            if (logic == null || !logic.Owns(__instance) ||
                !logic.HasSelectedGroup(__instance)) return true;

            logic.Issue(__instance, orderPosition, orderPosition);
            return false;
        }

        internal static bool BeforeMountedFormationSpeed(Agent ___Agent, ref float __result)
        {
            // AgentComponent.Agent is a protected field, supplied by Harmony.
            Agent rider = ___Agent;
            Mission mission = Mission.Current;
            Formation formation = rider?.Formation;
            if (mission == null || mission.Mode != MissionMode.Battle ||
                GameNetwork.IsSessionActive || mission.PlayerTeam == null ||
                rider == null || !rider.IsHuman || !rider.IsActive() ||
                rider.Controller != AgentControllerType.AI ||
                rider.MountAgent == null || rider.IsDetachedFromFormation ||
                formation == null || formation.Team != mission.PlayerTeam)
                return true;

            MovementOrder.MovementOrderEnum order =
                formation.GetReadonlyMovementOrderReference().OrderEnum;
            if (order != MovementOrder.MovementOrderEnum.Move &&
                order != MovementOrder.MovementOrderEnum.Charge &&
                order != MovementOrder.MovementOrderEnum.ChargeToTarget)
                return true;

            // Native formation correction can clamp mounted troops to 20% even
            // during charge. Remove only that multiplier; native agent stats,
            // skill debuffs, destination braking and collision handling still apply.
            __result = 1f;
            return false;
        }

    }
}
