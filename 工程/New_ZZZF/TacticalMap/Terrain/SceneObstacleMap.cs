using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// Converts actual stationary scene collision geometry into a tactical obstacle footprint.
    /// Visual-only meshes, agents, dynamic debris and raycast-only helpers are ignored.
    /// </summary>
    public static class SceneObstacleMap
    {
        private sealed class Baseline
        {
            public byte[] TerrainBase;
        }

        private static readonly ConditionalWeakTable<TerrainCache, Baseline> Baselines =
            new ConditionalWeakTable<TerrainCache, Baseline>();

        public static void Rebuild(TerrainCache cache, Scene scene)
        {
            if (cache == null || scene == null || !cache.IsBaked || cache.TerrainBaseRGBA == null)
                return;

            try
            {
                Baseline baseline;
                if (!Baselines.TryGetValue(cache, out baseline) || baseline == null || baseline.TerrainBase == null ||
                    baseline.TerrainBase.Length != cache.TerrainBaseRGBA.Length)
                {
                    baseline = new Baseline { TerrainBase = (byte[])cache.TerrainBaseRGBA.Clone() };
                    Baselines.Remove(cache);
                    Baselines.Add(cache, baseline);
                }
                else
                {
                    Buffer.BlockCopy(baseline.TerrainBase, 0, cache.TerrainBaseRGBA, 0, baseline.TerrainBase.Length);
                }

                int obstacleCells = 0;
                int obstacleEntities = 0;
                List<GameEntity> entities = new List<GameEntity>();
                scene.GetEntities(ref entities);

                for (int i = 0; i < entities.Count; i++)
                {
                    GameEntity entity = entities[i];
                    if (entity == null) continue;

                    BodyFlags flags;
                    try
                    {
                        flags = entity.PhysicsDescBodyFlag;
                        if (flags == BodyFlags.None)
                            flags = entity.BodyFlag;
                    }
                    catch
                    {
                        continue;
                    }

                    if (!IsUsableObstacleBody(flags)) continue;

                    Vec3 bbMin;
                    Vec3 bbMax;
                    try
                    {
                        bbMin = entity.GetBoundingBoxMin();
                        bbMax = entity.GetBoundingBoxMax();
                    }
                    catch
                    {
                        continue;
                    }

                    if (!bbMin.IsValid || !bbMax.IsValid) continue;
                    if (bbMax.X <= bbMin.X || bbMax.Y <= bbMin.Y || bbMax.Z <= bbMin.Z) continue;

                    float width = bbMax.X - bbMin.X;
                    float depth = bbMax.Y - bbMin.Y;
                    float height = bbMax.Z - bbMin.Z;
                    if (width < 0.10f || depth < 0.10f || height < 0.15f) continue;

                    // Ignore scene-wide collision shells / terrain proxy bodies.
                    bool hugeX = width > cache.WorldW * 0.75f;
                    bool hugeY = depth > cache.WorldH * 0.75f;
                    if (hugeX && hugeY) continue;

                    // A stationary physics object should intersect the local ground surface.
                    // This rejects roofs, floating decoration and high editor-only collision boxes.
                    float sampleX = (bbMin.X + bbMax.X) * 0.5f;
                    float sampleY = (bbMin.Y + bbMax.Y) * 0.5f;
                    float groundZ = cache.GetHeightAt(new Vec2(sampleX, sampleY));
                    if (float.IsNaN(groundZ) || float.IsInfinity(groundZ)) continue;
                    if (bbMin.Z > groundZ + Math.Max(3.0f, cache.CellStep * 0.8f)) continue;
                    if (bbMax.Z < groundZ - Math.Max(2.0f, cache.CellStep * 0.5f)) continue;

                    int minX = WorldToCellX(cache, bbMin.X);
                    int maxX = WorldToCellX(cache, bbMax.X);
                    int minY = WorldToCellY(cache, bbMin.Y);
                    int maxY = WorldToCellY(cache, bbMax.Y);
                    if (maxX < 0 || minX >= cache.Width || maxY < 0 || minY >= cache.Height) continue;

                    minX = Math.Max(0, minX);
                    maxX = Math.Min(cache.Width - 1, maxX);
                    minY = Math.Max(0, minY);
                    maxY = Math.Min(cache.Height - 1, maxY);

                    bool entityHit = false;
                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            Vec2 cellPos = cache.CellCenter(x, y);

                            // Global AABB is only used to find candidates. Reject cells that fall
                            // outside a narrow footprint for highly elongated barriers, which is
                            // where the previous implementation produced long false-positive bands.
                            if (!InsideObstacleFootprint(bbMin, bbMax, cellPos, cache.CellStep))
                                continue;

                            TerrainCell cell = cache.Cells[x, y];
                            if (cell.IsWater) continue;

                            cell.Kind = TerrainKind.Wall;
                            cell.MovementCost = 1f;
                            cell.Risk = 1f;
                            cache.SetPixel(cache.TerrainBaseRGBA, x, y, 44, 40, 34, 255);

                            obstacleCells++;
                            entityHit = true;
                        }
                    }

                    if (entityHit) obstacleEntities++;
                }

                TacticalMapLog.Info(
                    "SceneObstacleMap rebuild complete. Entities=" + entities.Count +
                    " obstacleEntities=" + obstacleEntities +
                    " obstacleCells=" + obstacleCells);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("SceneObstacleMap.Rebuild failed.", ex);
            }
        }

        private static bool IsUsableObstacleBody(BodyFlags flags)
        {
            if (flags == BodyFlags.None) return false;

            BodyFlags excluded = BodyFlags.Disabled |
                                 BodyFlags.Dynamic |
                                 BodyFlags.Moveable |
                                 BodyFlags.DynamicConvexHull |
                                 BodyFlags.Ladder |
                                 BodyFlags.HasSteps |
                                 BodyFlags.Ragdoll |
                                 BodyFlags.RagdollLimiter |
                                 BodyFlags.DroppedItem |
                                 BodyFlags.FloatingDebris |
                                 BodyFlags.WaterBody |
                                 BodyFlags.AgentOnly |
                                 BodyFlags.MissileOnly |
                                 BodyFlags.OnlyCollideWithRaycast;

            if ((flags & excluded) != 0) return false;
            return true;
        }

        private static bool InsideObstacleFootprint(Vec3 min, Vec3 max, Vec2 point, float cellStep)
        {
            float width = max.X - min.X;
            float depth = max.Y - min.Y;
            float pad = Math.Max(0.12f, cellStep * 0.30f);

            // Thin barriers should remain thin on the tactical map instead of being expanded
            // into a many-cell-wide band by an axis-aligned bounding box.
            if (width > depth * 5f)
            {
                float centerY = (min.Y + max.Y) * 0.5f;
                return point.Y >= centerY - Math.Max(pad, depth * 0.65f) &&
                       point.Y <= centerY + Math.Max(pad, depth * 0.65f);
            }

            if (depth > width * 5f)
            {
                float centerX = (min.X + max.X) * 0.5f;
                return point.X >= centerX - Math.Max(pad, width * 0.65f) &&
                       point.X <= centerX + Math.Max(pad, width * 0.65f);
            }

            return point.X >= min.X - pad && point.X <= max.X + pad &&
                   point.Y >= min.Y - pad && point.Y <= max.Y + pad;
        }

        private static int WorldToCellX(TerrainCache cache, float worldX)
        {
            return (int)Math.Floor((worldX - cache.OriginX) / cache.CellStep);
        }

        private static int WorldToCellY(TerrainCache cache, float worldY)
        {
            return (int)Math.Floor((worldY - cache.OriginY) / cache.CellStep);
        }
    }
}
