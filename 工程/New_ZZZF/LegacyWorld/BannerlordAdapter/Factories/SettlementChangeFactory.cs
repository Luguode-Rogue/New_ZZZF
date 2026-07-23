using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using New_ZZZF.LegacyWorld.Core;

namespace New_ZZZF.LegacyWorld.BannerlordAdapter.Factories
{
    /// <summary>
    /// 定居点所有权变更工厂。
    /// 通过游戏官方 API（Town.OwnerClan setter）变更所有权，确保完整的
    /// OnFortificationAdded/Removed 通知链和地图视觉更新。
    /// </summary>
    public static class SettlementChangeFactory
    {
        /// <summary>
        /// 变更定居点的所有者家族。
        /// 使用 Town.OwnerClan 公共 setter，保证：
        /// - 旧 Clan 的 OnFortificationRemoved() 被调用
        /// - 新 Clan 的 OnFortificationAdded() 被调用
        /// - 绑定村庄的地图视觉被刷新
        /// </summary>
        public static void ChangeOwner(Settlement settlement, Clan newOwner)
        {
            AffixLogger.Info("FACTORY", $"ChangeOwner 调用: settlement={settlement?.Name}({settlement?.StringId}), newOwner={newOwner?.Name}({newOwner?.StringId})");

            if (settlement == null || newOwner == null)
            {
                AffixLogger.Info("FACTORY", $"ChangeOwner 跳过: settlement==null={settlement == null}, newOwner==null={newOwner == null}");
                return;
            }

            try
            {
                // === 情况 1: Town/Castle 类型的定居点 → 直接使用公开 setter ===
                if (settlement.Town != null)
                {
                    var beforeClan = settlement.Town.OwnerClan;
                    AffixLogger.Info("FACTORY", $"变更前(Town.OwnerClan): {settlement.Name} 所有者={beforeClan?.Name}({beforeClan?.StringId})");

                    settlement.Town.OwnerClan = newOwner;

                    var afterClan = settlement.Town.OwnerClan;
                    AffixLogger.Info("FACTORY", $"变更完成: {settlement.Name} 所有者={afterClan?.Name}({afterClan?.StringId}), 期望={newOwner.Name}({newOwner.StringId})");

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[LegacyWorld] ✓ {settlement.Name} 归属已设为 {newOwner.Name}",
                        Colors.Green));
                    return;
                }

                // === 情况 2: Village 类型的定居点 → 变更其绑定定居点的 Town ===
                if (settlement.Village != null)
                {
                    Settlement boundSettlement = settlement.Village.Bound;
                    if (boundSettlement?.Town != null)
                    {
                        AffixLogger.Info("FACTORY", $"{settlement.Name} 是村庄，通过其绑定定居点 {boundSettlement.Name} 变更所有权");

                        var beforeClan = boundSettlement.Town.OwnerClan;
                        AffixLogger.Info("FACTORY", $"变更前(绑定Town): {boundSettlement.Name} 所有者={beforeClan?.Name}({beforeClan?.StringId})");

                        boundSettlement.Town.OwnerClan = newOwner;

                        var afterClan = settlement.OwnerClan;
                        AffixLogger.Info("FACTORY", $"变更完成(村庄跟随): {settlement.Name} 所有者={afterClan?.Name}({afterClan?.StringId})");

                        InformationManager.DisplayMessage(new InformationMessage(
                            $"[LegacyWorld] ✓ {settlement.Name} 归属已设为 {newOwner.Name}（通过绑定定居点）",
                            Colors.Green));
                        return;
                    }
                    else
                    {
                        AffixLogger.Error("FACTORY", $"村庄 {settlement.Name} 的绑定定居点无效或没有 Town 组件");
                    }
                }

                // === 情况 3: 藏身处或其他未知类型 ===
                AffixLogger.Info("FACTORY", $"无法处理的定居点类型: {settlement.Name}, " +
                    $"IsTown={settlement.IsTown}, IsCastle={settlement.IsCastle}, " +
                    $"IsVillage={settlement.IsVillage}, IsHideout={settlement.IsHideout}");

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[LegacyWorld] ⚠ {settlement.Name} 无法变更（未知类型）",
                    Colors.Yellow));
            }
            catch (Exception ex)
            {
                AffixLogger.Error("FACTORY", $"变更 {settlement.Name} 所有权失败", ex);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[LegacyWorld] ✗ {settlement.Name} 所有权变更失败: {ex.Message}",
                    Colors.Red));
            }
        }

        /// <summary>
        /// 设置定居点繁荣度。
        /// </summary>
        public static void SetProsperity(Settlement settlement, int prosperity)
        {
            if (settlement?.Town == null)
                return;

            settlement.Town.Prosperity = prosperity;
        }

        /// <summary>
        /// 设置家族金币。Clan.Gold 为只读，通过领主的金币变更实现。
        /// </summary>
        public static void SetClanGold(Clan clan, int gold)
        {
            if (clan?.Leader == null)
                return;

            int diff = gold - clan.Leader.Gold;
            clan.Leader.ChangeHeroGold(diff);
        }

        /// <summary>
        /// 设置家族声望。
        /// </summary>
        public static void SetClanRenown(Clan clan, float renown)
        {
            if (clan == null)
                return;

            clan.Renown = renown;
        }

        /// <summary>
        /// 设置家族影响力。
        /// </summary>
        public static void SetClanInfluence(Clan clan, float influence)
        {
            if (clan == null)
                return;

            clan.Influence = influence;
        }

        /// <summary>
        /// 设置王国统治者家族。
        /// </summary>
        public static void SetKingdomRuler(Kingdom kingdom, Clan rulerClan)
        {
            if (kingdom == null || rulerClan == null)
                return;

            kingdom.RulingClan = rulerClan;
        }

        /// <summary>
        /// 设置家族所属王国。
        /// </summary>
        public static void SetClanKingdom(Clan clan, Kingdom kingdom)
        {
            if (clan == null || kingdom == null)
                return;

            clan.Kingdom = kingdom;
        }
    }
}
