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
    /// The map uses the current order position when available, so the display communicates intent as well as location.
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

                    var snap = new FormationSnapshot
                    {
                        IsPlayer = isPlayer,
                        IsEnemy = isEnemy,
                        IsNeutral = isNeutral,
                        AveragePosition = formation.CachedAveragePosition,
                        Color = team.Color,
                        Count = formation.CountOfUnits,
                        Name = formation.FormationIndex.ToString(),
                        HasOrder = formation.OrderPositionIsValid,
                        OrderPosition = formation.OrderPositionIsValid ? formation.OrderPosition : formation.CachedAveragePosition
                    };

                    Vec2 facing = Vec2.Zero;
                    if (formation.OrderPositionIsValid)
                    {
                        Vec2 directionToOrder = formation.OrderPosition - formation.CachedAveragePosition;
                        if (directionToOrder.LengthSquared > 1E-4f)
                            facing = directionToOrder.Normalized();
                    }
                    if (facing.LengthSquared <= 1E-4f && formation.CurrentDirection.LengthSquared > 1E-4f)
                        facing = formation.CurrentDirection.Normalized();
                    snap.Facing = facing;

                    Snapshots.Add(snap);

                    if (isEnemy)
                    {
                        Vec2 pos = snap.AveragePosition;
                        Vec2 order = snap.OrderPosition;
                        Vec2 deltaFromPlayer = playerWorld.HasValue ? pos - playerWorld.Value : Vec2.Zero;
                        Vec2 deltaToOrder = snap.HasOrder ? order - pos : Vec2.Zero;

                        TacticalMapLog.Info(
                            "ENEMY_MAP_TRACE " +
                            "formation=" + snap.Name +
                            " count=" + snap.Count +
                            " world=(" + pos.X.ToString("F2") + "," + pos.Y.ToString("F2") + ")" +
                            " order=" + (snap.HasOrder
                                ? "(" + order.X.ToString("F2") + "," + order.Y.ToString("F2") + ")"
                                : "none") +
                            " facing=(" + snap.Facing.X.ToString("F4") + "," + snap.Facing.Y.ToString("F4") + ")" +
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
