using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// 可独立开关的 TacticalMap 子功能。所有子功能必须先过总开关 EnableMinimap。
    /// </summary>
    public enum TacticalFeature
    {
        RiskOverlay,
        CameraLink,
        DensityHeatmap,
        UnitMarkers
    }

    public static class FeatureGate
    {
        public static bool Enabled => TacticalSettings.Instance.EnableMinimap;

        public static bool IsEnabled(TacticalFeature feature)
        {
            if (!Enabled) return false;
            switch (feature)
            {
                case TacticalFeature.RiskOverlay:
                    return TacticalSettings.Instance.EnableRiskOverlay;
                case TacticalFeature.CameraLink:
                    return TacticalSettings.Instance.EnableCameraLink;
                case TacticalFeature.DensityHeatmap:
                    // HTMLUI 重制版不再提供密度热力图。
                    return false;
                case TacticalFeature.UnitMarkers:
                    return TacticalSettings.Instance.EnableUnitMarkers;
                default:
                    return true;
            }
        }
    }
}
