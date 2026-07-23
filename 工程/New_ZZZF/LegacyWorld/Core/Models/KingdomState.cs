using Newtonsoft.Json;

namespace New_ZZZF.LegacyWorld.Core.Models
{
    /// <summary>
    /// 王国状态数据模型。
    /// 记录一个王国的核心信息：ID、名称、统治者家族、文化。
    /// </summary>
    public class KingdomState
    {
        /// <summary>王国 ID，如 "kingdom_empire"</summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>王国显示名称</summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>统治者家族 ID</summary>
        [JsonProperty("ruler_clan_id")]
        public string RulerClanId { get; set; }

        /// <summary>王国文化</summary>
        [JsonProperty("culture")]
        public string Culture { get; set; }
    }
}
