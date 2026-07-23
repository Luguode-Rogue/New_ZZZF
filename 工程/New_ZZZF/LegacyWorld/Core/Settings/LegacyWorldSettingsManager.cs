using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using New_ZZZF.LegacyWorld.Core;

namespace New_ZZZF.LegacyWorld.Core.Settings
{
    /// <summary>
    /// LegacyWorld 设置管理器。
    /// 负责 XML 持久化、与 MCM 之间的双向同步，以及手动操作标志管理。
    /// 参照 ProjectileTrajectorySettingsManager 模式。
    /// </summary>
    public static class LegacyWorldSettingsManager
    {
        private static readonly string XmlPath;
        private static LegacyWorldSettingsData _data;
        private static readonly object _lock = new object();

        /// <summary>手动导出待处理标志（由 MCM 按钮触发，Tick 消费）</summary>
        private static bool _manualExportPending;

        /// <summary>手动导入待处理标志（由 MCM 按钮触发，Tick 消费）</summary>
        private static bool _manualApplyPending;

        // ===== 属性桥接（供 AffixLogger 等运行时读取） =====
        /// <summary>日志开关：直接从 Settings 实时读取</summary>
        public static bool LogEnabled => _data?.LogEnabled ?? true;

        static LegacyWorldSettingsManager()
        {
            try
            {
                string dllPath = Assembly.GetExecutingAssembly().Location;
                string dllDir = Path.GetDirectoryName(dllPath);
                string moduleDir = Path.GetDirectoryName(Path.GetDirectoryName(dllDir));
                XmlPath = Path.Combine(moduleDir, "Settings", "LegacyWorldSettings.xml");
                AffixLogger.Log($"LegacyWorldSettingsManager: XML path = {XmlPath}");
            }
            catch (Exception ex)
            {
                AffixLogger.Log($"LegacyWorldSettingsManager: 初始化路径失败: {ex.Message}");
            }
        }

        /// <summary>返回设置数据，惰性加载</summary>
        public static LegacyWorldSettingsData Settings
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        // ===== 持久化 =====

        /// <summary>从 XML 加载设置；若文件不存在则创建默认值</summary>
        public static void Load()
        {
            if (XmlPath == null)
            {
                _data = new LegacyWorldSettingsData();
                return;
            }

            lock (_lock)
            {
                string dir = Path.GetDirectoryName(XmlPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(XmlPath))
                {
                    try
                    {
                        var serializer = new XmlSerializer(typeof(LegacyWorldSettingsData));
                        using var stream = new FileStream(XmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        _data = (LegacyWorldSettingsData)(serializer.Deserialize(stream) ?? new LegacyWorldSettingsData());
                        AffixLogger.Log("LegacyWorldSettingsManager: 从 XML 加载设置成功");
                    }
                    catch (Exception ex)
                    {
                        AffixLogger.Log($"LegacyWorldSettingsManager: 加载 XML 失败: {ex.Message}，使用默认值");
                        _data = new LegacyWorldSettingsData();
                    }
                }
                else
                {
                    _data = new LegacyWorldSettingsData();
                    SaveInternal();
                    AffixLogger.Log("LegacyWorldSettingsManager: 已创建默认设置 XML");
                }
            }
        }

        private static void SaveInternal()
        {
            if (XmlPath == null || _data == null) return;
            try
            {
                var serializer = new XmlSerializer(typeof(LegacyWorldSettingsData));
                using var stream = new FileStream(XmlPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                serializer.Serialize(stream, _data);
            }
            catch (Exception ex)
            {
                AffixLogger.Log($"LegacyWorldSettingsManager: 保存 XML 失败: {ex.Message}");
            }
        }

        /// <summary>显式保存当前设置到 XML</summary>
        public static void Save()
        {
            lock (_lock) { SaveInternal(); }
        }

        // ===== MCM 同步 =====

        /// <summary>
        /// MCM 属性变更时调用，将 MCM 类的值同步回数据层并持久化。
        /// 参照 ProjectileTrajectorySettings.OnPropertyChanged 模式。
        /// </summary>
        public static void SyncFromMCM(
            bool enabled, bool autoExportOnSave, bool logEnabled,
            bool restoreKingdoms, bool restoreClans, bool restoreSettlements,
            bool restoreClanEconomy, bool createMissingClans)
        {
            lock (_lock)
            {
                if (_data == null) _data = new LegacyWorldSettingsData();

                _data.Enabled = enabled;
                _data.AutoExportOnSave = autoExportOnSave;
                _data.LogEnabled = logEnabled;
                _data.RestoreKingdoms = restoreKingdoms;
                _data.RestoreClans = restoreClans;
                _data.RestoreSettlements = restoreSettlements;
                _data.RestoreClanEconomy = restoreClanEconomy;
                _data.CreateMissingClans = createMissingClans;

                SaveInternal();
            }
        }

        // ===== 手动操作标志（供 MCM 按钮使用） =====

        public static void RequestManualExport()
        {
            _manualExportPending = true;
            AffixLogger.Log("LegacyWorldSettingsManager: 收到手动导出请求");
        }

        public static void RequestManualApply()
        {
            _manualApplyPending = true;
            AffixLogger.Log("LegacyWorldSettingsManager: 收到手动应用请求");
        }

        /// <summary>尝试消费手动导出标志（由 LegacyBehavior.OnTick 调用）</summary>
        public static bool TryConsumeManualExport()
        {
            if (_manualExportPending)
            {
                _manualExportPending = false;
                return true;
            }
            return false;
        }

        /// <summary>尝试消费手动应用标志（由 LegacyBehavior.OnTick 调用）</summary>
        public static bool TryConsumeManualApply()
        {
            if (_manualApplyPending)
            {
                _manualApplyPending = false;
                return true;
            }
            return false;
        }
    }
}
