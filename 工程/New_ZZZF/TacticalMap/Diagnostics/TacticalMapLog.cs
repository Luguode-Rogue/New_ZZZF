using System;
using System.IO;
using System.Text;

namespace New_ZZZF.TacticalMap.Diagnostics
{
    /// <summary>
    /// TacticalMap 专用诊断日志。
    /// 日志写到实际加载 New_ZZZF.dll 的 Mod 根目录：Modules/New_ZZZF/New_ZZZF_TacticalMap.log。
    /// 日志失败不能影响游戏流程。
    /// </summary>
    public static class TacticalMapLog
    {
        private static readonly object Sync = new object();
        private static string _logPath;
        private static bool _initialized;
        private const long MaxLogBytes = 8L * 1024L * 1024L;

        public static string LogPath
        {
            get
            {
                EnsureInitialized();
                return _logPath;
            }
        }

        public static void Initialize()
        {
            EnsureInitialized();
            Write("BOOT", "TacticalMap logger initialized. Path=" + _logPath);
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

            Write("ERROR", message + " | " + ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace);
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
                    RotateIfNeeded();
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

        private static void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(_logPath) || new FileInfo(_logPath).Length < MaxLogBytes)
                    return;

                string backup = _logPath + ".old";
                try
                {
                    if (File.Exists(backup))
                        File.Delete(backup);
                }
                catch { }

                try { File.Move(_logPath, backup); }
                catch { }
            }
            catch
            {
                // Ignore rotation failures.
            }
        }
    }
}
