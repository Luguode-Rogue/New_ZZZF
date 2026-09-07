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
        /// 拍照式底图统一使用 TerrainPhotoV2。
        /// SceneView / RenderTarget / Camera 必须进程级复用，禁止重新引入按战斗创建/销毁的旧路线。
        /// </summary>
        public bool TerrainPhotoV2 = true;

        /// <summary>
        /// TerrainCache 的旧字段引用兼容层。
        /// 这里只是配置别名，不会恢复旧版 TerrainPhotoCapture 渲染器；实际开关仍由 TerrainPhotoV2 控制。
        /// </summary>
        public bool PhotoMap => TerrainPhotoV2;

        /// <summary>
        /// GDI+ Format32bppArgb 的 LockBits 内存布局为 BGRA；拍照 PNG 回读必须交换 R/B，
        /// 才能恢复 TerrainPhotographer 内部统一使用的 RGBA 布局。
        /// </summary>
        public bool PhotoMapSwapRedBlue = true;

        public float CliffSlopeThreshold = 0.45f;
        public float CliffHeightJump = 1.6f;
        public float WaterHeightFraction = 0.05f;
        public float HighGroundReferenceHeight = 3.0f;
        public short[] ForestMaterialIndices = new short[] { 1, 2, 6 };
    }
}