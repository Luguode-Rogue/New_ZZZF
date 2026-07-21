using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.TacticalMap.Tracking
{
    /// <summary>
    /// 单个编队的轻量快照（用于绘制小地图标记）。
    /// </summary>
    public sealed class FormationSnapshot
    {
        public bool IsPlayer;
        public bool IsEnemy;        // 相对玩家是否为敌方队伍（用于红/绿框区分）
        public Vec2 AveragePosition;  // 世界坐标
        public Vec2 Facing;           // 归一化朝向（无有效朝向时为 0）
        public uint Color;
        public int Count;
        public string Name;
    }

    /// <summary>
    /// 每帧（节流）扫描所有队伍的非空编队，生成快照。
    /// 编队数量通常 ≤ 数十，远小于单位数，因此标记层与单位数无关、对 500v500 零压力。
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
                var formations = team.FormationsIncludingEmpty;
                if (formations == null) continue;

                foreach (var formation in formations)
                {
                    if (formation == null || formation.CountOfUnits <= 0) continue;

                    var snap = new FormationSnapshot
                    {
                        IsPlayer = isPlayer,
                        IsEnemy = isEnemy,
                        AveragePosition = formation.CachedAveragePosition,
                        Color = team.Color,
                        Count = formation.CountOfUnits,
                        Name = formation.FormationIndex.ToString()
                    };

                    // 朝向：优先用当前指令目标方向，否则用编队当前方向
                    // 注意：Formation.CurrentDirection 本身就是 Vec2（forward），不是 Mat3
                    Vec2 facing = Vec2.Zero;
                    if (formation.OrderPositionIsValid)
                    {
                        Vec2 d = formation.OrderPosition - formation.CachedAveragePosition;
                        if (d.LengthSquared > 1E-4f) facing = d.Normalized();
                    }
                    if (facing.LengthSquared <= 1E-4f && formation.CurrentDirection.LengthSquared > 1E-4f)
                    {
                        facing = formation.CurrentDirection.Normalized();
                    }
                    snap.Facing = facing;

                    Snapshots.Add(snap);
                }
            }
        }
    }
}
