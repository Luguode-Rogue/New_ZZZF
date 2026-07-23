using System;
using System.Xml.Serialization;

namespace New_ZZZF.LegacyWorld.Core.Settings
{
    /// <summary>
    /// LegacyWorld 系统的设置数据层（纯 POCO），由 XML 持久化。
    /// 替代原 LegacyWorldConfig（JSON）+ LegacySettings，统一管理。
    /// </summary>
    [Serializable]
    [XmlRoot("LegacyWorldSettings")]
    public class LegacyWorldSettingsData
    {
        // ===== 运行时控制 =====
        /// <summary>主开关：关闭后完全禁用导入/导出</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>存档时自动导出开关</summary>
        public bool AutoExportOnSave { get; set; } = true;

        /// <summary>日志输出开关</summary>
        public bool LogEnabled { get; set; } = true;

        // ===== 导入数据类别（原 LegacySettings） =====
        /// <summary>是否恢复王国结构（统治者等）</summary>
        public bool RestoreKingdoms { get; set; } = true;

        /// <summary>是否恢复家族数据（所属王国、等级等）</summary>
        public bool RestoreClans { get; set; } = true;

        /// <summary>是否恢复领地所有权（城镇/城堡/村庄的归属）</summary>
        public bool RestoreSettlements { get; set; } = true;

        /// <summary>是否恢复家族经济数据（金币、声望、影响力）</summary>
        public bool RestoreClanEconomy { get; set; } = true;

        /// <summary>是否自动创建在新世界中不存在的 Clan（预留功能）</summary>
        public bool CreateMissingClans { get; set; } = false;
    }
}
