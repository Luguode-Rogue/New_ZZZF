using System;
using System.IO;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.ArcherReposition
{
    /// <summary>
    /// 主线程决策逻辑。仅由 ArcherRepositionBehavior.OnMissionTick 驱动。
    ///
    /// 职责：
    /// 1. ManualTarget 生命周期自管（托管侧不清 target —— 必须自己恢复 SetAutomaticTargetSelection(true)）
    /// 2. 消费 Dirty 请求：优先换目标（射线验证），失败则批准侧移
    /// 3. 侧移升级（连续未恢复 → 加大幅度/换方向）
    /// 4. 加权射线预算（RayCastForClosestAgent=2 单位 > Scene 射线=1 单位）
    /// 5. 射线结果缓存（非对称 TTL：挡=0.5s / 通=0.1s）
    /// 6. 异常熔断：连续异常达到上限 → 自动禁用，行为回退原版
    /// </summary>
    internal static class ArcherRepositionLogic
    {
        /// <summary>熔断开关（volatile，检测 postfix 每次进入都会读）。</summary>
        public static volatile bool Disabled;

        private static int _faultCount;
        private static int _frameBudget;
        private static bool _resetPending;

        // ---------- 射线结果缓存（直接索引哈希，碰撞覆盖可接受） ----------

        private struct RayCacheEntry
        {
            public int Key;
            public float Time;
            public bool Blocked;
        }

        private static readonly RayCacheEntry[] RayCache = new RayCacheEntry[ArcherRepositionConfig.CacheSlots];

        // ---------- 主线程入口 ----------

        public static void TickMain(Mission? mission)
        {
            if (Disabled)
                return;

            if (mission == null || mission.MissionEnded || mission.IsMissionEnding)
            {
                if (!_resetPending)
                {
                    _resetPending = true;
                    ArcherRepositionStateStore.ResetAll();
                    Array.Clear(RayCache, 0, RayCache.Length);
                }
                return;
            }
            _resetPending = false;

            if (GameNetwork.IsClientOrReplay)
                return;

            float now = mission.CurrentTime;
            _frameBudget = ArcherRepositionConfig.MaxRayUnitsPerFrame;

            for (int i = 0; i < ArcherRepositionStateStore.Capacity; i++)
            {
                // 快速跳过：99% 的槽位零 native 调用
                ref ArcherAgentState s = ref ArcherRepositionStateStore.States[i];
                if (s.Stage == ArcherStage.Idle && !s.Dirty && !s.RequestExit)
                    continue;

                Agent agent = ArcherRepositionStateStore.Owners[i];
                if (agent == null || !agent.IsActive() || agent.Mission != mission)
                {
                    // 槽位失效（agent 已移除 / 战斗切换）—— 清理
                    s = default;
                    ArcherRepositionStateStore.Owners[i] = null;
                    continue;
                }

                // ---- ManualTarget 生命周期自管 ----
                if (s.Stage == ArcherStage.ManualTarget)
                {
                    bool restore = s.RequestExit;
                    if (!restore)
                    {
                        Agent target = agent.GetTargetAgent();
                        restore = target == null || !target.IsActive()
                            || agent.GetLastTargetVisibilityState() == AITargetVisibilityState.TargetIsClear;
                    }
                    if (!restore && s.Dirty && s.BadStreak >= ArcherRepositionConfig.DetectionStreak)
                    {
                        restore = true;   // 手动目标也被挡了 → 放弃手动指定，回到常规流程
                    }
                    if (restore)
                    {
                        SafeRestoreAutomaticTarget(agent);
                        EnterCooldown(ref s, now);
                    }
                    continue;
                }

                // ---- Strafe 收尾/升级 ----
                if (s.Stage == ArcherStage.Strafe)
                {
                    if (s.RequestExit)
                    {
                        EnterCooldown(ref s, now);
                        continue;
                    }
                    if (s.BadStreak >= ArcherRepositionConfig.StrafeEscalateStreak)
                    {
                        // 连续未恢复 → 加大幅度 / 换方向
                        TryApproveStrafe(mission, agent, ref s, now, escalate: true);
                    }
                    continue;
                }

                // ---- Idle / Cooldown：处理检测请求 ----
                if (!s.Dirty)
                    continue;
                s.Dirty = false;
                if (s.Stage == ArcherStage.Cooldown && now < s.NextAllowedTime)
                    continue;
                if (ArcherRepositionConfig.OnlyPlayerTeam && !IsPlayerSide(mission, agent))
                    continue;

                ProcessBlockedAgent(mission, agent, ref s, now);
            }
        }

        // ---------- 决策 ----------

        private static void ProcessBlockedAgent(Mission mission, Agent agent, ref ArcherAgentState s, float now)
        {
            // 1) 换目标（优先 —— 零移动成本）
            Team enemy = FindEnemyTeam(mission, agent.Team);
            if (enemy != null && enemy.ActiveAgents.Count > 0)
            {
                Agent candidate = FindClearTarget(mission, agent, enemy, ref s, now);
                if (candidate != null)
                {
                    SafeTakeManualTarget(agent, candidate);
                    s.Stage = ArcherStage.ManualTarget;
                    s.ManualTargetIndex = candidate.Index;
                    s.StageStartTime = now;
                    s.RequestExit = false;
                    Log($"t={now:F1} agent#{agent.Index} 换目标 -> #{candidate.Index}");
                    return;
                }
            }

            // 2) 侧移找射角
            if (_frameBudget > 0 && TryApproveStrafe(mission, agent, ref s, now, escalate: false))
                return;

            // 全部失败 → 冷却后重试
            EnterCooldown(ref s, now);
        }

        private static Team? FindEnemyTeam(Mission mission, Team? ownTeam)
        {
            if (ownTeam == null)
                return null;
            foreach (Team team in mission.Teams)
            {
                if (team != ownTeam && team.IsEnemyOf(ownTeam) && team.ActiveAgents.Count > 0)
                    return team;
            }
            return null;
        }

        private static Agent? FindClearTarget(Mission mission, Agent agent, Team enemy, ref ArcherAgentState s, float now)
        {
            MBReadOnlyList<Agent> list = enemy.ActiveAgents;
            int count = list.Count;
            if (count <= 0)
                return null;

            float range = agent.GetMissileRange() * ArcherRepositionConfig.TargetRangeFactor;
            float maxSq = range * range;
            float minSq = ArcherRepositionConfig.MinTargetDistance * ArcherRepositionConfig.MinTargetDistance;

            s.SamplePhase++;
            int seed = agent.Index * 31 + s.SamplePhase * 97;   // Index 哈希伪随机（System.Random 非线程安全且无需随机质量）

            for (int k = 0; k < ArcherRepositionConfig.TargetSampleCount; k++)
            {
                int idx = (seed + k * 257) % count;
                if (idx < 0)
                    idx += count;
                Agent candidate = list[idx];
                if (candidate == null || !candidate.IsActive() || candidate.IsMount || !candidate.IsEnemyOf(agent))
                    continue;
                float distSq = candidate.Position.DistanceSquared(agent.Position);
                if (distSq < minSq || distSq > maxSq)
                    continue;
                if (IsLineBlocked(mission, agent, candidate, now))
                    continue;
                return candidate;
            }
            return null;
        }

        /// <summary>批准侧移。返回 false 表示当前预算/几何条件下找不到可用偏移。</summary>
        private static bool TryApproveStrafe(Mission mission, Agent agent, ref ArcherAgentState s, float now, bool escalate)
        {
            Agent target = agent.GetTargetAgent();
            if (target == null || !target.IsActive())
                return false;

            Vec3 eye = agent.GetEyeGlobalPosition();
            Vec3 aim = target.CollisionCapsuleCenter;
            float dist = eye.Distance(aim);
            if (dist < ArcherRepositionConfig.MinStrafeDistance)
                return false;

            Vec2 toTarget = (aim - eye).AsVec2;
            if (toTarget.LengthSquared < 0.01f)
                return false;
            Vec2 lateral = toTarget.Normalized();
            lateral = new Vec2(-lateral.y, lateral.x);

            if (!agent.GetBaseFormationFrame(out WorldPosition basePos, out Vec2 baseDir) || !basePos.IsValid)
                return false;

            if (escalate)
            {
                float[] offsets = ArcherRepositionConfig.StrafeOffsets;
                if (s.StrafeOffset < offsets[offsets.Length - 1])
                    s.StrafeOffset = offsets[offsets.Length - 1];          // 升到最大幅度
                else
                    s.StrafeDir = (sbyte)(-s.StrafeDir);                   // 已最大 → 换方向
                s.BadStreak = 0;
            }

            sbyte primaryDir = s.StrafeDir == 0 ? (sbyte)1 : s.StrafeDir;

            foreach (sbyte dir in new sbyte[] { primaryDir, (sbyte)(-primaryDir) })
            {
                foreach (float offset in ArcherRepositionConfig.StrafeOffsets)
                {
                    if (_frameBudget <= 0)
                    {
                        s.Dirty = true;   // 预算耗尽 → 下一帧重试（自然退化）
                        return false;
                    }

                    Vec2 delta = lateral * dir * offset;
                    Vec3 eyeShifted = eye + new Vec3(delta.x, delta.y, 0f, -1f);

                    WorldPosition moved = basePos;
                    moved.SetVec2MT(basePos.AsVec2 + delta);
                    if (!moved.IsValid || float.IsNaN(moved.GetNavMeshZMT()))
                        continue;                                          // 落点不可走

                    if (IsLineBlockedInternal(mission, eyeShifted, aim, agent.Index, target.Index, now,
                            cacheKeyExtra: (int)(dir * 1000 + offset * 10)))
                        continue;

                    s.StrafeApproved = true;
                    s.StrafeDir = dir;
                    s.StrafeOffset = offset;
                    s.Stage = ArcherStage.Strafe;
                    s.StageStartTime = now;
                    s.RequestExit = false;
                    s.BadStreak = 0;
                    Log($"t={now:F1} agent#{agent.Index} 侧移批准 dir={dir} offset={offset:F1}m");
                    return true;
                }
            }
            return false;
        }

        // ---------- 射线 ----------

        private static bool IsLineBlocked(Mission mission, Agent shooter, Agent target, float now)
        {
            return IsLineBlockedInternal(mission,
                shooter.GetEyeGlobalPosition(), target.CollisionCapsuleCenter,
                shooter.Index, target.Index, now, cacheKeyExtra: 0);
        }

        private static bool IsLineBlockedInternal(Mission mission, Vec3 from, Vec3 to,
            int shooterIndex, int targetIndex, float now, int cacheKeyExtra)
        {
            int key = 0;
            if (ArcherRepositionConfig.CacheEnabled)
            {
                key = MakeCacheKey(from, to, cacheKeyExtra);
                int slot = key & (ArcherRepositionConfig.CacheSlots - 1);
                if (slot < 0)
                    slot += ArcherRepositionConfig.CacheSlots;
                ref RayCacheEntry entry = ref RayCache[slot];
                if (entry.Key == key)
                {
                    float ttl = entry.Blocked
                        ? ArcherRepositionConfig.CacheTtlBlocked
                        : ArcherRepositionConfig.CacheTtlClear;
                    if (now - entry.Time < ttl)
                        return entry.Blocked;
                }
            }

            bool blocked = false;
            float dist = from.Distance(to);

            // 1) agent 射线（Mission 级胶囊遍历，O(活跃agent)，最贵 → 2 单位）
            if (SpendBudget(ArcherRepositionConfig.AgentRayCost))
            {
                try
                {
                    Agent hit = mission.RayCastForClosestAgent(
                        from, to, shooterIndex, ArcherRepositionConfig.RayThickness, out float _);
                    if (hit != null && hit.Index != targetIndex)
                        blocked = true;                                    // 友军（或别的单位）挡在弹道上
                }
                catch (Exception ex)
                {
                    ReportFault(ex);
                }
            }

            // 2) 地形/杂物射线（物理 broadphase，较便宜 → 1 单位）
            if (!blocked && SpendBudget(ArcherRepositionConfig.SceneRayCost))
            {
                try
                {
                    bool hitWorld = mission.Scene.RayCastForClosestEntityOrTerrain(
                        from, to, out float collisionDist, out WeakGameEntity _,
                        ArcherRepositionConfig.RayThickness, BodyFlags.CommonCollisionExcludeFlagsForMissile);
                    if (hitWorld && collisionDist < dist - ArcherRepositionConfig.TerrainTolerance)
                        blocked = true;
                }
                catch (Exception ex)
                {
                    ReportFault(ex);
                }
            }

            if (ArcherRepositionConfig.CacheEnabled)
            {
                int slot = key & (ArcherRepositionConfig.CacheSlots - 1);
                if (slot < 0)
                    slot += ArcherRepositionConfig.CacheSlots;
                RayCache[slot] = new RayCacheEntry { Key = key, Time = now, Blocked = blocked };
            }
            return blocked;
        }

        private static int MakeCacheKey(Vec3 from, Vec3 to, int extra)
        {
            // 1m 量化 + 二元组(射手格, 目标格)
            unchecked
            {
                int k = 17;
                k = k * 31 + (int)from.x;
                k = k * 31 + (int)from.y;
                k = k * 31 + (int)to.x;
                k = k * 31 + (int)to.y;
                k = k * 31 + extra;
                return k;
            }
        }

        private static bool SpendBudget(int cost)
        {
            if (_frameBudget < cost)
                return false;
            _frameBudget -= cost;
            return true;
        }

        // ---------- 状态迁移 ----------

        private static void EnterCooldown(ref ArcherAgentState s, float now)
        {
            s.Stage = ArcherStage.Cooldown;
            s.NextAllowedTime = now + ArcherRepositionConfig.ExitCooldown;
            s.StrafeApproved = false;
            s.RequestExit = false;
            s.Dirty = false;
            s.BadStreak = 0;
            s.ClearStreak = 0;
        }

        private static void SafeRestoreAutomaticTarget(Agent agent)
        {
            try
            {
                agent.SetAutomaticTargetSelection(true);
            }
            catch (Exception ex)
            {
                ReportFault(ex);
            }
        }

        private static void SafeTakeManualTarget(Agent agent, Agent candidate)
        {
            try
            {
                agent.SetAutomaticTargetSelection(false);   // 官方模式：TaskForceDetachment.cs:95-96
                agent.SetTargetAgent(candidate);
            }
            catch (Exception ex)
            {
                ReportFault(ex);
            }
        }

        private static bool IsPlayerSide(Mission mission, Agent agent)
        {
            Team team = agent.Team;
            if (team == null)
                return false;
            if (team.IsPlayerTeam)
                return true;
            Team playerTeam = mission.PlayerTeam;
            return playerTeam != null && !playerTeam.IsEnemyOf(team);   // 含友方队伍
        }

        // ---------- 熔断与日志 ----------

        public static void ReportFault(Exception ex)
        {
            int count = Interlocked.Increment(ref _faultCount);
            Log("FAULT #" + count + " " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
            if (count == 1)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[ArcherReposition] 发生异常，详见 Logs/ArcherReposition.log", Colors.Red));
            }
            if (count >= ArcherRepositionConfig.MaxFaults)
            {
                Disabled = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    "[ArcherReposition] 连续异常已熔断禁用，行为回退原版。", Colors.Red));
            }
        }

        private static readonly object LogLock = new object();

        private static void Log(string message)
        {
            if (!ArcherRepositionConfig.DebugLog)
                return;
            try
            {
                lock (LogLock)
                {
                    string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                        Environment.CurrentDirectory, "../../Modules/New_ZZZF/Logs/ArcherReposition.log"));
                    string? dir = System.IO.Path.GetDirectoryName(path);
                    if (dir != null && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff ") + message + Environment.NewLine);
                }
            }
            catch
            {
                // 日志失败静默 —— 绝不影响战斗
            }
        }
    }
}
