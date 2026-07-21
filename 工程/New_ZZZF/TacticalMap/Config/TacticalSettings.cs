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

        // ---- 总开关 ----
        public bool EnableMinimap = true;

        // ---- 子功能 ----
        public bool EnableRiskOverlay = true;
        public bool EnableDensityHeatmap = true;
        public bool EnableUnitMarkers = true;
        public bool EnableAgentMarkers = true;   // 单位层纹理：每个 agent 一个点（我方蓝/敌方红）
        public bool EnableCameraLink = true;

        // ---- 热键 ----
        public InputKey ToggleKey = InputKey.N;          // 开关小地图
        public InputKey CameraFollowKey = InputKey.C;    // 切换"镜头联动"模式（开启后点小地图飞镜头）

        // ---- 布局（屏幕像素）----
        public int MapSize = 320;
        public int MapMargin = 16;

        // ---- 烘焙分辨率（地形栅格每边采样数）----
        public int BakeResolution = 256;

        // ---- 动态纹理刷新间隔（秒）。0.15 => ~6.6Hz，足够流畅且极低开销 ----
        public float UpdateInterval = 0.15f;

        // ---- 地形分析阈值（基于高度/法线/材质推断，详见 TerrainAnalyzer）----
        public float CliffSlopeThreshold = 0.55f;   // 1 - normal.z
        public float CliffHeightJump = 2.5f;        // 相邻栅格高度突变（米）
        public float WaterHeightFraction = 0.05f;   // 接近最低高度的区域视为水域（启发式）

        // 植被/林地材质层索引（场景相关，需按实际场景微调；留空则林地主要依赖密度推断）
        // 注意：Bannerlord 地形物理材质索引无统一语义，这里给出最可能的候选值。
        public short[] ForestMaterialIndices = new short[] { 1, 2, 6 };
    }
}
