using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
        private static string _path;
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
                    assemblyDir = Path.GetDirectoryName(typeof(TacticalMapHtmlUiDebug).Assembly.Location);
                }
                catch { }

                string modDir = FindModDirectory(assemblyDir);
                string logDir = modDir ?? assemblyDir;

                if (string.IsNullOrWhiteSpace(logDir))
                    logDir = Environment.CurrentDirectory;

                Directory.CreateDirectory(logDir);
                _path = Path.Combine(logDir, "TacticalMapHtmlUiDebug.log");

                File.WriteAllText(_path, string.Empty);
            }
            catch
            {
                try
                {
                    _path = Path.Combine(Environment.CurrentDirectory, "TacticalMapHtmlUiDebug.log");
                    File.WriteAllText(_path, string.Empty);
                }
                catch { }
            }

            Log("DEBUG_INIT", "diagnostic logger initialized; path=" + (_path ?? "<none>"));
        }

        private static string FindModDirectory(string assemblyDir)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(assemblyDir))
                {
                    string current = Path.GetFullPath(assemblyDir);
                    for (int i = 0; i < 8 && !string.IsNullOrWhiteSpace(current); i++)
                    {
                        string name = Path.GetFileName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        if (string.Equals(name, "New_ZZZF", StringComparison.OrdinalIgnoreCase))
                            return current;

                        current = Directory.GetParent(current)?.FullName;
                    }
                }
            }
            catch { }

            try
            {
                string steam = Path.GetFullPath(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Steam", "steamapps", "common", "Mount & Blade II Bannerlord", "Modules", "New_ZZZF"));
                if (Directory.Exists(steam)) return steam;
            }
            catch { }

            try
            {
                string steam64 = Path.GetFullPath(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Steam", "steamapps", "common", "Mount & Blade II Bannerlord", "Modules", "New_ZZZF"));
                if (Directory.Exists(steam64)) return steam64;
            }
            catch { }

            return null;
        }

        public static string Path => _path;

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
                        if (!string.IsNullOrWhiteSpace(_path))
                            File.AppendAllText(_path, line + Environment.NewLine);
                    }
                }
                catch { }

                try { Debug.Print("[TMapHtmlUI] " + line); } catch { }
            }
            catch { }
        }
    }
}
