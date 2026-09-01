using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>Thin wrapper around Bannerlord's native AI pathfinder.</summary>
    public sealed class NavigationPathService
    {
        private readonly Scene _scene;
        private readonly float _agentRadius;

        public NavigationPathService(Scene scene, float agentRadius = 0.5f)
        {
            _scene = scene;
            _agentRadius = Math.Max(0.05f, agentRadius);
        }

        public bool TryGetPath(Vec2 start, Vec2 destination, out List<Vec2> points)
        {
            points = new List<Vec2>();
            if (_scene == null) return false;

            PathFaceRecord startFace = PathFaceRecord.NullFaceRecord;
            PathFaceRecord endFace = PathFaceRecord.NullFaceRecord;
            try
            {
                float startZ = _scene.GetGroundHeightAtPosition(new Vec3(start.X, start.Y, 100f));
                float endZ = _scene.GetGroundHeightAtPosition(new Vec3(destination.X, destination.Y, 100f));
                _scene.GetNavMeshFaceIndex(ref startFace, new Vec3(start.X, start.Y, startZ), true);
                _scene.GetNavMeshFaceIndex(ref endFace, new Vec3(destination.X, destination.Y, endZ), true);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("NavigationPathService face lookup failed.", ex);
                return false;
            }

            if (!startFace.IsValid() || !endFace.IsValid())
                return false;

            try
            {
                var path = new NavigationPath();
                bool success = _scene.GetPathBetweenAIFaces(
                    startFace.FaceIndex,
                    endFace.FaceIndex,
                    start,
                    destination,
                    _agentRadius,
                    path,
                    null,
                    1f);

                if (!success || path.PathPoints == null || path.Size <= 0)
                    return false;

                for (int i = 0; i < path.Size && i < path.PathPoints.Length; i++)
                    points.Add(path.PathPoints[i]);

                return points.Count >= 2;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("NavigationPathService path query failed.", ex);
                return false;
            }
        }
    }
}
