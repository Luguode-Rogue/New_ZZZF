using System;
using New_ZZZF.LegacyWorld.Adapter;
using New_ZZZF.LegacyWorld.Core;
using New_ZZZF.LegacyWorld.Core.Models;
using New_ZZZF.LegacyWorld.Core.Settings;

namespace New_ZZZF.LegacyWorld.Core.Import
{
    /// <summary>
    /// 世界遗产导入引擎。
    /// 编排 Kingdom → Clan → Settlement 的恢复顺序，
    /// 确保依赖关系（Clan 依赖 Kingdom，Settlement 依赖 Clan）正确满足。
    /// </summary>
    public class LegacyImporter
    {
        private readonly IGameAdapter _adapter;
        private readonly LegacySettings _settings;

        public LegacyImporter(IGameAdapter adapter, LegacySettings settings)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// 应用世界遗产数据到当前 Campaign。
        /// </summary>
        /// <param name="data">从 Legacy.json 加载的遗产数据</param>
        /// <returns>各阶段恢复的实体数量统计</returns>
        public ImportResult Apply(LegacyData data)
        {
            AffixLogger.Info("IMPORTER", "Apply 开始");

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            AffixLogger.Info("IMPORTER", $"Settings: RestoreKingdoms={_settings.RestoreKingdoms}, RestoreClans={_settings.RestoreClans}, RestoreSettlements={_settings.RestoreSettlements}, RestoreClanEconomy={_settings.RestoreClanEconomy}");

            var result = new ImportResult();

            // Phase 1: 恢复王国
            if (_settings.RestoreKingdoms)
            {
                AffixLogger.Info("IMPORTER", $"Phase 1: 恢复王国 ({data.Kingdoms?.Count ?? 0} 个)");
                var importer = new KingdomImporter(_adapter);
                foreach (var kingdom in data.Kingdoms)
                {
                    result.KingdomsRestored += importer.Apply(kingdom);
                }
                AffixLogger.Info("IMPORTER", $"Phase 1 完成: 恢复 {result.KingdomsRestored} 个");
            }

            // Phase 2: 恢复家族
            if (_settings.RestoreClans)
            {
                AffixLogger.Info("IMPORTER", $"Phase 2: 恢复家族 ({data.Clans?.Count ?? 0} 个)");
                var importer = new ClanImporter(_adapter, _settings);
                foreach (var clan in data.Clans)
                {
                    result.ClansRestored += importer.Apply(clan);
                }
                AffixLogger.Info("IMPORTER", $"Phase 2 完成: 恢复 {result.ClansRestored} 个");
            }

            // Phase 3: 恢复定居点
            if (_settings.RestoreSettlements)
            {
                AffixLogger.Info("IMPORTER", $"Phase 3: 恢复定居点 ({data.Settlements?.Count ?? 0} 个)");
                var importer = new SettlementImporter(_adapter);
                foreach (var settlement in data.Settlements)
                {
                    result.SettlementsRestored += importer.Apply(settlement);
                }
                AffixLogger.Info("IMPORTER", $"Phase 3 完成: 恢复 {result.SettlementsRestored} 个");
            }

            AffixLogger.Info("IMPORTER", $"Apply 完成: {result}");

            return result;
        }
    }

    /// <summary>
    /// 导入操作的结果统计。
    /// </summary>
    public class ImportResult
    {
        public int KingdomsRestored { get; set; }
        public int ClansRestored { get; set; }
        public int SettlementsRestored { get; set; }

        public override string ToString()
        {
            return $"Kingdoms: {KingdomsRestored}, Clans: {ClansRestored}, Settlements: {SettlementsRestored}";
        }
    }
}
