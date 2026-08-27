using System.Collections.Generic;
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
                }
            }
        }
    }
}
