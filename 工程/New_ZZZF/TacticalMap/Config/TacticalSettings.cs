using TaleWorlds.InputSystem;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// TacticalMap 可调参数。HTMLUI 负责表现，核心逻辑仅读取这里的行为/性能参数。
    /// </summary>
    public sealed class TacticalSettings
    {
        private static TacticalSettings _instance;
        public static TacticalSettings Instance => _instance ?? (_instance = new TacticalSettings());

        public bool EnableMinimap = true;

        public bool EnableRiskOverlay = true;
        public bool EnableUnitMarkers = true;
        public bool EnableAgentMarkers = true;
        public bool EnableCameraLink = true;

        // 仅为旧版 Gauntlet MinimapWidget 提供编译兼容；HTMLUI 不使用 Density Heatmap。
        public bool EnableDensityHeatmap = false;

        // N：短按切换地图操作状态；长按切换“小地图 -> 全屏 -> 隐藏”。
        public InputKey ToggleKey = InputKey.N;
        public float ToggleLongPressThreshold = 0.45f;

        public int MapSize = 320;
        public int MapMargin = 16;

        // 玩家附近显示单个 Agent；远处仅显示编队。
        public float AgentDetailDistance = 90f;

        public int BakeResolution = 256;
        public float UpdateInterval = 0.2f;

        public float CliffSlopeThreshold = 0.55f;
        public float CliffHeightJump = 2.5f;
        public float WaterHeightFraction = 0.05f;

        public short[] ForestMaterialIndices = new short[] { 1, 2, 6 };
    }
}
