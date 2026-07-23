using Newtonsoft.Json;

namespace New_ZZZF.LegacyWorld.Core.Models
{
    /// <summary>
    /// 家族（Clan）状态数据模型。
    /// 记录家族的金币、声望、影响力、所属王国等经济与政治信息。
    /// </summary>
    public class ClanState
    {
        /// <summary>家族 ID</summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>家族名称</summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>所属王国 ID</summary>
        [JsonProperty("kingdom_id")]
        public string KingdomId { get; set; }

        /// <summary>家族等级</summary>
        [JsonProperty("tier")]
        public int Tier { get; set; }

        /// <summary>金币</summary>
        [JsonProperty("gold")]
        public int Gold { get; set; }

        /// <summary>声望</summary>
        [JsonProperty("renown")]
        public float Renown { get; set; }

        /// <summary>影响力</summary>
        [JsonProperty("influence")]
        public float Influence { get; set; }

        /// <summary>是否已经毁灭</summary>
        [JsonProperty("is_destroyed")]
        public bool IsDestroyed { get; set; }
    }
}
