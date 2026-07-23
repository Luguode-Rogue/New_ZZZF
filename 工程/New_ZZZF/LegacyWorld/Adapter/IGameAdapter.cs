using System.Collections.Generic;

namespace New_ZZZF.LegacyWorld.Adapter
{
    /// <summary>
    /// 游戏适配器接口。
    /// 用于隔离 Core 层与 Bannerlord API 的直接依赖，支持多版本兼容。
    /// 所有与 TaleWorlds 的交互必须通过此接口。
    /// </summary>
    public interface IGameAdapter
    {
        // ========== 世界信息 ==========

        /// <summary>获取当前世界的唯一标识</summary>
        string GetWorldId();

        /// <summary>获取当前游戏内时间（字符串格式）</summary>
        string GetCurrentGameTime();

        /// <summary>获取主导文化</summary>
        string GetDominantCulture();

        /// <summary>获取游戏版本号</summary>
        string GetGameVersion();

        // ========== Kingdom 相关 ==========

        /// <summary>获取所有王国</summary>
        IEnumerable<IKingdomInfo> GetAllKingdoms();

        /// <summary>根据 ID 查找王国，找不到返回 null</summary>
        IKingdomInfo FindKingdom(string id);

        /// <summary>设置王国的统治者家族</summary>
        void SetKingdomRuler(IKingdomInfo kingdom, IClanInfo rulerClan);

        // ========== Clan 相关 ==========

        /// <summary>获取所有家族</summary>
        IEnumerable<IClanInfo> GetAllClans();

        /// <summary>根据 ID 查找家族，找不到返回 null</summary>
        IClanInfo FindClan(string id);

        /// <summary>创建新家族（预留，当前返回 null）</summary>
        IClanInfo CreateClan(string id, string name);

        /// <summary>设置家族所属王国</summary>
        void SetClanKingdom(IClanInfo clan, IKingdomInfo kingdom);

        /// <summary>设置家族金币</summary>
        void SetClanGold(IClanInfo clan, int gold);

        /// <summary>设置家族声望</summary>
        void SetClanRenown(IClanInfo clan, float renown);

        /// <summary>设置家族影响力</summary>
        void SetClanInfluence(IClanInfo clan, float influence);

        // ========== Settlement 相关 ==========

        /// <summary>获取所有定居点</summary>
        IEnumerable<ISettlementInfo> GetAllSettlements();

        /// <summary>根据 ID 查找定居点，找不到返回 null</summary>
        ISettlementInfo FindSettlement(string id);

        /// <summary>变更定居点的所有者家族</summary>
        void ChangeSettlementOwner(ISettlementInfo settlement, IClanInfo newOwner);

        /// <summary>设置定居点繁荣度</summary>
        void SetSettlementProsperity(ISettlementInfo settlement, int prosperity);
    }

    // ========== 数据传输对象接口 ==========

    public interface IKingdomInfo
    {
        string Id { get; }
        string Name { get; }
        string RulerClanId { get; }
        string Culture { get; }
    }

    public interface IClanInfo
    {
        string Id { get; }
        string Name { get; }
        string KingdomId { get; }
        int Tier { get; }
        int Gold { get; }
        float Renown { get; }
        float Influence { get; }
        bool IsDestroyed { get; }
    }

    public interface ISettlementInfo
    {
        string Id { get; }
        string Name { get; }
        string Type { get; } // Town / Castle / Village
        string OwnerClanId { get; }
        string OwnerKingdomId { get; }
        string Culture { get; }
        int Prosperity { get; }
    }
}
