using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using New_ZZZF.LegacyWorld.Adapter;
using New_ZZZF.LegacyWorld.BannerlordAdapter.Factories;
using New_ZZZF.LegacyWorld.Core;

namespace New_ZZZF.LegacyWorld.BannerlordAdapter
{
    /// <summary>
    /// Bannerlord 游戏适配器实现（v1.2.x）。
    /// 将 IGameAdapter 接口调用映射到 TaleWorlds.CampaignSystem API。
    /// </summary>
    public class BannerlordGameAdapter : IGameAdapter
    {
        // ========== 世界信息 ==========

        public string GetWorldId()
        {
            string seed = Campaign.Current?.UniqueGameId ?? "0";
            return seed;
        }

        public string GetCurrentGameTime()
        {
            return CampaignTime.Now.ToString();
        }

        public string GetDominantCulture()
        {
            var firstKingdom = Campaign.Current?.Kingdoms?.FirstOrDefault();
            return firstKingdom?.Culture?.StringId ?? "unknown";
        }

        public string GetGameVersion()
        {
            return ApplicationVersion.FromParametersFile().ToString();
        }

        // ========== Kingdom 相关 ==========

        public IEnumerable<IKingdomInfo> GetAllKingdoms()
        {
            if (Campaign.Current?.Kingdoms == null)
                return Enumerable.Empty<IKingdomInfo>();

            return Campaign.Current.Kingdoms
                .Where(k => !k.IsEliminated)
                .Select(k => (IKingdomInfo)new KingdomInfoWrapper(k))
                .ToList();
        }

        public IKingdomInfo FindKingdom(string id)
        {
            var kingdom = ObjectFinder.FindKingdomById(id);
            return kingdom != null ? new KingdomInfoWrapper(kingdom) : null;
        }

        public void SetKingdomRuler(IKingdomInfo kingdom, IClanInfo rulerClan)
        {
            if (kingdom is KingdomInfoWrapper kw && rulerClan is ClanInfoWrapper cw)
            {
                SettlementChangeFactory.SetKingdomRuler(kw.Inner, cw.Inner);
            }
        }

        // ========== Clan 相关 ==========

        public IEnumerable<IClanInfo> GetAllClans()
        {
            if (Campaign.Current?.Clans == null)
                return Enumerable.Empty<IClanInfo>();

            return Campaign.Current.Clans
                .Where(c => !c.IsMinorFaction && !c.IsBanditFaction)
                .Select(c => (IClanInfo)new ClanInfoWrapper(c))
                .ToList();
        }

        public IClanInfo FindClan(string id)
        {
            var clan = ObjectFinder.FindClanById(id);
            return clan != null ? new ClanInfoWrapper(clan) : null;
        }

        public IClanInfo CreateClan(string id, string name)
        {
            // 当前版本未实现 Clan 创建逻辑
            return null;
        }

        public void SetClanKingdom(IClanInfo clan, IKingdomInfo kingdom)
        {
            if (clan is ClanInfoWrapper cw && kingdom is KingdomInfoWrapper kw)
            {
                SettlementChangeFactory.SetClanKingdom(cw.Inner, kw.Inner);
            }
        }

        public void SetClanGold(IClanInfo clan, int gold)
        {
            if (clan is ClanInfoWrapper cw)
            {
                SettlementChangeFactory.SetClanGold(cw.Inner, gold);
            }
        }

        public void SetClanRenown(IClanInfo clan, float renown)
        {
            if (clan is ClanInfoWrapper cw)
            {
                SettlementChangeFactory.SetClanRenown(cw.Inner, renown);
            }
        }

        public void SetClanInfluence(IClanInfo clan, float influence)
        {
            if (clan is ClanInfoWrapper cw)
            {
                SettlementChangeFactory.SetClanInfluence(cw.Inner, influence);
            }
        }

        // ========== Settlement 相关 ==========

        public IEnumerable<ISettlementInfo> GetAllSettlements()
        {
            if (Settlement.All == null)
                return Enumerable.Empty<ISettlementInfo>();

            return Settlement.All
                .Where(s => s.IsTown || s.IsCastle || s.IsVillage)
                .Select(s => (ISettlementInfo)new SettlementInfoWrapper(s))
                .ToList();
        }

        public ISettlementInfo FindSettlement(string id)
        {
            var settlement = ObjectFinder.FindSettlementById(id);
            return settlement != null ? new SettlementInfoWrapper(settlement) : null;
        }

        public void ChangeSettlementOwner(ISettlementInfo settlement, IClanInfo newOwner)
        {
            AffixLogger.Info("ADAPTER", $"ChangeSettlementOwner: settlement={settlement?.Name}({settlement?.Id}), newOwner={newOwner?.Name}({newOwner?.Id})");

            if (settlement is SettlementInfoWrapper sw && newOwner is ClanInfoWrapper cw)
            {
                AffixLogger.Info("ADAPTER", $"类型转换成功, Inner settlement={sw.Inner.Name}, Inner clan={cw.Inner.Name}");
                SettlementChangeFactory.ChangeOwner(sw.Inner, cw.Inner);
            }
            else
            {
                AffixLogger.Error("ADAPTER", $"类型转换失败: settlement is SettlementInfoWrapper={settlement is SettlementInfoWrapper sw2}, newOwner is ClanInfoWrapper={newOwner is ClanInfoWrapper cw2}");
            }
        }

        public void SetSettlementProsperity(ISettlementInfo settlement, int prosperity)
        {
            if (settlement is SettlementInfoWrapper sw)
            {
                SettlementChangeFactory.SetProsperity(sw.Inner, prosperity);
            }
        }

        // ========== 内部包装类 ==========

        private sealed class KingdomInfoWrapper : IKingdomInfo
        {
            public Kingdom Inner { get; }

            public KingdomInfoWrapper(Kingdom kingdom)
            {
                Inner = kingdom;
            }

            public string Id => Inner.StringId;
            public string Name => Inner.Name?.ToString() ?? "";
            public string RulerClanId => Inner.RulingClan?.StringId ?? "";
            public string Culture => Inner.Culture?.StringId ?? "";
        }

        private sealed class ClanInfoWrapper : IClanInfo
        {
            public Clan Inner { get; }

            public ClanInfoWrapper(Clan clan)
            {
                Inner = clan;
            }

            public string Id => Inner.StringId;
            public string Name => Inner.Name?.ToString() ?? "";
            public string KingdomId => Inner.Kingdom?.StringId ?? "";
            public int Tier => Inner.Tier;
            public int Gold => Inner.Gold;
            public float Renown => Inner.Renown;
            public float Influence => Inner.Influence;
            public bool IsDestroyed => Inner.IsEliminated;
        }

        private sealed class SettlementInfoWrapper : ISettlementInfo
        {
            public Settlement Inner { get; }

            public SettlementInfoWrapper(Settlement settlement)
            {
                Inner = settlement;
            }

            public string Id => Inner.StringId;
            public string Name => Inner.Name?.ToString() ?? "";
            public string Type => Inner.IsTown ? "Town" : Inner.IsCastle ? "Castle" : "Village";
            public string OwnerClanId => Inner.OwnerClan?.StringId ?? "";
            public string OwnerKingdomId => Inner.OwnerClan?.Kingdom?.StringId ?? "";
            public string Culture => Inner.Culture?.StringId ?? "";
            public int Prosperity => (int)(Inner.Town?.Prosperity ?? 0);
        }
    }
}
