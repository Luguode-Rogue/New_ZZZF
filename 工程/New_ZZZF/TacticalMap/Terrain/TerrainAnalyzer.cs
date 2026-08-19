using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// 战术语义地形推断器。
    /// 不依赖任何“森林/悬崖”高层语义接口，而是用 高度 + 法线 + 材质层 + 邻域突变 推断。
    /// 这是纯数据计算阶段，不访问 Scene / Game API，可安全并行执行。
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
            var forestMaterials = new HashSet<short>(s.ForestMaterialIndices ?? Array.Empty<short>());
            var watch = Stopwatch.StartNew();

            Parallel.For(0, w, x =>
            {
                for (int y = 0; y < h; y++)
                {
                    var cell = cache.Cells[x, y];
                    float slope = 1f - cell.Normal.z;
                    if (slope < 0f) slope = 0f;
                    else if (slope > 1f) slope = 1f;
                    cell.Slope = slope;

                    float centerHeight = heights[x, y];
                    float jump = 0f;
                    if (x > 0) jump = Math.Max(jump, Math.Abs(centerHeight - heights[x - 1, y]));
                    if (x < w - 1) jump = Math.Max(jump, Math.Abs(centerHeight - heights[x + 1, y]));
                    if (y > 0) jump = Math.Max(jump, Math.Abs(centerHeight - heights[x, y - 1]));
                    if (y < h - 1) jump = Math.Max(jump, Math.Abs(centerHeight - heights[x, y + 1]));

                    float heightFrac = (cell.Height - cache.MinH) / range;
                    cell.IsWater = heightFrac <= s.WaterHeightFraction;
                    cell.IsCliff = !cell.IsWater && (slope > s.CliffSlopeThreshold || jump > s.CliffHeightJump);

                    bool vegMat = false;
                    var layers = cell.MaterialLayers;
                    if (layers != null && layers.Length > 0 && forestMaterials.Count > 0)
                    {
                        for (int i = 0; i < layers.Length; i++)
                        {
                            if (forestMaterials.Contains(layers[i]))
                            {
                                vegMat = true;
                                break;
                            }
                        }
                    }
                    cell.IsForest = !cell.IsWater && !cell.IsCliff && slope < 0.12f && vegMat;

                    if (cell.IsCliff)
                    {
                        cell.Kind = TerrainKind.Cliff;
                        cell.Risk = 0.9f;
                    }
                    else if (cell.IsWater)
                    {
                        cell.Kind = TerrainKind.Water;
                        cell.Risk = 0.7f;
                    }
                    else if (cell.IsForest)
                    {
                        cell.Kind = TerrainKind.Forest;
                        cell.Risk = 0.35f;
                    }
                    else
                    {
                        cell.Kind = TerrainKind.Plain;
                        cell.Risk = 0f;
                    }
                }
            });

            watch.Stop();
            New_ZZZF.TacticalMap.Diagnostics.TacticalMapLog.Info(
                "TerrainAnalyzer.ClassifyAll completed in " + watch.ElapsedMilliseconds + " ms. Cells=" + (w * h));
        }
    }
}
