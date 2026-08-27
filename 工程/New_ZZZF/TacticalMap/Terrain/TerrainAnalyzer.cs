using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// Builds gameplay-relevant terrain semantics from height, normal, material and local relief.
    /// No Scene/Game API is accessed here, so the classification pass can run in parallel.
    /// </summary>
    public static class TerrainAnalyzer
    {
        public static void ClassifyAll(TerrainCache cache, float[,] heights, TacticalMap.Config.TacticalSettings s)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            if (s == null) throw new ArgumentNullException(nameof(s));

            int w = cache.Width;
            int h = cache.Height;
            if (w <= 0 || h <= 0) return;

            float range = Math.Max(0.001f, cache.MaxH - cache.MinH);
            var forestMaterials = new HashSet<short>(s.ForestMaterialIndices ?? new short[0]);
            var watch = Stopwatch.StartNew();

            Parallel.For(0, w, x =>
            {
                for (int y = 0; y < h; y++)
                {
                    TerrainCell cell = cache.Cells[x, y];
                    float slope = 1f - cell.Normal.z;
                    if (slope < 0f) slope = 0f;
                    else if (slope > 1f) slope = 1f;
                    cell.Slope = slope;

                    float centerHeight = heights[x, y];
                    float neighborSum = 0f;
                    int neighborCount = 0;
                    float jump = 0f;
                    int minX = Math.Max(0, x - 1);
                    int maxX = Math.Min(w - 1, x + 1);
                    int minY = Math.Max(0, y - 1);
                    int maxY = Math.Min(h - 1, y + 1);

                    for (int nx = minX; nx <= maxX; nx++)
                    {
                        for (int ny = minY; ny <= maxY; ny++)
                        {
                            if (nx == x && ny == y) continue;
                            float nh = heights[nx, ny];
                            neighborSum += nh;
                            neighborCount++;
                            float delta = Math.Abs(centerHeight - nh);
                            if (delta > jump) jump = delta;
                        }
                    }

                    float neighborhoodAverage = neighborCount > 0 ? neighborSum / neighborCount : centerHeight;
                    float relativeDelta = centerHeight - neighborhoodAverage;
                    cell.RelativeHeight = Clamp(relativeDelta / Math.Max(0.5f, s.HighGroundReferenceHeight), -1f, 1f);
                    cell.HighGround = Math.Max(0f, cell.RelativeHeight);
                    cell.IsHighGround = cell.HighGround >= 0.25f;
                    cell.HeightBreak = Clamp(jump / Math.Max(0.5f, s.CliffHeightJump), 0f, 1f);

                    float heightFrac = (cell.Height - cache.MinH) / range;
                    cell.IsWater = heightFrac <= s.WaterHeightFraction && slope < 0.25f;
                    cell.IsCliff = !cell.IsWater &&
                                   (slope >= s.CliffSlopeThreshold || jump >= s.CliffHeightJump);

                    bool vegetationMaterial = false;
                    short[] layers = cell.MaterialLayers;
                    if (layers != null && layers.Length > 0 && forestMaterials.Count > 0)
                    {
                        for (int i = 0; i < layers.Length; i++)
                        {
                            if (forestMaterials.Contains(layers[i]))
                            {
                                vegetationMaterial = true;
                                break;
                            }
                        }
                    }
                    cell.IsForest = !cell.IsWater && !cell.IsCliff && vegetationMaterial && slope < 0.28f;

                    if (cell.IsWater)
                        cell.Kind = TerrainKind.Water;
                    else if (cell.IsCliff)
                        cell.Kind = TerrainKind.Cliff;
                    else if (cell.IsForest)
                        cell.Kind = TerrainKind.Forest;
                    else
                        cell.Kind = TerrainKind.Plain;

                    float slopeCost = SmoothStep(0.18f, Math.Max(0.22f, s.CliffSlopeThreshold), slope) * 0.58f;
                    float breakCost = cell.HeightBreak * 0.32f;
                    float materialCost = cell.IsForest ? 0.14f : 0f;
                    float blockerCost = cell.IsCliff ? 1f : (cell.IsWater ? 0.92f : 0f);
                    cell.MovementCost = Clamp(Math.Max(blockerCost, slopeCost + breakCost + materialCost), 0f, 1f);

                    // Keep the old field meaningful for consumers that still read RiskRGBA.
                    cell.Risk = cell.MovementCost;
                }
            });

            watch.Stop();
            New_ZZZF.TacticalMap.Diagnostics.TacticalMapLog.Info(
                "TerrainAnalyzer.ClassifyAll completed in " + watch.ElapsedMilliseconds + " ms. Cells=" + (w * h));
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            if (value <= edge0) return 0f;
            if (value >= edge1) return 1f;
            float t = (value - edge0) / Math.Max(0.0001f, edge1 - edge0);
            return t * t * (3f - 2f * t);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
