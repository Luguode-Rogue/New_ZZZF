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
        public bool EnableRiskOverlay = false;
        public bool EnableUnitMarkers = true;
        public bool EnableAgentMarkers = true;
        public bool EnableCameraLink = true;
        public bool EnableDensityHeatmap = false;
        public InputKey ToggleKey = InputKey.N;
        public float ToggleLongPressThreshold = 0.45f;
        public int MapSize = 320;
        public int MapMargin = 16;
        public float AgentDetailDistance = 90f;
        public int BakeResolution = 256;
        public float UpdateInterval = 0.2f;

        /// <summary>
        /// 拍照式底图统一使用开放世界瓦片路线。
        /// 禁止重新引入独立 SceneView、RenderTarget、Tableau 或整场单图路线。
        /// </summary>
        public bool TerrainPhotoV2 = true;

        /// <summary>
        /// TerrainCache 的旧字段引用兼容层。
        /// 这里只是配置别名；实际开关仍由 TerrainPhotoV2 控制。
        /// </summary>
        public bool PhotoMap => TerrainPhotoV2;

        /// <summary>
        /// GDI+ Format32bppArgb 的 LockBits 内存布局为 BGRA；拍照 PNG 回读必须交换 R/B，
        /// 才能恢复瓦片底图内部统一使用的 RGBA 布局。
        /// </summary>
        public bool PhotoMapSwapRedBlue = true;

        /// <summary>
        /// 全地图瓦片拍摄（TerrainPhotoTileCapture）：单瓦片目标边长（米，指南北向）。
        /// 越小 → 瓦片越多、细节越高、拍摄越久。默认 140m（典型战斗约 4×7=28 张）。
        /// </summary>
        public float PhotoTileWorldHeight = 140f;

        /// <summary>
        /// 瓦片视野相对格子的外扩比例（0~0.45）。相邻瓦片重叠区按羽化权重取
        /// 离格心更近者，吸收地形高度差造成的接缝错位。
        /// </summary>
        public float PhotoTileOverlap = 0.15f;

        /// <summary>合成底图长边像素上限（行 0 = 南、列 0 = 西，等比世界边界）。3072 ≈ 3.4px/m。</summary>
        public int PhotoBufferMaxDim = 3072;

        /// <summary>拍摄期间隐藏全部 Agent 视觉体，避免部队被烤进底图（结束时恢复）。</summary>
        public bool HideAgentsInPhoto = true;

        public float CliffSlopeThreshold = 0.45f;
        public float CliffHeightJump = 1.6f;
        public float WaterHeightFraction = 0.05f;
        public float HighGroundReferenceHeight = 3.0f;
        public short[] ForestMaterialIndices = new short[] { 1, 2, 6 };
    }
}
