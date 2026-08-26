using ConfigTacticalSettings = New_ZZZF.TacticalMap.Config.TacticalSettings;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// 兼容旧版 TacticalMapHtmlUi 对 TacticalSettings 的未限定引用。
    /// 实际配置仍由 Config.TacticalSettings 单例持有。
    /// </summary>
    internal static class TacticalSettings
    {
        public static ConfigTacticalSettings Instance => ConfigTacticalSettings.Instance;
    }
}
