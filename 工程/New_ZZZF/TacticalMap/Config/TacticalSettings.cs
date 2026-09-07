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

        /// <summary>
        /// 【旧路线·已停用】拍照式地形底图 v6：每场新建 SceneView/RT/Camera，结束时销毁。
        /// 渲染对象生命周期在多次进出战斗时反复污染引擎状态（卡死/卡顿多次实测），
        /// 2026-09-06 起默认关闭，改用 TerrainPhotoV2。代码保留供回退验证。
        /// </summary>
        public bool PhotoMap = false;

        /// <summary>
        /// 【新路线】拍照式地形底图 v2：渲染对象全进程单例——SceneView/RT/Camera 只创建一次，
        /// 永不销毁（无 Cleanup/Shutdown/销毁时机问题）；每场仅 SetScene 切场景 + 拍摄 + 停用。
        /// 回读仍走 SaveToFile 落盘（引擎唯一安全路线）。同场景进程级照片缓存。
        /// </summary>
        public bool TerrainPhotoV2 = true;

        /// <summary>
        /// GDI+ Format32bppArgb 的 LockBits 内存布局为 BGRA；拍照 PNG 回读必须交换 R/B，
        /// 才能恢复 TerrainPhotographer 内部统一使用的 RGBA 布局。
        /// </summary>
        public bool PhotoMapSwapRedBlue = true;

        // Bannerlord's normal.z-derived slope value is not an angle; 0.45 already represents
        // a genuinely steep surface. Lowering the previous 0.55 avoids missing carved ledges.
        public float CliffSlopeThreshold = 0.45f;
        public float CliffHeightJump = 1.6f;
        public float WaterHeightFraction = 0.05f;
        public float HighGroundReferenceHeight = 3.0f;

        public short[] ForestMaterialIndices = new short[] { 1, 2, 6 };
    }
}
