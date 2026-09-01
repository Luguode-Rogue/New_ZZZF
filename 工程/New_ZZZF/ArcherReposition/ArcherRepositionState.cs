using TaleWorlds.MountAndBlade;

namespace New_ZZZF.ArcherReposition
{
    /// <summary>单兵状态机的阶段。</summary>
    internal static class ArcherStage
    {
        public const byte Idle = 0;          // 未介入
        public const byte ManualTarget = 1;  // 已手动指定目标（SetAutomaticTargetSelection(false) 生效中）
        public const byte Strafe = 2;        // 侧移中（postfix 每周期覆写 formation frame）
        public const byte Cooldown = 3;      // 冷却
    }

    /// <summary>
    /// 单兵热状态（struct，纯标量字段，零堆分配）。
    ///
    /// 线程约定：
    /// - 检测 postfix（TWParallel 工作线程，同一 agent 同一时刻只被一个 worker 处理）读写全部字段；
    /// - 主线程（TickMain）读写全部字段；
    /// - byte/bool/int/float 对齐字段在 x86/x64 上无撕裂写，竞态均为良性（最多晚一个周期生效），
    ///   不加锁 —— 遵循引擎自身 "并行置标志、主线程做副作用" 的纪律（CommonAIComponent 范本）。
    /// </summary>
    internal struct ArcherAgentState
    {
        public byte Stage;
        public byte BadStreak;        // 连续非 Clear 计数
        public byte ClearStreak;      // 连续 Clear 计数
        public byte RefreshCounter;   // IsRanged 缓存刷新计数
        public byte SamplePhase;      // 采样游标（按 agent 自增，多 agent 采样去相关）

        public bool IsRanged;         // 主手是否远程（缓存，定期刷新）
        public bool Dirty;            // postfix → 主线程的处理请求
        public bool RequestExit;      // 请求主线程恢复引擎状态/退出当前阶段

        public bool StrafeApproved;   // 主线程已批准的侧移偏移
        public sbyte StrafeDir;       // +1 / -1
        public float StrafeOffset;    // 当前批准的侧移幅度（米）

        public int ManualTargetIndex; // 手动目标 Agent.Index（调试用）
        public float NextAllowedTime; // 冷却截止时间（Mission.CurrentTime）
        public float StageStartTime;  // 进入当前阶段的时间
    }

    /// <summary>
    /// 状态存储：按 Agent.Index 对齐的定长 struct 数组 + Owner 数组自愈槽位复用。
    /// 零 Dictionary、零队列、零每帧分配。
    ///
    /// 槽位自愈：postfix/主线程访问时若 Owners[i] != agent（旧 agent 已移除、Index 被新 agent 复用），
    /// 即清零重登记 —— 无需依赖 OnAgentCreated/OnAgentRemoved 生命周期钩子。
    /// </summary>
    internal static class ArcherRepositionStateStore
    {
        public static readonly int Capacity = ArcherRepositionConfig.StateCapacity;
        public static readonly ArcherAgentState[] States = new ArcherAgentState[Capacity];
        public static readonly Agent[] Owners = new Agent[Capacity];

        /// <summary>战斗结束时整体重置（防止跨战斗的 stale 引用拖内存）。</summary>
        public static void ResetAll()
        {
            for (int i = 0; i < Capacity; i++)
            {
                States[i] = default;
                Owners[i] = null;
            }
        }
    }
}
