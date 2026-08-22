using System;
using System.IO;
using System.Text;
using System.Threading;

namespace New_ZZZF
{
    /// <summary>
    /// Damage-system diagnostic logger. Independent from TacticalMap logging.
    /// </summary>
    internal static class DamageLog
    {
        private static readonly object Sync = new object();
        private static string _logPath;
        private static bool _initialized;
        private const long MaxLogBytes = 8L * 1024L * 1024L;

        // Keep the diagnostic path cold unless explicitly enabled.
        public static bool Enabled { get; set; }

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
            if (Enabled)
                Write("BOOT", "Damage logger initialized. Path=" + _logPath);
        }

        public static void Info(string message)
        {
            if (!Enabled)
                return;
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            if (!Enabled)
                return;
            Write("WARN", message);
        }

        public static void Error(string message, Exception ex = null)
        {
            if (!Enabled)
                return;

            if (ex == null)
            {
                Write("ERROR", message);
                return;
            }

            Write("ERROR", message + " | " + ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace);
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
                    string assemblyPath = typeof(DamageLog).Assembly.Location;
                    string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? ".";
                    string moduleDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", ".."));
                    Directory.CreateDirectory(moduleDir);
                    _logPath = Path.Combine(moduleDir, "New_ZZZF_Damage.log");
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
                        Thread.CurrentThread.ManagedThreadId,
                        message,
                        Environment.NewLine);
                    File.AppendAllText(_logPath, line, new UTF8Encoding(false));
                }
            }
            catch
            {
                // Diagnostics must never affect gameplay.
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
