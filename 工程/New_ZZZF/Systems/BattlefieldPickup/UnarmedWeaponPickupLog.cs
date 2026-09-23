using System;
using System.IO;
using System.Text;

namespace New_ZZZF.Systems.BattlefieldPickup
{
    /// <summary>
    /// 无武器 AI 拾取系统专用文件日志。
    /// Modules/New_ZZZF/Logs/New_ZZZF_UnarmedWeaponPickup.log
    /// </summary>
    internal static class UnarmedWeaponPickupLog
    {
        private static readonly object Sync = new object();
        private static string _logPath;
        private static bool _initialized;
        private static bool _sessionStarted;

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
                        WriteUnlocked("BOOT", "===== NEW GAME SESSION =====");
                    }

                    WriteUnlocked("INFO", message);
                }
                catch
                {
                    // 日志失败不得影响战斗逻辑。
                }
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
                    string assemblyDir = Path.GetDirectoryName(typeof(UnarmedWeaponPickupLog).Assembly.Location) ?? ".";
                    string moduleDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", ".."));
                    string logDirectory = Path.Combine(moduleDir, "Logs");
                    Directory.CreateDirectory(logDirectory);
                    _logPath = Path.Combine(logDirectory, "New_ZZZF_UnarmedWeaponPickup.log");
                }
                catch
                {
                    _logPath = null;
                }

                _initialized = true;
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
