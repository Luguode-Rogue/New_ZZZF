using System;
using System.IO;

namespace New_ZZZF
{
    /// <summary>
    /// 装备词缀实例生命周期诊断日志。
    /// 本轮排查的所有新增日志统一从这里输出，排查完成后可整体删除本文件。
    /// </summary>
    internal static class AffixLifecycleDebugLog
    {
        private static readonly object Sync = new object();

        private static string FilePath
        {
            get
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                return Path.GetFullPath(Path.Combine(
                    baseDirectory,
                    "..", "..", "Modules", "New_ZZZF",
                    "affix_lifecycle_debug.log"));
            }
        }

        internal static void Info(string message) => Write("INFO", message);
        internal static void Warn(string message) => Write("WARN", message);
        internal static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    string path = FilePath;
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    File.AppendAllText(
                        path,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // 诊断日志绝不能影响游戏逻辑。
            }
        }
    }
}
