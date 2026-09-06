using System;
using System.Text;

namespace New_ZZZF.TacticalMap.Diagnostics
{
    /// <summary>
    /// 地图功能分段性能采样（仅游戏主线程使用，无锁）。
    /// 由 MissionLogic 心跳每 5s 汇总输出一次各段累计耗时，用于直接定位开战掉帧的归属段。
    /// </summary>
    internal static class TacticalMapPerf
    {
        public const int Snapshots = 0;   // FormationTracker.Update
        public const int Paths = 1;       // RebuildFormationPaths（含跳过判定）
        public const int Agents = 2;      // RebuildAgentSnapshots
        public const int Camera = 3;      // CameraController.Initialize/CaptureBaseHeight/Tick
        public const int RuntimeBuild = 4;// BuildRuntimeState（含签名）
        public const int RuntimeSend = 5; // SendEvent（仅实际发送时）
        public const int StaticPub = 6;   // PublishStaticStateIfChanged（含服务 PNG 写出）
        public const int PhotoTick = 7;   // TickPhotoCapture

        private const int SlotCount = 8;

        private static readonly long[] Ticks = new long[SlotCount];
        private static readonly int[] Calls = new int[SlotCount];
        private static readonly string[] Names =
        {
            "snapshots", "paths", "agents", "camera", "runtimeBuild", "runtimeSend", "staticPub", "photoTick"
        };

        public static void Add(int slot, long elapsedTicks)
        {
            Ticks[slot] += elapsedTicks;
            Calls[slot]++;
        }

        /// <summary>输出 "name=avgMs(calls) ..." 并清零。窗口时长用于换算每秒均摊。</summary>
        public static string DrainReport(double windowSeconds)
        {
            var sb = new StringBuilder(256);
            for (int i = 0; i < SlotCount; i++)
            {
                double ms = Ticks[i] * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (i > 0) sb.Append(' ');
                sb.Append(Names[i]).Append('=')
                  .Append(ms.ToString("0.0")).Append("ms/")
                  .Append(windowSeconds.ToString("0")).Append("s(")
                  .Append(Calls[i]).Append(')');
                Ticks[i] = 0;
                Calls[i] = 0;
            }
            return sb.ToString();
        }
    }
}
