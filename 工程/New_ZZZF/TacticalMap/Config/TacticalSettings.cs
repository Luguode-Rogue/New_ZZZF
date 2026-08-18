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

        // ---- 总开关 ----
        public bool EnableMinimap = true;

        // ---- 子功能 ----
        public bool EnableRiskOverlay = true;
        public bool EnableUnitMarkers = true;
        public bool EnableAgentMarkers = true;
        public bool EnableCameraLink = true;

        // ---- 热键 ----
        // N：短按切换地图操作状态；长按切换“小地图 -> 全屏 -> 隐藏”。
        public InputKey ToggleKey = InputKey.N;
        public float ToggleLongPressThreshold = 0.45f;

        // ---- 布局（屏幕像素）----
        public int MapSize = 320;
        public int MapMargin = 16;

        // ---- 动态 Agent 显示 ----
        // 玩家附近显示单个 Agent；远处仅显示编队。该值只影响表现，不改变追踪/订单逻辑。
        public float AgentDetailDistance = 90f;

        // ---- 烘焙分辨率（地形栅格每边采样数）----
        public int BakeResolution = 256;

        // ---- 动态数据刷新间隔（秒）----
        public float UpdateInterval = 0.2f;

        // ---- 地形分析阈值 ----
        public float CliffSlopeThreshold = 0.55f;
        public float CliffHeightJump = 2.5f;
        public float WaterHeightFraction = 0.05f;

        // 植被/林地材质层索引。
        public short[] ForestMaterialIndices = new short[] { 1, 2, 6 };
    }
}
