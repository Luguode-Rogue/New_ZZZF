using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// Tactical terrain classification and derived movement/readability metrics.
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
    /// One low-resolution tactical terrain cell.
    /// The derived values intentionally describe gameplay-relevant geometry instead of an opaque risk score.
    /// </summary>
    public sealed class TerrainCell
    {
        public float Height;
        public Vec3 Normal;
        public float Slope;                 // 0..1, normalized surface slope.
        public short[] MaterialLayers;      // Physics material layer indices.
        public TerrainKind Kind;

        // Derived tactical metrics.
        public float MovementCost;           // 0..1, higher means harder/slower to cross.
        public float RelativeHeight;         // -1..1, local elevation against nearby terrain.
        public float HighGround;             // 0..1, local elevation advantage.
        public float HeightBreak;            // 0..1, local height discontinuity.

        // Compatibility alias for older callers. This now means movement difficulty, not combat danger.
        public float Risk;

        public bool IsForest;
        public bool IsCliff;
        public bool IsWater;
        public int DensityAgentCount;        // Legacy heatmap data; HTMLUI no longer renders this layer.
    }
}
