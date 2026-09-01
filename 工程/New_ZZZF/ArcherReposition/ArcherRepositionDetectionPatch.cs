using System;
using System.Linq.Expressions;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.ArcherReposition
{
    /// <summary>
    /// 检测 postfix：挂在 HumanAIComponent.ParallelUpdateFormationMovement() 上。
    ///
    /// 关键事实（源码已验证）：
    /// - 该方法由 Agent.TickParallel 经 _cachedAndFormationValuesUpdateTimer（0.45~0.55s 错峰）调用，
    ///   即本 postfix 天然以 ~0.5s/agent 的频率触发 —— 检测与覆写都不需要自己做每帧节流；
    /// - 它跑在 TWParallel 工作线程（NativeParallelDriver，grainSize=16 分片），
    ///   同一 agent 同一时刻只被一个 worker 处理；
    /// - 原方法自身就在该线程调用 TrySetFormationFrame / GetNavMesh*MT —— 本 postfix 的覆写
    ///   与原方法同线程同上下文，有引擎先例；
    /// - 换目标（SetAutomaticTargetSelection/SetTargetAgent）无并行段先例，只置标志，主线程执行。
    ///
    /// 补丁用显式 Harmony.Apply（EnsurePatched）而非 [HarmonyPatch] 特性，
    /// 避免与本工程已有的 PatchAll(Assembly) 双重应用，且支持独立 UnpatchAll。
    /// </summary>
    internal static class ArcherRepositionDetectionPatch
    {
        // AgentComponent.Agent 是 protected 字段 —— 用表达式树编译一次访问器（零装箱、无每调用反射开销）
        private static readonly Func<HumanAIComponent, Agent?> AgentGetter = CreateAgentGetter();

        private static Func<HumanAIComponent, Agent?> CreateAgentGetter()
        {
            var field = AccessTools.Field(typeof(AgentComponent), "Agent");
            if (field == null)
                return _ => null;
            var instance = Expression.Parameter(typeof(HumanAIComponent), "c");
            return Expression.Lambda<Func<HumanAIComponent, Agent?>>(
                Expression.Field(instance, field), instance).Compile();
        }

        public static void Postfix(HumanAIComponent __instance)
        {
            try
            {
                if (!ArcherRepositionConfig.Enable || ArcherRepositionLogic.Disabled)
                    return;

                Agent? agent = AgentGetter(__instance);
                if (agent == null || !agent.IsAIControlled || agent.Controller != AgentControllerType.AI)
                    return;
                if (GameNetwork.IsClientOrReplay)
                    return;
                // v1 排除骑马射手：坐骑移动由 C++ 骑乘逻辑接管，formation frame 覆写语义不同
                if (agent.MountAgent != null || agent.IsMount || !agent.IsHuman)
                    return;

                int slot = agent.Index;
                if (slot < 0 || slot >= ArcherRepositionStateStore.Capacity)
                    return;

                // 槽位自愈：Index 复用 / 首次见到该 agent
                if (ArcherRepositionStateStore.Owners[slot] != agent)
                {
                    ArcherRepositionStateStore.States[slot] = default;
                    ArcherRepositionStateStore.Owners[slot] = agent;
                    ArcherRepositionStateStore.States[slot].IsRanged = ComputeIsRanged(agent);
                }

                ref ArcherAgentState s = ref ArcherRepositionStateStore.States[slot];

                // "是否远程"缓存刷新（把 2000 次/s 的装备属性链访问变成读一个 bool）
                if (++s.RefreshCounter >= ArcherRepositionConfig.IsRangedRefreshCycles)
                {
                    s.RefreshCounter = 0;
                    s.IsRanged = ComputeIsRanged(agent);
                }
                if (!s.IsRanged)
                    return;

                Formation formation = agent.Formation;
                if (formation == null || agent.IsDetachedFromFormation
                    || formation.Arrangement is ColumnFormation
                    || agent.IsRetreating())
                {
                    // 交给原版逻辑的场景：请求主线程收尾（若有进行中的阶段）
                    if (s.Stage != ArcherStage.Idle && s.Stage != ArcherStage.Cooldown)
                        s.RequestExit = true;
                    s.BadStreak = 0;
                    s.ClearStreak = 0;
                    return;
                }

                Mission mission = agent.Mission;
                if (mission == null)
                    return;
                float now = mission.CurrentTime;

                // 可见性判定（引擎 native 缓存值，0 额外射线成本）
                AITargetVisibilityState vis = agent.GetLastTargetVisibilityState();
                if (vis == AITargetVisibilityState.TargetIsClear)
                {
                    s.ClearStreak++;
                    s.BadStreak = 0;
                    if (s.Stage != ArcherStage.Idle && s.Stage != ArcherStage.Cooldown
                        && s.ClearStreak >= ArcherRepositionConfig.ClearStreakToExit)
                    {
                        s.RequestExit = true;
                    }
                    return;
                }
                s.ClearStreak = 0;

                if (s.Stage == ArcherStage.Cooldown && now < s.NextAllowedTime)
                    return;
                if (++s.BadStreak < ArcherRepositionConfig.DetectionStreak)
                    return;

                // 二次确认：目标有效性 / 距离在射程内（超射程不是遮挡问题，交给阵型层）
                Agent target = agent.GetTargetAgent();
                if (target == null || !target.IsActive() || !agent.IsEnemyOf(target))
                    return;
                float range = agent.GetMissileRange();
                if (range <= 0f)
                    return;
                float maxDistSq = range * range
                    * ArcherRepositionConfig.TargetRangeFactor * ArcherRepositionConfig.TargetRangeFactor;
                if (target.Position.DistanceSquared(agent.Position) > maxDistSq)
                {
                    s.BadStreak = 0;
                    return;
                }

                // 请求主线程处理（换目标 / 批准侧移）
                s.Dirty = true;

                // 侧移执行：覆写本周期 formation frame（与原方法同线程同上下文，有先例）
                if (s.Stage == ArcherStage.Strafe && s.StrafeApproved)
                {
                    if (now - s.StageStartTime > ArcherRepositionConfig.StrafeTimeout)
                    {
                        s.StrafeApproved = false;
                        s.RequestExit = true;
                        return;
                    }

                    // 侧移方向 = 垂直于"射手→目标"（不是垂直于阵型朝向，防朝向偏离时沿射轴移动）
                    Vec2 toTarget = (target.Position - agent.Position).AsVec2;
                    if (toTarget.LengthSquared < 0.01f)
                        return;
                    Vec2 lateral = toTarget.Normalized();
                    lateral = new Vec2(-lateral.y, lateral.x) * s.StrafeDir * s.StrafeOffset;

                    if (agent.GetBaseFormationFrame(out WorldPosition basePos, out Vec2 baseDir)
                        && basePos.IsValid)
                    {
                        WorldPosition moved = basePos;
                        moved.SetVec2MT(basePos.AsVec2 + lateral);   // MT 安全：内置 navmesh Z 重验证
                        if (moved.IsValid && !float.IsNaN(moved.GetNavMeshZMT()))
                            agent.TrySetFormationFrame(moved, baseDir);
                        else
                            s.StrafeApproved = false;                // 落点不可走 → 主线程换方向/换目标
                    }
                    else
                    {
                        s.StrafeApproved = false;                    // 引擎未启用 frame（如 Stop 语义）→ 让位
                    }
                }
            }
            catch (Exception ex)
            {
                // native worker 线程抛异常会中断 AgentTickMT 整个区块 —— 必须全包
                ArcherRepositionLogic.ReportFault(ex);
            }
        }

        private static bool ComputeIsRanged(Agent agent)
        {
            EquipmentIndex slot = agent.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None)
                return false;
            WeaponComponentData weapon = agent.Equipment[slot].CurrentUsageItem;
            return weapon != null && weapon.IsRangedWeapon;
        }
    }

    /// <summary>
    /// 补丁安装器。由 ArcherRepositionBehavior 构造函数（主线程、mission 初始化阶段）调用，
    /// 保证无论 SubModule 何时执行 PatchAll，本特性都能在进入战斗前完成打点。
    /// </summary>
    internal static class ArcherRepositionPatchInstaller
    {
        private static readonly object InstallLock = new object();
        private static Harmony _harmony;
        private static bool _installed;

        public static void EnsureInstalled()
        {
            if (_installed)
                return;
            lock (InstallLock)
            {
                if (_installed)
                    return;
                _harmony = new Harmony("New_ZZZF.ArcherReposition");
                _harmony.Patch(
                    AccessTools.Method(typeof(HumanAIComponent), nameof(HumanAIComponent.ParallelUpdateFormationMovement)),
                    postfix: new HarmonyMethod(typeof(ArcherRepositionDetectionPatch), nameof(ArcherRepositionDetectionPatch.Postfix)));
                _installed = true;
            }
        }

        /// <summary>特性卸载用（热移除/调试）：恢复全部补丁。</summary>
        public static void Uninstall()
        {
            lock (InstallLock)
            {
                _harmony?.UnpatchAll("New_ZZZF.ArcherReposition");
                _harmony = null;
                _installed = false;
            }
        }
    }
}
