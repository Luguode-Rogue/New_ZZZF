using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 点击模式 -> 实际编队指令 的路由层。
    /// 这里只调用 Bannerlord 1.4.6 已验证存在的 Formation/Order API。
    /// </summary>
    public enum TacticalClickMode
    {
        Move,        // 移动到点（保留阵型）
        AttackMove,  // 推进/攻击移动
        Face,        // 朝向某点
        Stop         // 原地停止
    }

    public sealed class OrderSystem
    {
        private readonly Terrain.TerrainCache _cache;

        public OrderSystem(Terrain.TerrainCache cache)
        {
            _cache = cache;
        }

        public void IssueOrder(Mission mission, Vec2 worldPos, TacticalClickMode mode)
        {
            if (mission == null || mission.Scene == null) return;
            var formations = SelectionSystem.GetTargetFormations(mission);
            if (formations.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("战术地图：未选择任何编队", new Color(1f, 0.6f, 0.1f, 1f)));
                return;
            }

            float height = 0f;
            try { height = mission.Scene.GetTerrainHeight(worldPos, true); } catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] 取地形高度失败: {ex.Message}")); }

            int issued = 0;
            foreach (var formation in formations)
            {
                if (formation == null) continue;
                switch (mode)
                {
                    case TacticalClickMode.Move:
                        formation.SetMovementOrder(MovementOrder.MovementOrderMove(
                            new WorldPosition(mission.Scene, new Vec3(worldPos.X, worldPos.Y, height))));
                        break;
                    case TacticalClickMode.AttackMove:
                        formation.SetMovementOrder(MovementOrder.MovementOrderAdvance);
                        break;
                    case TacticalClickMode.Face:
                        {
                            Vec2 dir = worldPos - formation.CachedAveragePosition;
                            if (dir.LengthSquared > 1E-4f)
                                formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(dir.Normalized()));
                        }
                        break;
                    case TacticalClickMode.Stop:
                        formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                        break;
                }
                issued++;
            }

            if (issued > 0)
            {
                string label = mode == TacticalClickMode.Move ? "移动"
                    : mode == TacticalClickMode.AttackMove ? "推进"
                    : mode == TacticalClickMode.Face ? "朝向" : "停止";
                InformationManager.DisplayMessage(new InformationMessage($"战术地图：已向 {issued} 个编队下达[{label}]指令", new Color(0.2f, 0.9f, 1f, 1f)));
            }
        }
    }

    /// <summary>
    /// 选择系统：返回应接收指令的编队（优先玩家当前选中的编队）。
    /// </summary>
    public static class SelectionSystem
    {
        public static List<Formation> GetTargetFormations(Mission mission)
        {
            var result = new List<Formation>();
            if (mission == null || mission.PlayerTeam == null) return result;

            var oc = mission.PlayerTeam.PlayerOrderController;
            if (oc != null && oc.SelectedFormations != null && oc.SelectedFormations.Count > 0)
            {
                foreach (var f in oc.SelectedFormations) result.Add(f);
                return result;
            }

            // 未选中时，发给玩家所有非空编队
            var forms = mission.PlayerTeam.FormationsIncludingEmpty;
            if (forms != null)
            {
                foreach (var f in forms)
                    if (f != null && f.CountOfUnits > 0) result.Add(f);
            }
            return result;
        }
    }
}
