using System;
using System.IO;

namespace New_ZZZF
{
    /// <summary>
    /// 装备词缀生命周期排查日志。
    /// 所有本轮新增诊断日志统一写入此文件，排查完成后删除本文件即可移除本轮日志。
    /// </summary>
    internal static class AffixLifecycleDebugLog
    {
        private static readonly object SyncRoot = new object();
        private static readonly string LogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Modules",
            "New_ZZZF",
            "affix_lifecycle_debug.log");

        public static bool Enabled = true;

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            if (!Enabled) return;

            try
            {
                lock (SyncRoot)
                {
                    string directory = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    File.AppendAllText(
                        LogPath,
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
