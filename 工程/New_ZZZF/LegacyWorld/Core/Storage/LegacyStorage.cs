using System;
using System.IO;

namespace New_ZZZF.LegacyWorld.Core.Storage
{
    /// <summary>
    /// Legacy 文件的存储路径管理。
    /// 保存位置：{MyDocuments}\Mount & Blade II Bannerlord\LegacyWorld\Legacy.json
    /// </summary>
    public static class LegacyStorage
    {
        private const string DirectoryName = "LegacyWorld";
        private const string FileName = "Legacy.json";

        /// <summary>
        /// Legacy 文件所在的目录路径。
        /// </summary>
        public static string LegacyDirectory
        {
            get
            {
                string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(myDocs, "Mount & Blade II Bannerlord", DirectoryName);
            }
        }

        /// <summary>
        /// Legacy.json 文件的完整路径。
        /// </summary>
        public static string LegacyFile => Path.Combine(LegacyDirectory, FileName);

        /// <summary>
        /// 检查 Legacy.json 是否存在。
        /// </summary>
        public static bool Exists()
        {
            return File.Exists(LegacyFile);
        }

        /// <summary>
        /// 确保存储目录存在。如果不存在则创建。
        /// </summary>
        public static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(LegacyDirectory))
            {
                Directory.CreateDirectory(LegacyDirectory);
            }
        }
    }
}
