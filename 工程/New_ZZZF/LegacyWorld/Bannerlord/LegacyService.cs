using New_ZZZF.LegacyWorld.Adapter;
using New_ZZZF.LegacyWorld.BannerlordAdapter;
using New_ZZZF.LegacyWorld.Core;
using New_ZZZF.LegacyWorld.Core.Export;
using New_ZZZF.LegacyWorld.Core.Import;
using New_ZZZF.LegacyWorld.Core.Serialization;
using New_ZZZF.LegacyWorld.Core.Settings;
using New_ZZZF.LegacyWorld.Core.Storage;

namespace New_ZZZF.LegacyWorld.Bannerlord
{
    /// <summary>
    /// Legacy 系统的静态服务入口。
    /// 提供 Export / Import 的简洁调用接口，管理适配器与配置的单例。
    /// </summary>
    public static class LegacyService
    {
        private static IGameAdapter _adapter;
        private static LegacySettings _settings;

        /// <summary>
        /// 初始化服务（首次访问时自动调用）。
        /// </summary>
        public static void Initialize()
        {
            if (_adapter != null)
                return;

            _adapter = new BannerlordGameAdapter();

            // 从 MCM 数据层（XML）加载设置，替代硬编码默认值
            RefreshSettings();
        }

        /// <summary>
        /// 从 LegacyWorldSettingsManager 重新加载设置。
        /// 当 MCM 面板中的导入类别开关变更时，应调用此方法确保生效。
        /// </summary>
        public static void RefreshSettings()
        {
            var mcmData = LegacyWorldSettingsManager.Settings;
            _settings = new LegacySettings
            {
                CreateMissingClans = mcmData.CreateMissingClans,
                RestoreClanEconomy = mcmData.RestoreClanEconomy,
                RestoreClans = mcmData.RestoreClans,
                RestoreKingdoms = mcmData.RestoreKingdoms,
                RestoreSettlements = mcmData.RestoreSettlements,
            };
        }

        /// <summary>
        /// 导出当前世界状态到 Legacy.json。
        /// </summary>
        public static void Export()
        {
            Initialize();

            var exporter = new LegacyExporter(_adapter);
            exporter.Export();
        }

        /// <summary>
        /// 从 Legacy.json 导入世界状态（含同世界检测）。
        /// </summary>
        /// <param name="currentWorldId">当前世界的唯一标识，用于检测是否同世界覆盖</param>
        /// <returns>如果成功导入返回 true，否则返回 false</returns>
        public static bool Import(string currentWorldId)
        {
            return ImportCore(currentWorldId, false);
        }

        /// <summary>
        /// 手动强制从 Legacy.json 导入世界状态（忽略同世界检测）。
        /// 仅用于调试/测试。
        /// </summary>
        /// <param name="currentWorldId">当前世界的唯一标识</param>
        /// <returns>如果成功导入返回 true，否则返回 false</returns>
        public static bool ForceImport(string currentWorldId)
        {
            return ImportCore(currentWorldId, true);
        }

        /// <summary>
        /// 导入核心逻辑。
        /// </summary>
        private static bool ImportCore(string currentWorldId, bool force)
        {
            var mode = force ? "FORCE" : "NORMAL";
            AffixLogger.Info("SERVICE", $"Import({mode}) 开始");

            Initialize();

            // 每次导入前刷新设置，确保 MCM 面板的变更生效
            RefreshSettings();

            if (!LegacyStorage.Exists())
            {
                AffixLogger.Info("SERVICE", "Legacy.json 不存在，跳过导入");
                return false;
            }

            var data = LegacySerializer.Load(LegacyStorage.LegacyFile);
            if (data == null)
            {
                AffixLogger.Info("SERVICE", "LegacySerializer.Load 返回 null，跳过");
                return false;
            }

            AffixLogger.Info("SERVICE", $"Legacy数据: WorldId={data.WorldId}, Kingdoms={data.Kingdoms?.Count ?? 0}, Clans={data.Clans?.Count ?? 0}, Settlements={data.Settlements?.Count ?? 0}");

            // 禁止同世界互相覆盖（强制模式跳过此检查）
            if (!force && data.WorldId == currentWorldId)
            {
                AffixLogger.Info("SERVICE", $"同世界禁止覆盖: LegacyWorld={data.WorldId} == CurrentWorld={currentWorldId}");
                return false;
            }

            if (force)
                AffixLogger.Info("SERVICE", "强制模式：忽略同世界检测");
            else
                AffixLogger.Info("SERVICE", $"不同世界，允许导入: LegacyWorld={data.WorldId} != CurrentWorld={currentWorldId}");

            var importer = new LegacyImporter(_adapter, _settings);
            var result = importer.Apply(data);

            AffixLogger.Info("SERVICE", $"Import({mode}) 完成: Kingdoms={result.KingdomsRestored}, Clans={result.ClansRestored}, Settlements={result.SettlementsRestored}");

            return result.KingdomsRestored > 0 || result.ClansRestored > 0 || result.SettlementsRestored > 0;
        }
    }
}
