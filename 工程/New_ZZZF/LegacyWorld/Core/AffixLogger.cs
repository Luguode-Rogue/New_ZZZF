using System;
using System.IO;
using System.Reflection;

namespace New_ZZZF.LegacyWorld.Core
{
    /// <summary>
    /// Affix 调试日志器，将日志写入模块根目录下的 affix_debug.log。
    /// 文件位置与 SubModule.xml 同目录：Modules\New_ZZZF\affix_debug.log
    /// 可通过 <see cref="LogEnabled"/> 在运行时开关日志输出。
    /// </summary>
    public static class AffixLogger
    {
        private static readonly string LogPath;
        private static readonly object _lock = new();

        /// <summary>
        /// 日志主开关。设为 false 后不再写入日志文件。
        /// 由 LegacyWorldMCMSettings（MCM 面板「启用调试日志」）通过 LegacyBehavior.RegisterEvents 初始化同步。
        /// </summary>
        public static bool LogEnabled { get; set; } = true;

        static AffixLogger()
        {
            // 通过 DLL 路径自动定位模块根目录
            try
            {
                string dllPath = Assembly.GetExecutingAssembly().Location;
                string dllDir = Path.GetDirectoryName(dllPath);
                // DLL 位于 Modules\New_ZZZF\bin\Win64_Shipping_Client\ → 向上 2 级到模块根
                string moduleDir = Path.GetDirectoryName(Path.GetDirectoryName(dllDir));
                LogPath = Path.Combine(moduleDir, "affix_debug.log");

                // 清空旧日志，确保每次游戏启动写新文件
                File.WriteAllText(LogPath, $"[{Timestamp}] === AffixDebugLog START ===\r\n");
            }
            catch
            {
                // 如果写不了日志也不能崩游戏
                LogPath = null;
            }
        }

        private static string Timestamp =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        /// <summary>
        /// 写一行日志到 affix_debug.log。
        /// </summary>
        public static void Log(string message)
        {
            if (!LogEnabled || LogPath == null)
                return;

            try
            {
                lock (_lock)
                {
                    File.AppendAllText(LogPath, $"[{Timestamp}] {message}\r\n");
                }
            }
            catch
            {
                // 日志失败绝对不能崩游戏
            }
        }

        /// <summary>
        /// 写一行带前缀标记的日志（方便 grep 过滤）。
        /// </summary>
        public static void Info(string tag, string message)
        {
            Log($"[{tag}] {message}");
        }

        /// <summary>
        /// 写错误日志。
        /// </summary>
        public static void Error(string tag, string message, Exception ex = null)
        {
            if (ex != null)
                Log($"[{tag}][ERROR] {message} | Exception: {ex.GetType().Name}: {ex.Message}");
            else
                Log($"[{tag}][ERROR] {message}");
        }

        /// <summary>
        /// 获取当前日志文件完整路径（供 UI 或调试显示）。
        /// </summary>
        public static string GetLogPath() => LogPath;
    }
}
