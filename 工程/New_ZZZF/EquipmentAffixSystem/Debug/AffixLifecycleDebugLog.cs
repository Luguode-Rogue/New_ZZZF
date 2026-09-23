namespace New_ZZZF
{
    /// <summary>
    /// 装备词缀实例生命周期诊断日志。
    /// 本轮排查的所有新增日志统一从这里输出，排查完成后可整体删除本文件。
    /// </summary>
    internal static class AffixLifecycleDebugLog
    {
        internal static void Info(string message) => Write("INFO", message);
        internal static void Warn(string message) => Write("WARN", message);
        internal static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            // 生命周期日志仅用于临时排查，不能在 Harmony/战场回调内同步写盘。
        }
    }
}
