using System;
using System.IO;
using System.Text;

namespace New_ZZZF.ActionExplorer.M0_Probe
{
    /// <summary>
    /// M0 探针独立日志（不依赖 Debug.Print，直接落盘到工程诊断文件）。
    /// 日志路径：ActionExplorer_diag.log
    /// </summary>
    internal static class M0Log
    {
        private static readonly string LogPath =
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Modules", "New_ZZZF", "工程", "ActionExplorer_diag.log");

        private static readonly object Lock = new object();

        public static void Info(string msg) => Write("INFO", msg);
        public static void Warn(string msg) => Write("WARN", msg);
        public static void Error(string msg) => Write("ERROR", msg);

        /// <summary>
        /// 生命周期事件日志（规划第十四节）：记录每个阶段最后一个成功节点，
        /// 异常时据此定位断点。stage 形如 "[M0]"、"[M1]"...，evt 为事件名。
        /// </summary>
        public static void Lifecycle(string stage, string evt)
            => Write("LIFECYCLE", $"[{stage}] {evt}");

        private static void Write(string level, string msg)
        {
            try
            {
                var line = $"[{DateTime.Now:HH:mm:ss.fff}][M0][{level}] {msg}{Environment.NewLine}";
                lock (Lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                    File.AppendAllText(LogPath, line, Encoding.UTF8);
                }
            }
            catch
            {
                // 日志失败不阻断探针
            }
        }
    }
}
