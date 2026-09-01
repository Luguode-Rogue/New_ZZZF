using System;

namespace New_ZZZF.ArcherReposition
{
    /// <summary>
    /// 射手防发呆重定位 —— 运行时配置。
    ///
    /// 调参建议（500v500 场景实测）：
    /// - 帧预算吃紧 → 调低 MaxRayUnitsPerFrame 或降低 TargetSampleCount
    /// - 侧移太粘滞（挡→移→通→再判挡振荡）→ 提高 ExitCooldown
    /// - 侧移幅度不够 → 调大 StrafeOffsets
    /// </summary>
    internal static class ArcherRepositionConfig
    {
        /// <summary>总开关。false 时本功能完全休眠（检测 postfix 第一行即返回，行为与原版一致）。</summary>
        public static bool Enable = true;

        /// <summary>true = 只处理玩家方（含友方队伍）的射手；false = 双方都处理。</summary>
        public static bool OnlyPlayerTeam = true;

        // ---------- 检测 ----------

        /// <summary>连续 N 次读到"非 TargetIsClear"才判定被挡（可见性是引擎 native 缓存值，有滞后，须防抖）。</summary>
        public static int DetectionStreak = 2;

        /// <summary>连续 N 次读到 TargetIsClear 才认定遮挡解除。</summary>
        public static int ClearStreakToExit = 2;

        /// <summary>IsRanged 缓存多少个检测周期刷新一次（检测周期 ≈ 0.45~0.55s/agent 错峰）。</summary>
        public static int IsRangedRefreshCycles = 8;

        // ---------- 换目标 ----------

        /// <summary>从敌方 ActiveAgents 随机采样的候选数（纯数学预筛免费，射线验证受限流）。</summary>
        public static int TargetSampleCount = 4;

        /// <summary>候选目标距离下限（米）。太近换它没意义，交给近战逻辑。</summary>
        public static float MinTargetDistance = 10f;

        /// <summary>候选目标距离 = 射程 × 该系数 为上限。</summary>
        public static float TargetRangeFactor = 0.9f;

        // ---------- 侧移 ----------

        /// <summary>侧移尝试幅度序列（米），逐级尝试；升级时取下一档。</summary>
        public static float[] StrafeOffsets = { 1.2f, 2.4f };

        /// <summary>与目标距离小于该值（米）时不侧移（近距离直接换目标更合理）。</summary>
        public static float MinStrafeDistance = 14f;

        /// <summary>侧移最长持续时间（秒），硬超时兜底防粘滞。</summary>
        public static float StrafeTimeout = 4f;

        /// <summary>侧移中连续 N 个检测周期仍未恢复通视 → 升级（加大幅度/换方向）。</summary>
        public static int StrafeEscalateStreak = 6;

        /// <summary>退出（解除/超时/失败）后的冷却（秒），防振荡。</summary>
        public static float ExitCooldown = 0.75f;

        // ---------- 射线与预算 ----------

        /// <summary>每帧射线预算（加权单位）。RayCastForClosestAgent = AgentRayCost，Scene 射线 = SceneRayCost。
        /// 参照：本体 FocusTick 每帧常态 7~8 次射线。</summary>
        public static int MaxRayUnitsPerFrame = 12;

        public static int AgentRayCost = 2;
        public static int SceneRayCost = 1;

        /// <summary>射线厚度（米）。取弹丸半径量级：略厚可把"贴着友军飞过"也算挡，符合射手主观判断。</summary>
        public static float RayThickness = 0.25f;

        /// <summary>地形射线：命中距离比到目标距离近 0.5m 以上才算挡（容差防误报）。</summary>
        public static float TerrainTolerance = 0.5f;

        // ---------- 缓存 ----------

        public static bool CacheEnabled = true;
        public static int CacheSlots = 512;
        /// <summary>缓存 TTL：结果"挡"保守成立可留久，结果"通"快速失效防穿人误判。</summary>
        public static float CacheTtlBlocked = 0.5f;
        public static float CacheTtlClear = 0.1f;

        // ---------- 稳定性 ----------

        /// <summary>连续异常达到该次数后自动熔断禁用（行为退化为原版，绝不变差）。</summary>
        public static int MaxFaults = 5;

        /// <summary>状态槽数量。按 Agent.Index 对齐；1000 agent 战斗留足余量。</summary>
        public static int StateCapacity = 2048;

        /// <summary>调试日志（写入 Logs/ArcherReposition.log）。默认关；只记录状态迁移摘要，无每帧输出。</summary>
        public static bool DebugLog = false;
    }
}
