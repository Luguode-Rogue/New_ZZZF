using System;
using System.Diagnostics;
using System.IO;
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// TacticalMap HTMLUI 一次性/低频诊断日志。
    /// 优先写入实际 Mod 目录；若无法取得则回退到工程目录，再回退到当前目录。
    /// </summary>
    internal static class TacticalMapHtmlUiDebug
    {
        private static readonly object Sync = new object();
        private static string _logPath;
        private static bool _initialized;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                string assemblyDir = null;
                try
                {
                    assemblyDir = System.IO.Path.GetDirectoryName(typeof(TacticalMapHtmlUiDebug).Assembly.Location);
                }
                catch { }

                string modDir = FindModDirectory(assemblyDir);
                string logDir = modDir ?? assemblyDir;

                if (string.IsNullOrWhiteSpace(logDir))
                    logDir = Environment.CurrentDirectory;

                Directory.CreateDirectory(logDir);
                _logPath = System.IO.Path.Combine(logDir, "TacticalMapHtmlUiDebug.log");
                File.WriteAllText(_logPath, string.Empty);
            }
            catch
            {
                try
                {
                    _logPath = System.IO.Path.Combine(Environment.CurrentDirectory, "TacticalMapHtmlUiDebug.log");
                    File.WriteAllText(_logPath, string.Empty);
                }
                catch { }
            }

            Log("DEBUG_INIT", "diagnostic logger initialized; path=" + (_logPath ?? "<none>"));
        }

        private static string FindModDirectory(string assemblyDir)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(assemblyDir))
                {
                    string current = System.IO.Path.GetFullPath(assemblyDir);
                    for (int i = 0; i < 8 && !string.IsNullOrWhiteSpace(current); i++)
                    {
                        string name = System.IO.Path.GetFileName(current.TrimEnd(
                            System.IO.Path.DirectorySeparatorChar,
                            System.IO.Path.AltDirectorySeparatorChar));
                        if (string.Equals(name, "New_ZZZF", StringComparison.OrdinalIgnoreCase))
                            return current;

                        current = Directory.GetParent(current)?.FullName;
                    }
                }
            }
            catch { }

            try
            {
                string steam = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Steam", "steamapps", "common", "Mount & Blade II Bannerlord", "Modules", "New_ZZZF"));
                if (Directory.Exists(steam)) return steam;
            }
            catch { }

            try
            {
                string steam64 = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Steam", "steamapps", "common", "Mount & Blade II Bannerlord", "Modules", "New_ZZZF"));
                if (Directory.Exists(steam64)) return steam64;
            }
            catch { }

            return null;
        }

        public static string LogFilePath => _logPath;

        public static void Log(string stage, string message)
        {
            try
            {
                if (!_initialized) Init();
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{stage}] {message}";
                try
                {
                    lock (Sync)
                    {
                        if (!string.IsNullOrWhiteSpace(_logPath))
                            File.AppendAllText(_logPath, line + Environment.NewLine);
                    }
                }
                catch { }

                try { TaleWorlds.Library.Debug.Print("[TMapHtmlUI] " + line); } catch { }
            }
            catch { }
        }
    }
}
