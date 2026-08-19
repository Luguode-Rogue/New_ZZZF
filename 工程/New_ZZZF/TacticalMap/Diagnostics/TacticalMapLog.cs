using System;
using System.IO;
using System.Text;

namespace New_ZZZF.TacticalMap.Diagnostics
{
    /// <summary>
    /// TacticalMap 专用诊断日志。
    /// 每次 SubModule 加载创建一份全新的日志：
    /// Modules/New_ZZZF/Logs/New_ZZZF_TacticalMap.log
    /// </summary>
    public static class TacticalMapLog
    {
        private static readonly object Sync = new object();
        private static string _logPath;
        private static bool _initialized;
        private static bool _sessionStarted;

        public static string LogPath
        {
            get { EnsureInitialized(); return _logPath; }
        }

        public static void Initialize()
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(_logPath)) return;

            lock (Sync)
            {
                if (_sessionStarted) return;
                try
                {
                    File.WriteAllText(_logPath, string.Empty, new UTF8Encoding(false));
                    _sessionStarted = true;
                    WriteUnlocked("BOOT", "===== NEW GAME SESSION =====");
                }
                catch { }
            }
        }

        public static void Info(string message) { Write("INFO", message); }
        public static void Warn(string message) { Write("WARN", message); }

        public static void Error(string message, Exception ex = null)
        {
            Write("ERROR", ex == null
                ? message
                : message + " | " + ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace);
        }

        public static void Section(string name) { Write("TRACE", "========== " + name + " =========="); }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (Sync)
            {
                if (_initialized) return;
                try
                {
                    string assemblyPath = typeof(TacticalMapLog).Assembly.Location;
                    string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? ".";
                    string moduleDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", ".."));
                    string logDirectory = Path.Combine(moduleDir, "Logs");
                    Directory.CreateDirectory(logDirectory);
                    _logPath = Path.Combine(logDirectory, "New_ZZZF_TacticalMap.log");
                }
                catch { _logPath = null; }
                _initialized = true;
            }
        }

        private static void Write(string level, string message)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(_logPath)) return;

            lock (Sync)
            {
                try
                {
                    if (!_sessionStarted)
                    {
                        File.WriteAllText(_logPath, string.Empty, new UTF8Encoding(false));
                        _sessionStarted = true;
                        WriteUnlocked("BOOT", "===== NEW GAME SESSION =====");
                    }
                    WriteUnlocked(level, message);
                }
                catch { }
            }
        }

        private static void WriteUnlocked(string level, string message)
        {
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
}