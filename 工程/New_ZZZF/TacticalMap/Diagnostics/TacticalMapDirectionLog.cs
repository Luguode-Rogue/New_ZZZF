using System;
using System.IO;
using System.Text;

namespace New_ZZZF.TacticalMap.Diagnostics
{
    /// <summary>
    /// Temporary TacticalMap direction/coordinate diagnostic log.
    /// Intentionally separate from the normal TacticalMap log and cleared once per process.
    /// </summary>
    public static class TacticalMapDirectionLog
    {
        private static readonly object Sync = new object();
        private static string _logPath;
        private static bool _initialized;
        private static bool _sessionStarted;

        public static string LogPath
        {
            get { EnsureInitialized(); return _logPath; }
        }

        public static void Info(string message)
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
                        WriteUnlocked("===== TACTICAL MAP DIRECTION DEBUG SESSION =====");
                    }

                    WriteUnlocked(message);
                }
                catch { }
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (Sync)
            {
                if (_initialized) return;
                try
                {
                    string assemblyPath = typeof(TacticalMapDirectionLog).Assembly.Location;
                    string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? ".";
                    string moduleDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", ".."));
                    string logDirectory = Path.Combine(moduleDir, "Logs");
                    Directory.CreateDirectory(logDirectory);
                    _logPath = Path.Combine(logDirectory, "New_ZZZF_TacticalMap_DirectionDebug.log");
                }
                catch { _logPath = null; }
                _initialized = true;
            }
        }

        private static void WriteUnlocked(string message)
        {
            string line = string.Format(
                "[{0:yyyy-MM-dd HH:mm:ss.fff}] [DIRECTION] [Thread:{1}] {2}{3}",
                DateTime.Now,
                System.Threading.Thread.CurrentThread.ManagedThreadId,
                message,
                Environment.NewLine);
            File.AppendAllText(_logPath, line, new UTF8Encoding(false));
        }
    }
}
