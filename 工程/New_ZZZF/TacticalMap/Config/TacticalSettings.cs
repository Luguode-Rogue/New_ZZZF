using TaleWorlds.InputSystem;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// 所有可调参数集中于此，方便以后抽成独立 mod 时由 MCM 接管。
    /// </summary>
    public sealed class TacticalSettings
    {
        private static TacticalSettings _instance;
        public static TacticalSettings Instance => _instance ?? (_instance = new TacticalSettings());

        public bool EnableMinimap = true;
        public bool EnableRiskOverlay = true;
        public bool EnableDensityHeatmap = true;
        public bool EnableUnitMarkers = true;
        public bool EnableAgentMarkers = true;
        public bool EnableCameraLink = true;

        public InputKey ToggleKey = InputKey.N;
        public InputKey CameraFollowKey = InputKey.C;

        public int MapSize = 320;
        public int MapMargin = 16;
        public int BakeResolution = 256;
        public float UpdateInterval = 0.2f;
        public float CliffSlopeThreshold = 0.55f;
        public float CliffHeightJump = 2.5f;
        public float WaterHeightFraction = 0.05f;
        public short[] ForestMaterialIndices = new short[] { 1, 2, 6 };

        // HTMLUI/旧版 TacticalMap 仍使用的兼容参数。
        public float ToggleLongPressThreshold = 0.75f;
        public float AgentDetailDistance = 40f;
    }
}