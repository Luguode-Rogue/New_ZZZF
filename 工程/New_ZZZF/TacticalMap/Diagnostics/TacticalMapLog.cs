using System;
using System.IO;
using System.Text;

namespace New_ZZZF.TacticalMap.Diagnostics
{
    /// <summary>
    /// TacticalMap 专用诊断日志。
    /// 每次 SubModule 加载都会创建一份全新的日志，写入 Mod 根目录：
    /// Modules/New_ZZZF/New_ZZZF_TacticalMap.log
    /// </summary>
    public static class TacticalMapLog
    {
        private static readonly object Sync = new object();
        private static string _logPath;
        private static bool _initialized;
        private static bool _sessionStarted;

        public static string LogPath
        {
            get
            {
                EnsureInitialized();
                return _logPath;
            }
        }

        /// <summary>
        /// 开始新的游戏/Mod 会话。旧日志直接删除，不做 .old 滚动。
        /// </summary>
        public static void Initialize()
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(_logPath))
                return;

            lock (Sync)
            {
                if (_sessionStarted)
                    return;

                try
                {
                    // 每次游戏启动都从零开始，确保日志只对应当前会话。
                    File.WriteAllText(_logPath, string.Empty, new UTF8Encoding(false));
                    _sessionStarted = true;

                    string header = string.Format(
                        "[{0:yyyy-MM-dd HH:mm:ss.fff}] [BOOT] ===== NEW GAME SESSION ====={1}",
                        DateTime.Now,
                        Environment.NewLine);
                    File.AppendAllText(_logPath, header, new UTF8Encoding(false));
                }
                catch
                {
                    // Diagnostics must never become a gameplay dependency.
                }
            }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);

        public static void Error(string message, Exception ex = null)
        {
            if (ex == null)
            {
                Write("ERROR", message);
                return;
            }

            Write(
                "ERROR",
                message + " | " + ex.GetType().Name + ": " + ex.Message
                + Environment.NewLine + ex.StackTrace);
        }

        public static void Section(string name)
        {
            Write("TRACE", "========== " + name + " ==========");
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (Sync)
            {
                if (_initialized)
                    return;

                try
                {
                    string assemblyPath = typeof(TacticalMapLog).Assembly.Location;
                    string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? ".";
                    string moduleDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", ".."));
                    Directory.CreateDirectory(moduleDir);
                    _logPath = Path.Combine(moduleDir, "New_ZZZF_TacticalMap.log");
                }
                catch
                {
                    _logPath = null;
                }

                _initialized = true;
            }
        }

        private static void Write(string level, string message)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(_logPath))
                return;

            try
            {
                lock (Sync)
                {
                    // 允许日志类在极早期被调用；只要还没建立本会话，就先建立。
                    if (!_sessionStarted)
                    {
                        File.WriteAllText(_logPath, string.Empty, new UTF8Encoding(false));
                        _sessionStarted = true;
                        string header = string.Format(
                            "[{0:yyyy-MM-dd HH:mm:ss.fff}] [BOOT] ===== NEW GAME SESSION ====={1}",
                            DateTime.Now,
                            Environment.NewLine);
                        File.AppendAllText(_logPath, header, new UTF8Encoding(false));
                    }

                    string line = string.Format(
                        "[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] [Thread:{2}] {3}{4}",
                        DateTime.Now,
                        level,
                        System.Threading.Thread.CurrentThread.ManagedThreadId,
                        message,
                        Environment.NewLine);

                    File.AppendAllText(_logPath, line, new UTF8Encoding(false));
                }
            }
            catch
            {
                // Diagnostics must never become a gameplay dependency.
            }
        }
    }
}
