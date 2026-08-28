using System;
using System.Collections.Generic;
using New_ZZZF.TacticalMap.Diagnostics;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.TacticalMap.Tracking
{
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
        public List<Vec2> PathPoints { get; } = new List<Vec2>();
    }

    public sealed class FormationTracker
    {
        public List<FormationSnapshot> Snapshots { get; } = new List<FormationSnapshot>();

        public void Update(Mission mission)
        {
            Snapshots.Clear();
            if (mission == null) return;

            Agent mainAgent = mission.MainAgent;
            Vec2? playerWorld = mainAgent != null ? (Vec2?)mainAgent.Position.AsVec2 : null;
            Vec2 playerFacing = Vec2.Zero;
            if (mainAgent != null)
            {
                playerFacing = mainAgent.LookDirection.AsVec2;
                if (playerFacing.LengthSquared > 1E-4f)
                    playerFacing = playerFacing.Normalized();
            }

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
                        Vec2 displayFacing = new Vec2(-snap.Facing.X, snap.Facing.Y);
                        Vec2 playerDisplayFacing = new Vec2(-playerFacing.X, playerFacing.Y);

                        TacticalMapDirectionLog.Info(
                            "ENEMY_MAP_TRACE " +
                            "formation=" + snap.Name +
                            " count=" + snap.Count +
                            " world=(" + pos.X.ToString("F2") + "," + pos.Y.ToString("F2") + ")" +
                            " order=" + (snap.HasOrder
                                ? "(" + order.X.ToString("F2") + "," + order.Y.ToString("F2") + ")"
                                : "none") +
                            " currentDirection=(" + currentDirection.X.ToString("F4") + "," + currentDirection.Y.ToString("F4") + ")" +
                            " facing=(" + snap.Facing.X.ToString("F4") + "," + snap.Facing.Y.ToString("F4") + ")" +
                            " displayFacing=(" + displayFacing.X.ToString("F4") + "," + displayFacing.Y.ToString("F4") + ")" +
                            " playerWorld=" + (playerWorld.HasValue
                                ? "(" + playerWorld.Value.X.ToString("F2") + "," + playerWorld.Value.Y.ToString("F2") + ")"
                                : "none") +
                            " playerFacingRaw=(" + playerFacing.X.ToString("F4") + "," + playerFacing.Y.ToString("F4") + ")" +
                            " playerFacingDisplay=(" + playerDisplayFacing.X.ToString("F4") + "," + playerDisplayFacing.Y.ToString("F4") + ")" +
                            " deltaPlayer=(" + deltaFromPlayer.X.ToString("F2") + "," + deltaFromPlayer.Y.ToString("F2") + ")" +
                            " deltaOrder=(" + deltaToOrder.X.ToString("F2") + "," + deltaToOrder.Y.ToString("F2") + ")");
                    }
                }
            }
        }
    }
}