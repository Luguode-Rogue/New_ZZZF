using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// Converts static scene geometry into a tactical obstacle footprint.
    /// This complements the heightmap: buildings, fences, walls, gates and rocks do not
    /// necessarily change terrain height, but their scene/entity bounds still occupy space.
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

                    // Keep authored/static mesh geometry. Dynamic helper entities and agents are
                    // intentionally left to the formation/agent tracking layer.
                    if (entity.GetFirstMesh() == null) continue;

                    Vec3 bbMin;
                    Vec3 bbMax;
                    try
                    {
                        // Bannerlord exposes these entity bounding-box accessors. Do not use the
                        // non-existent GetPhysicsBoundingBoxMin/Max methods.
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

                    // Skip terrain-scale/global meshes and insignificant fragments.
                    if (width > cache.WorldW * 0.55f || depth > cache.WorldH * 0.55f) continue;
                    if (width < 0.18f && depth < 0.18f) continue;
                    if (height < 0.18f) continue;

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
                            var cell = cache.Cells[x, y];
                            cell.Kind = TerrainKind.Wall;
                            cell.MovementCost = 1f;
                            cell.Risk = 1f;
                            cache.SetPixel(cache.TerrainBaseRGBA, x, y, 38, 36, 32, 255);

                            // Keep thin fences/walls readable at low map resolution.
                            bool edge = x == minX || x == maxX || y == minY || y == maxY;
                            if (edge)
                                cache.SetPixel(cache.TerrainBaseRGBA, x, y, 68, 63, 52, 255);

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
