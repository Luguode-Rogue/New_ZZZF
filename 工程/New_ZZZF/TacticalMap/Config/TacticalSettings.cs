using TaleWorlds.InputSystem;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// TacticalMap behavior and performance settings.
    /// </summary>
    public sealed class TacticalSettings
    {
        private static TacticalSettings _instance;
        public static TacticalSettings Instance => _instance ?? (_instance = new TacticalSettings());

        public bool EnableMinimap = true;

        // Movement-cost heat is intentionally disabled by default. It paints smooth slope changes
        // as red bands and obscures the actual terrain/obstacle map.
        public bool EnableRiskOverlay = false;
        public bool EnableUnitMarkers = true;
        public bool EnableAgentMarkers = true;
        public bool EnableCameraLink = true;

        // Legacy switch retained for compatibility; tactical terrain overlay replaces the old density heatmap.
        public bool EnableDensityHeatmap = false;

        public InputKey ToggleKey = InputKey.N;
        public float ToggleLongPressThreshold = 0.45f;

        public int MapSize = 320;
        public int MapMargin = 16;

        // Near the player: individual agents. Far away: formation-level information.
        public float AgentDetailDistance = 90f;

        public int BakeResolution = 256;
        public float UpdateInterval = 0.2f;

        // Bannerlord's normal.z-derived slope value is not an angle; 0.45 already represents
        // a genuinely steep surface. Lowering the previous 0.55 avoids missing carved ledges.
        public float CliffSlopeThreshold = 0.45f;
        public float CliffHeightJump = 1.6f;
        public float WaterHeightFraction = 0.05f;
        public float HighGroundReferenceHeight = 3.0f;

        public short[] ForestMaterialIndices = new short[] { 1, 2, 6 };
    }
}
