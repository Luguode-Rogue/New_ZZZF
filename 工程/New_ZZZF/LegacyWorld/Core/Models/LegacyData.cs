using System.Collections.Generic;
using Newtonsoft.Json;

namespace New_ZZZF.LegacyWorld.Core.Models
{
    /// <summary>
    /// 世界遗产数据的顶层容器。
    /// 包含版本信息、世界标识、以及所有需要继承的状态集合。
    /// </summary>
    public class LegacyData
    {
        /// <summary>数据格式版本号，用于向后兼容</summary>
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        /// <summary>当前世界唯一标识，防止同世界互相覆盖</summary>
        [JsonProperty("world_id")]
        public string WorldId { get; set; }

        /// <summary>导出时的游戏内日期</summary>
        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        /// <summary>主导文化</summary>
        [JsonProperty("culture")]
        public string Culture { get; set; }

        /// <summary>Bannerlord 版本号</summary>
        [JsonProperty("game_version")]
        public string GameVersion { get; set; }

        /// <summary>所有王国的状态列表</summary>
        [JsonProperty("kingdoms")]
        public List<KingdomState> Kingdoms { get; set; } = new();

        /// <summary>所有家族的状态列表</summary>
        [JsonProperty("clans")]
        public List<ClanState> Clans { get; set; } = new();

        /// <summary>所有定居点的状态列表</summary>
        [JsonProperty("settlements")]
        public List<SettlementState> Settlements { get; set; } = new();
    }
}
