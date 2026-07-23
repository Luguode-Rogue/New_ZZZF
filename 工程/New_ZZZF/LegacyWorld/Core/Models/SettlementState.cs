using Newtonsoft.Json;

namespace New_ZZZF.LegacyWorld.Core.Models
{
    /// <summary>
    /// 定居点（城镇/城堡/村庄）状态数据模型。
    /// 记录该定居点的所有者、所属王国及繁荣度。
    /// </summary>
    public class SettlementState
    {
        /// <summary>定居点 ID</summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>定居点名称</summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>类型：Town / Castle / Village</summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>所有者家族 ID</summary>
        [JsonProperty("owner_clan_id")]
        public string OwnerClanId { get; set; }

        /// <summary>所属王国 ID</summary>
        [JsonProperty("owner_kingdom_id")]
        public string OwnerKingdomId { get; set; }

        /// <summary>文化</summary>
        [JsonProperty("culture")]
        public string Culture { get; set; }

        /// <summary>繁荣度</summary>
        [JsonProperty("prosperity")]
        public int Prosperity { get; set; }
    }
}
