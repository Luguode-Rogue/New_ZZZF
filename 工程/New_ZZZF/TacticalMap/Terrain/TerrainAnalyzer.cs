using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// 战术语义地形推断器。
    /// 不依赖任何“森林/悬崖”高层语义接口，而是用 高度 + 法线 + 材质层 + 邻域突变 推断。
    /// 这是基于现有 Scene 接口的工程化推断（见需求讨论：你自己做一个战术判定器）。
    /// </summary>
    public static class TerrainAnalyzer
    {
        public static void ClassifyAll(TerrainCache cache, float[,] heights, TacticalMap.Config.TacticalSettings s)
        {
            int w = cache.Width;
            int h = cache.Height;
            float range = System.Math.Max(0.001f, cache.MaxH - cache.MinH);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    var cell = cache.Cells[x, y];

                    // 坡度：法线 z 分量越接近 1 越平
                    float slope = 1f - cell.Normal.z;
                    if (slope < 0f) slope = 0f; if (slope > 1f) slope = 1f;
                    cell.Slope = slope;

                    // 邻域高度突变
                    float jump = 0f;
                    if (x > 0) jump = System.Math.Max(jump, System.Math.Abs(heights[x, y] - heights[x - 1, y]));
                    if (x < w - 1) jump = System.Math.Max(jump, System.Math.Abs(heights[x, y] - heights[x + 1, y]));
                    if (y > 0) jump = System.Math.Max(jump, System.Math.Abs(heights[x, y] - heights[x, y - 1]));
                    if (y < h - 1) jump = System.Math.Max(jump, System.Math.Abs(heights[x, y] - heights[x, y + 1]));

                    float heightFrac = (cell.Height - cache.MinH) / range;

                    // 水域：接近最低高度（启发式）
                    cell.IsWater = heightFrac <= s.WaterHeightFraction;

                    // 悬崖：陡坡 或 邻域突变
                    cell.IsCliff = (!cell.IsWater) && (slope > s.CliffSlopeThreshold || jump > s.CliffHeightJump);

                    // 林地：平坦、非水、且材质层命中植被索引；或（兜底）明显偏低坡的绿色区域
                    bool vegMat = false;
                    if (cell.MaterialLayers != null)
                    {
                        for (int i = 0; i < cell.MaterialLayers.Length; i++)
                        {
                            for (int j = 0; j < s.ForestMaterialIndices.Length; j++)
                            {
                                if (cell.MaterialLayers[i] == s.ForestMaterialIndices[j]) { vegMat = true; break; }
                            }
                            if (vegMat) break;
                        }
                    }
                    cell.IsForest = (!cell.IsWater) && (!cell.IsCliff) && slope < 0.12f && vegMat;

                    // 类别与风险
                    if (cell.IsCliff) { cell.Kind = TerrainKind.Cliff; cell.Risk = 0.9f; }
                    else if (cell.IsWater) { cell.Kind = TerrainKind.Water; cell.Risk = 0.7f; }
                    else if (cell.IsForest) { cell.Kind = TerrainKind.Forest; cell.Risk = 0.35f; }
                    else { cell.Kind = TerrainKind.Plain; cell.Risk = 0f; }
                }
            }
        }
    }
}
