using System;
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// 可独立开关的子功能。所有子功能必须先过总开关 EnableMinimap。
    /// </summary>
    public enum TacticalFeature
    {
        RiskOverlay,     // 地形风险提示层（悬崖/水域/林地）
        CameraLink,      // 小地图点击 -> 镜头联动
        DensityHeatmap,  // 单位密度热力
        UnitMarkers      // 编队/单位标记
    }

    public static class FeatureGate
    {
        public static bool Enabled => TacticalSettings.Instance.EnableMinimap;

        public static bool IsEnabled(TacticalFeature feature)
        {
            if (!Enabled) return false;
            switch (feature)
            {
                case TacticalFeature.RiskOverlay: return TacticalSettings.Instance.EnableRiskOverlay;
                case TacticalFeature.CameraLink: return TacticalSettings.Instance.EnableCameraLink;
                case TacticalFeature.DensityHeatmap: return TacticalSettings.Instance.EnableDensityHeatmap;
                case TacticalFeature.UnitMarkers: return TacticalSettings.Instance.EnableUnitMarkers;
                default: return true;
            }
        }
    }
}
