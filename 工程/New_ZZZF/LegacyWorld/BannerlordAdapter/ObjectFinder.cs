using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using New_ZZZF.LegacyWorld.Adapter;

namespace New_ZZZF.LegacyWorld.BannerlordAdapter
{
    /// <summary>
    /// Bannerlord 游戏对象查找辅助类。
    /// 通过 CampaignSystem 的静态 API 查找 Kingdom / Clan / Settlement。
    /// </summary>
    public static class ObjectFinder
    {
        public static Kingdom FindKingdomById(string id)
        {
            return Campaign.Current?.Kingdoms?.Find(k => k.StringId == id);
        }

        public static Clan FindClanById(string id)
        {
            return Campaign.Current?.Clans?.Find(c => c.StringId == id);
        }

        public static Settlement FindSettlementById(string id)
        {
            return Settlement.Find(id);
        }
    }
}
