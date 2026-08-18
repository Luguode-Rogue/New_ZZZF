using System;
using System.IO;
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// TacticalMap HTMLUI 一次性/低频诊断日志。
    /// 不记录每帧数据，只记录生命周期、输入和页面转换关键节点。
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
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                _path = Path.Combine(dir, "TacticalMapHtmlUiDebug.log");
                File.WriteAllText(_path, string.Empty);
            }
            catch { }
            Log("DEBUG_INIT", "diagnostic logger initialized");
        }

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
