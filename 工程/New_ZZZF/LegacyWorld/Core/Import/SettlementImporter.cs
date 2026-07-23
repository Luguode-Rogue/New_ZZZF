using System;
using New_ZZZF.LegacyWorld.Adapter;
using New_ZZZF.LegacyWorld.Core;
using New_ZZZF.LegacyWorld.Core.Models;

namespace New_ZZZF.LegacyWorld.Core.Import
{
    /// <summary>
    /// 定居点状态导入器。
    /// 恢复城镇/城堡/村庄的所有者家族和繁荣度。
    /// 如果定居点在新世界中不存在或所有者家族不存在，将被跳过。
    /// </summary>
    public class SettlementImporter
    {
        private readonly IGameAdapter _adapter;

        public SettlementImporter(IGameAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        /// <summary>
        /// 执行定居点状态恢复。
        /// </summary>
        /// <param name="state">来自 Legacy 的定居点状态数据</param>
        /// <returns>成功恢复的定居点数量</returns>
        public int Apply(SettlementState state)
        {
            AffixLogger.Info("SETTLEIMPORT", $"Apply: id={state?.Id}, name={state?.Name}, ownerClanId={state?.OwnerClanId}");

            var settlement = _adapter.FindSettlement(state.Id);
            if (settlement == null)
            {
                AffixLogger.Info("SETTLEIMPORT", $"定居点 {state.Id}({state.Name}) 在新世界不存在，跳过");
                return 0;
            }

            AffixLogger.Info("SETTLEIMPORT", $"找到定居点 {settlement.Name}({settlement.Id}), OwnerClanId={settlement.OwnerClanId}");

            // 恢复所有者家族
            if (!string.IsNullOrEmpty(state.OwnerClanId))
            {
                var ownerClan = _adapter.FindClan(state.OwnerClanId);
                if (ownerClan != null)
                {
                    AffixLogger.Info("SETTLEIMPORT", $"找到所有者家族 {ownerClan.Name}({ownerClan.Id})，执行所有权变更");
                    _adapter.ChangeSettlementOwner(settlement, ownerClan);
                }
                else
                {
                    AffixLogger.Info("SETTLEIMPORT", $"所有者家族 {state.OwnerClanId} 在新世界不存在，跳过所有权变更");
                }
            }
            else
            {
                AffixLogger.Info("SETTLEIMPORT", $"state.OwnerClanId 为空，跳过所有权变更");
            }

            // 恢复繁荣度
            AffixLogger.Info("SETTLEIMPORT", $"设置繁荣度: {state.Prosperity}");
            _adapter.SetSettlementProsperity(settlement, state.Prosperity);

            return 1;
        }
    }
}
