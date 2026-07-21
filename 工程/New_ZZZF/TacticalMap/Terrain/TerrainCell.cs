using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// 战术语义地形类别（由高度/法线/材质/邻域推断，非引擎原生语义）。
    /// </summary>
    public enum TerrainKind
    {
        Plain,
        Forest,
        Cliff,
        Water,
        Mud,
        Snow,
        Road,
        Bridge,
        Wall
    }

    /// <summary>
    /// 单个低分辨率战术栅格单元。
    /// </summary>
    public sealed class TerrainCell
    {
        public float Height;
        public Vec3 Normal;
        public float Slope;                 // 0..1，越大越陡
        public short[] MaterialLayers;      // 物理材质层索引
        public TerrainKind Kind;
        public float Risk;                  // 0..1，红/危险
        public bool IsForest;
        public bool IsCliff;
        public bool IsWater;
        public int DensityAgentCount;       // 由 AgentTracker 填充（单位密度）
    }
}
