using System.Collections.Generic;
using New_ZZZF.TacticalMap.Diagnostics;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.TacticalMap.Tracking
{
    /// <summary>
    /// Compact formation state used by the tactical map.
    /// </summary>
    public sealed class FormationSnapshot
    {
        public bool IsPlayer;
        public bool IsEnemy;
        public bool IsNeutral;
        public Vec2 AveragePosition;
        public Vec2 Facing;
        public bool HasOrder;
        public Vec2 OrderPosition;
        public uint Color;
        public int Count;
        public string Name;
    }

    /// <summary>
    /// Refreshes formation-level situational awareness at a throttled rate.
    /// CurrentDirection is authoritative for the actual movement/facing indicator.
    /// OrderPosition is only exposed as a forward destination when it lies in front of the formation.
    /// </summary>
    public sealed class FormationTracker
    {
        public List<FormationSnapshot> Snapshots { get; } = new List<FormationSnapshot>();

        public void Update(Mission mission)
        {
            Snapshots.Clear();
            if (mission == null) return;

            Agent mainAgent = mission.MainAgent;
            Vec2? playerWorld = mainAgent != null ? (Vec2?)mainAgent.Position.AsVec2 : null;
            var playerTeam = mission.PlayerTeam;
            foreach (var team in mission.Teams)
            {
                if (team == null) continue;
                bool isPlayer = team.IsPlayerTeam;
                bool isEnemy = !isPlayer && playerTeam != null && playerTeam.IsEnemyOf(team);
                bool isNeutral = !isPlayer && !isEnemy;
                var formations = team.FormationsIncludingEmpty;
                if (formations == null) continue;

                foreach (var formation in formations)
                {
                    if (formation == null || formation.CountOfUnits <= 0) continue;

                    Vec2 rawCurrentDirection = formation.CurrentDirection;
                    Vec2 currentDirection = rawCurrentDirection.LengthSquared > 1E-4f
                        ? rawCurrentDirection.Normalized()
                        : Vec2.Zero;

                    bool hasOrder = formation.OrderPositionIsValid;
                    Vec2 orderPosition = hasOrder ? formation.OrderPosition : formation.CachedAveragePosition;
                    Vec2 directionToOrder = hasOrder ? orderPosition - formation.CachedAveragePosition : Vec2.Zero;

                    // OrderPosition can remain behind a moving formation. In that case it is not a useful
                    // "next destination" for the tactical display, so suppress the destination line.
                    if (hasOrder && directionToOrder.LengthSquared > 1E-4f)
                    {
                        Vec2 orderDirection = directionToOrder.Normalized();
                        if (currentDirection.LengthSquared > 1E-4f &&
                            Vec2.DotProduct(currentDirection, orderDirection) <= 0f)
                        {
                            hasOrder = false;
                            orderPosition = formation.CachedAveragePosition;
                            directionToOrder = Vec2.Zero;
                        }
                    }

                    // Actual formation direction takes priority over the order point.
                    Vec2 facing = currentDirection;
                    if (facing.LengthSquared <= 1E-4f && directionToOrder.LengthSquared > 1E-4f)
                        facing = directionToOrder.Normalized();

                    var snap = new FormationSnapshot
                    {
                        IsPlayer = isPlayer,
                        IsEnemy = isEnemy,
                        IsNeutral = isNeutral,
                        AveragePosition = formation.CachedAveragePosition,
                        Color = team.Color,
                        Count = formation.CountOfUnits,
                        Name = formation.FormationIndex.ToString(),
                        HasOrder = hasOrder,
                        OrderPosition = orderPosition,
                        Facing = facing
                    };

                    Snapshots.Add(snap);

                    if (isEnemy)
                    {
                        Vec2 pos = snap.AveragePosition;
                        Vec2 order = snap.OrderPosition;
                        Vec2 deltaFromPlayer = playerWorld.HasValue ? pos - playerWorld.Value : Vec2.Zero;
                        Vec2 deltaToOrder = snap.HasOrder ? order - pos : Vec2.Zero;

                        TacticalMapDirectionLog.Info(
                            "ENEMY_MAP_TRACE " +
                            "formation=" + snap.Name +
                            " count=" + snap.Count +
                            " world=(" + pos.X.ToString("F2") + "," + pos.Y.ToString("F2") + ")" +
                            " order=" + (snap.HasOrder
                                ? "(" + order.X.ToString("F2") + "," + order.Y.ToString("F2") + ")"
                                : "suppressed") +
                            " currentDirection=(" + currentDirection.X.ToString("F4") + "," + currentDirection.Y.ToString("F4") + ")" +
                            " facing=(" + snap.Facing.X.ToString("F4") + "," + snap.Facing.Y.ToString("F4") + ")" +
                            " displayFacing=(" + snap.Facing.X.ToString("F4") + "," + snap.Facing.Y.ToString("F4") + ")" +
                            " playerWorld=" + (playerWorld.HasValue
                                ? "(" + playerWorld.Value.X.ToString("F2") + "," + playerWorld.Value.Y.ToString("F2") + ")"
                                : "none") +
                            " deltaPlayer=(" + deltaFromPlayer.X.ToString("F2") + "," + deltaFromPlayer.Y.ToString("F2") + ")" +
                            " deltaOrder=(" + deltaToOrder.X.ToString("F2") + "," + deltaToOrder.Y.ToString("F2") + ")");
                    }
                }
            }
        }
    }
}
