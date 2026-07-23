using System.IO;
using Newtonsoft.Json;
using New_ZZZF.LegacyWorld.Core.Models;

namespace New_ZZZF.LegacyWorld.Core.Serialization
{
    /// <summary>
    /// Legacy 数据的 JSON 序列化/反序列化器。
    /// 使用 Newtonsoft.Json 实现，支持格式化输出和错误安全加载。
    /// </summary>
    public static class LegacySerializer
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        /// <summary>
        /// 将 LegacyData 序列化为 JSON 字符串。
        /// </summary>
        public static string Serialize(LegacyData data)
        {
            return JsonConvert.SerializeObject(data, Settings);
        }

        /// <summary>
        /// 将 LegacyData 序列化并写入文件。
        /// 使用原子写入：先写入临时文件，再重命名覆盖，防止文件损坏。
        /// </summary>
        public static void Save(LegacyData data, string filePath)
        {
            string tmpPath = filePath + ".tmp";
            string json = Serialize(data);
            File.WriteAllText(tmpPath, json);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tmpPath, filePath);
        }

        /// <summary>
        /// 从 JSON 文件中反序列化为 LegacyData。
        /// 如果文件不存在或解析失败，返回 null。
        /// </summary>
        public static LegacyData Load(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<LegacyData>(json, Settings);
            }
            catch
            {
                return null;
            }
        }
    }
}
