using System;
using New_ZZZF.LegacyWorld.Adapter;
using New_ZZZF.LegacyWorld.Core;
using New_ZZZF.LegacyWorld.Core.Models;
using New_ZZZF.LegacyWorld.Core.Settings;

namespace New_ZZZF.LegacyWorld.Core.Import
{
    /// <summary>
    /// 家族状态导入器。
    /// 恢复 Clan 的 Kingdom 归属、金币、声望、影响力。
    /// 如果 Clan 在新世界中不存在，根据设置决定是否跳过。
    /// </summary>
    public class ClanImporter
    {
        private readonly IGameAdapter _adapter;
        private readonly LegacySettings _settings;

        public ClanImporter(IGameAdapter adapter, LegacySettings settings)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// 执行家族状态恢复。
        /// </summary>
        /// <param name="state">来自 Legacy 的家族状态数据</param>
        /// <returns>成功恢复的家族数量</returns>
        public int Apply(ClanState state)
        {
            AffixLogger.Info("CLANIMP", $"Apply: id={state?.Id}, name={state?.Name}, kingdomId={state?.KingdomId}");

            var clan = _adapter.FindClan(state.Id);
            if (clan == null)
            {
                AffixLogger.Info("CLANIMP", $"家族 {state.Id}({state.Name}) 在新世界不存在");

                // 家族不存在：根据设置决定是否尝试创建
                if (_settings.CreateMissingClans)
                {
                    AffixLogger.Info("CLANIMP", $"尝试创建家族 {state.Id}");
                    clan = _adapter.CreateClan(state.Id, state.Name);
                    if (clan == null)
                    {
                        AffixLogger.Info("CLANIMP", "创建失败，跳过");
                        return 0;
                    }
                    AffixLogger.Info("CLANIMP", "创建成功");
                }
                else
                {
                    AffixLogger.Info("CLANIMP", "CreateMissingClans=false，跳过");
                    return 0;
                }
            }

            AffixLogger.Info("CLANIMP", $"找到家族 {clan.Name}({clan.Id}), KingdomId={clan.KingdomId}");

            // 恢复 Kingdom 归属
            if (!string.IsNullOrEmpty(state.KingdomId))
            {
                var kingdom = _adapter.FindKingdom(state.KingdomId);
                if (kingdom != null)
                {
                    AffixLogger.Info("CLANIMP", $"设置家族Kingdom: {kingdom.Name}");
                    _adapter.SetClanKingdom(clan, kingdom);
                }
                else
                {
                    AffixLogger.Info("CLANIMP", $"Kingdom {state.KingdomId} 不存在，跳过");
                }
            }

            // 恢复经济数据
            if (_settings.RestoreClanEconomy)
            {
                AffixLogger.Info("CLANIMP", $"恢复经济: Gold={state.Gold}, Renown={state.Renown}, Influence={state.Influence}");
                _adapter.SetClanGold(clan, state.Gold);
                _adapter.SetClanRenown(clan, state.Renown);
                _adapter.SetClanInfluence(clan, state.Influence);
            }

            return 1;
        }
    }
}
