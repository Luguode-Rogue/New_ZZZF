// using System;
// using System.IO;
// using TaleWorlds.CampaignSystem;
// using TaleWorlds.Library;

namespace New_ZZZF
{
    // =========================================================================
    // 暂时停用：技能界面统一日志助手
    // （将日志同时输出到：1) 游戏内 InformationManager 消息  2) 文本文件）
    // =========================================================================
    public static class SkillDebug
    {
        /*
        /// <summary>日志文件路径：Documents\Mount and Blade II Bannerlord\SkillDebug.log</summary>
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "SkillDebug.log");

        private static readonly object _lock = new object();
        private static int _logCount = 0;

        /// <summary>
        /// 记录一条调试日志（游戏内显示 + 文件写入）
        /// </summary>
        public static void Log(string msg)
        {
            int num = ++_logCount;
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string fullMsg = $"[{num:D3}] [{timestamp}] {msg}";

            // 游戏内显示
            InformationManager.DisplayMessage(new InformationMessage(fullMsg));

            // 写入文件
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(LogPath, fullMsg + Environment.NewLine);
                }
            }
            catch
            {
                // 文件写入失败不抛异常，避免影响游戏运行
            }
        }
        */

        /// <summary>暂时停用的空方法，保留接口不变</summary>
        public static void Log(string msg)
        {
            // 暂时停用
        }
    }
}
