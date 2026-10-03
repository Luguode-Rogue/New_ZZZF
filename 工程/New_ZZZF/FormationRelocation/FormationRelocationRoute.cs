using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.FormationRelocation
{
    internal static class FormationRelocationPlacement
    {
        internal static bool TryTarget(Mission mission, WorldPosition orderPosition,
            Vec2 point, Team team, out WorldPosition target)
        {
            target = WorldPosition.Invalid;
            if (mission?.Scene == null || !orderPosition.IsValid || !point.IsValid ||
                !mission.IsPositionInsideBoundaries(point)) return false;

            // Match native OrderController placement: copy the clicked world
            // position and move it in XY so WorldPosition resolves the surface.
            // Rebuilding from terrain height can select ground below a bridge
            // or wall instead of the walkable surface chosen by the player.
            target = orderPosition;
            target.SetVec2(point);
            return mission.IsOrderPositionAvailable(target, team);
        }
    }

    internal sealed class FormationRelocationSlot
    {
        internal readonly Formation Formation;
        internal readonly Vec2 Offset;

        internal FormationRelocationSlot(Formation formation, Vec2 start, Vec2 anchor)
        {
            Formation = formation;
            Offset = start - anchor;
        }
    }
}
