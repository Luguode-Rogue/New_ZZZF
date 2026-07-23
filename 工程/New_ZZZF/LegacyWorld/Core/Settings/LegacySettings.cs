using System;
using System.Xml.Serialization;

namespace New_ZZZF.LegacyWorld.Core.Settings
{
    /// <summary>
    /// LegacyWorld 导入数据类别选择。
    /// 由 Import 模块（ClanImporter / KingdomImporter 等）读取。
    /// 数据来源：MCM 数据层（LegacyWorldSettingsData），通过 LegacyService.RefreshSettings() 同步。
    /// </summary>
    [Serializable]
    public class LegacySettings
    {
        public bool CreateMissingClans { get; set; } = false;
        public bool RestoreClanEconomy { get; set; } = true;
        public bool RestoreClans { get; set; } = true;
        public bool RestoreKingdoms { get; set; } = true;
        public bool RestoreSettlements { get; set; } = true;
    }
}
