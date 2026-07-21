using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.TacticalMap.Tracking
{
    /// <summary>
    /// 单位密度追踪：把存活单位按世界坐标累加到地形栅格上。
    /// 仅用于密度热力叠加层，按节流频率扫描一次 Mission.Agents（500v500 也极快）。
    /// </summary>
    public sealed class AgentTracker
    {
        private readonly Terrain.TerrainCache _cache;

        public AgentTracker(Terrain.TerrainCache cache)
        {
            _cache = cache;
        }

        public void Update(Mission mission)
        {
            if (mission == null || !_cache.IsBaked) return;

            // 清空密度与单位层
            _cache.ClearAgents();
            for (int x = 0; x < _cache.Width; x++)
                for (int y = 0; y < _cache.Height; y++)
                    _cache.Cells[x, y].DensityAgentCount = 0;

            foreach (var agent in mission.Agents)
            {
                if (agent == null || agent.Health <= 0f || !agent.IsHuman) continue;

                Vec2 p = agent.Position.AsVec2;
                Vec2 uv = _cache.WorldToUV(p);
                if (uv.X < 0f || uv.X > 1f || uv.Y < 0f || uv.Y > 1f) continue;

                int gx = (int)(uv.X * _cache.Width);
                int gy = (int)(uv.Y * _cache.Height);
                if (gx < 0) gx = 0; if (gx >= _cache.Width) gx = _cache.Width - 1;
                if (gy < 0) gy = 0; if (gy >= _cache.Height) gy = _cache.Height - 1;
                _cache.Cells[gx, gy].DensityAgentCount++;

                // 阵营着色：我方亮青、敌方纯红、中立灰
                byte r, g, b;
                if (agent.Team == null) { r = 180; g = 180; b = 180; }
                else if (agent.Team.IsPlayerTeam) { r = 0; g = 230; b = 255; }
                else { r = 255; g = 30; b = 30; }
                _cache.PaintAgent(gx, gy, r, g, b, 2);
            }
        }
    }
}
