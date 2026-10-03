using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF
{
    /// <summary>
    /// 玩家法术的统一选点系统。按住游戏键24（现有 Shift/缩放指示器使用的同一键）
    /// 时使用摄像机视线落点；未按住时，仅在技能发动瞬间搜索视野内可见敌人。
    /// 本系统不在 MissionTick 中做目标扫描，避免重复的距离与视线判定。
    /// </summary>
    public static class SpellTargetingSystem
    {
        private const float MinimumViewDot = 0.15f;

        public struct Result
        {
            public Vec3 Position;
            public Agent Target;
            public bool UsesManualIndicator;
        }

        public static bool IsManualIndicatorHeld(Agent caster)
        {
            if (caster == null || !caster.IsMainAgent)
                return false;
            MissionScreen screen = ScreenManager.TopScreen as MissionScreen;
            return screen != null && screen.SceneLayer.Input.IsGameKeyDown(24);
        }

        /// <summary>单体/投射物法术：指示模式返回落点，快速施法返回最靠近视野中心的可见敌人。</summary>
        public static bool TryResolveSingleTarget(
            Agent caster,
            float maximumRange,
            out Result result)
        {
            result = default;
            if (!CanResolve(caster))
                return false;

            if (IsManualIndicatorHeld(caster))
            {
                Vec3 point = Script.CameraLookPos();
                if (!point.IsValid)
                    return false;
                // 指示施法必须严格使用已显示的视线落点，不在这里暗中截短距离。
                result.Position = point;
                // 需要具体 Agent 的技能（如冲刺斩）可使用指示点附近的最近敌人；
                // 只需要位置的投射物仍以 Position 为准。
                result.Target = FindNearestEnemyToPoint(caster, point, 5f);
                result.UsesManualIndicator = true;
                return true;
            }

            Agent currentTarget = caster.IsPlayerControlled ? null : caster.GetTargetAgent();
            if (IsValidVisibleEnemy(caster, currentTarget, maximumRange))
            {
                result.Target = currentTarget;
                result.Position = currentTarget.GetEyeGlobalPosition();
                return true;
            }

            Vec3 viewDirection = GetViewDirection(caster);
            float bestDot = MinimumViewDot;
            float bestDistanceSquared = float.MaxValue;
            foreach (Agent candidate in Mission.Current.Agents)
            {
                if (!IsValidVisibleEnemy(caster, candidate, maximumRange))
                    continue;
                Vec3 direction = candidate.GetEyeGlobalPosition() - caster.GetEyeGlobalPosition();
                float distanceSquared = direction.LengthSquared;
                if (distanceSquared < 0.001f)
                    continue;
                direction.Normalize();
                float dot = Vec3.DotProduct(viewDirection, direction);
                if (dot < MinimumViewDot ||
                    (dot < bestDot + 0.001f && distanceSquared >= bestDistanceSquared))
                    continue;
                bestDot = dot;
                bestDistanceSquared = distanceSquared;
                result.Target = candidate;
            }

            if (result.Target == null)
                return false;
            result.Position = result.Target.GetEyeGlobalPosition();
            return true;
        }

        /// <summary>
        /// AOE法术：指示模式使用视线落点；快速施法在可见敌人中选择指定半径内
        /// 聚集敌人最多的候选位置。扫描只在发动瞬间执行。
        /// </summary>
        public static bool TryResolveAreaTarget(
            Agent caster,
            float maximumRange,
            float clusterRadius,
            out Result result, bool requireLineOfSight = true)
        {
            result = default;
            if (!CanResolve(caster))
                return false;

            if (IsManualIndicatorHeld(caster))
            {
                Vec3 point = Script.CameraLookPos();
                if (!point.IsValid)
                    return false;
                result.Position = point;
                result.UsesManualIndicator = true;
                return true;
            }

            List<Agent> visibleEnemies = new List<Agent>();
            foreach (Agent candidate in Mission.Current.Agents)
            {
                if (IsValidVisibleEnemy(caster, candidate, maximumRange, requireLineOfSight) &&
                    IsInsideView(caster, candidate))
                    visibleEnemies.Add(candidate);
            }
            if (visibleEnemies.Count == 0)
                return false;

            float radiusSquared = clusterRadius * clusterRadius;
            int bestCount = 0;
            float bestDistanceSquared = float.MaxValue;
            Agent best = null;
            for (int i = 0; i < visibleEnemies.Count; i++)
            {
                Agent candidate = visibleEnemies[i];
                int count = 0;
                for (int j = 0; j < visibleEnemies.Count; j++)
                {
                    if ((visibleEnemies[j].Position - candidate.Position).LengthSquared <= radiusSquared)
                        count++;
                }
                float distanceSquared = (candidate.Position - caster.Position).LengthSquared;
                if (count > bestCount || (count == bestCount && distanceSquared < bestDistanceSquared))
                {
                    bestCount = count;
                    bestDistanceSquared = distanceSquared;
                    best = candidate;
                }
            }

            result.Target = best;
            result.Position = best.Position;
            return true;
        }

        private sealed class DenseCell
        {
            internal Vec3 Sum;
            internal int Count, X, Y;
            internal Vec3 Center => Sum / Count;
        }
        private static long DenseCellKey(int x, int y) => ((long)x << 32) | (uint)y;
        /// <summary>空间分桶估算敌群密度，仅在施法时执行；适合大规模 NPC 范围技能。</summary>
        public static bool TryResolveDenseAreaTarget(Agent caster, float maximumRange, float radius, out Result result)
        {
            result = default;
            if (!CanResolve(caster) || radius <= 0f || maximumRange <= 0f) return false;
            if (IsManualIndicatorHeld(caster)) {
                Vec3 point = Script.CameraLookPos();
                if (!point.IsValid || (point - caster.Position).LengthSquared > maximumRange * maximumRange) return false;
                var targets = new MBList<Agent>();
                Mission.Current.GetNearbyAgents(point.AsVec2, radius, targets);
                foreach (Agent target in targets)
                    if (target != null && target.IsHuman && IsValidVisibleEnemy(caster, target, maximumRange, false) &&
                        (target.Position - point).LengthSquared <= radius * radius) {
                        result = new Result { Position = point, Target = target, UsesManualIndicator = true }; return true;
                    }
                return false;
            }
            float cellSize = radius * 0.5f;
            var buckets = new Dictionary<long, DenseCell>();
            var nearby = new MBList<Agent>();
            bool player = caster.IsPlayerControlled;
            Vec3 eye = caster.GetEyeGlobalPosition();
            Vec3 view = player ? GetViewDirection(caster) : Vec3.Zero;
            if (float.IsPositiveInfinity(maximumRange)) nearby.AddRange(Mission.Current.Agents);
            else if (caster.Team != null) Mission.Current.GetNearbyEnemyAgents(caster.Position.AsVec2, maximumRange, caster.Team, nearby);
            else Mission.Current.GetNearbyAgents(caster.Position.AsVec2, maximumRange, nearby);
            foreach (Agent target in nearby) {
                if (target == null || !target.IsHuman || !IsValidVisibleEnemy(caster, target, maximumRange, false)) continue;
                Vec3 toward = target.GetEyeGlobalPosition() - eye;
                if (player && toward.LengthSquared > 0.001f &&
                    Vec3.DotProduct(view, toward.NormalizedCopy()) < MinimumViewDot) continue;
                int x = (int)System.Math.Floor(target.Position.x / cellSize);
                int y = (int)System.Math.Floor(target.Position.y / cellSize);
                long key = DenseCellKey(x, y);
                if (!buckets.TryGetValue(key, out DenseCell cell)) buckets[key] = cell = new DenseCell { X = x, Y = y };
                cell.Sum += target.Position; cell.Count++;
            }
            int bestScore = 0;
            float bestDistance = float.MaxValue;
            foreach (var candidate in buckets) {
                Vec3 center = candidate.Value.Center;
                int score = 0;
                for (int x = -3; x <= 3; x++)
                    for (int y = -3; y <= 3; y++)
                        if (buckets.TryGetValue(DenseCellKey(candidate.Value.X + x, candidate.Value.Y + y), out DenseCell neighbor) &&
                            (neighbor.Center - center).LengthSquared <= radius * radius) score += neighbor.Count;
                float distance = (center - caster.Position).LengthSquared;
                if (score > bestScore || score == bestScore && distance < bestDistance) {
                    bestScore = score; bestDistance = distance; result.Position = center;
                }
            }
            return bestScore > 0;
        }


        /// <summary>只在已按统一指示键时查询指示点附近敌人；附加筛选由具体技能提供。</summary>
        public static bool TryResolveEnemyNearIndicator(Agent caster, float radius, out Agent target,
            System.Func<Agent, bool> extraFilter = null)
        {
            target = null;
            if (!CanResolve(caster) || !IsManualIndicatorHeld(caster)) return false;
            Vec3 point = Script.CameraLookPos();
            if (!point.IsValid) return false;
            var nearby = new MBList<Agent>();
            Mission.Current.GetNearbyAgents(point.AsVec2, radius, nearby);
            float best = radius * radius;
            foreach (Agent candidate in nearby) {
                if (!IsValidVisibleEnemy(caster, candidate, float.PositiveInfinity, false) ||
                    extraFilter != null && !extraFilter(candidate)) continue;
                float distance = (candidate.Position - point).LengthSquared;
                if (distance <= best) { best = distance; target = candidate; }
            }
            return target != null;
        }

        private static bool CanResolve(Agent caster)
        {
            return caster != null && caster.IsActive() && Mission.Current != null;
        }

        private static bool IsValidVisibleEnemy(Agent caster, Agent target, float maximumRange, bool requireLineOfSight = true)
        {
            if (target == null || target == caster || !target.IsActive() ||
                target.Health <= 0f || SkillTargetProtection.IsProtected(target) || !caster.IsEnemyOf(target))
                return false;
            if ((target.Position - caster.Position).LengthSquared > maximumRange * maximumRange)
                return false;
            return !requireLineOfSight || RushMovementMissionLogic.HasLineOfSight(caster, target);
        }

        private static bool IsInsideView(Agent caster, Agent target)
        {
            Vec3 direction = target.GetEyeGlobalPosition() - caster.GetEyeGlobalPosition();
            if (direction.LengthSquared < 0.001f)
                return true;
            direction.Normalize();
            return Vec3.DotProduct(GetViewDirection(caster), direction) >= MinimumViewDot;
        }

        private static Vec3 GetViewDirection(Agent caster)
        {
            MissionScreen screen = ScreenManager.TopScreen as MissionScreen;
            Vec3 direction = caster.IsMainAgent && screen != null
                ? screen.CombatCamera.Direction
                : caster.LookDirection;
            if (direction.LengthSquared < 0.001f)
                direction = caster.LookDirection;
            direction.Normalize();
            return direction;
        }

        private static Agent FindNearestEnemyToPoint(Agent caster, Vec3 point, float radius)
        {
            float bestDistanceSquared = radius * radius;
            Agent best = null;
            foreach (Agent candidate in Mission.Current.Agents)
            {
                if (candidate == null || candidate == caster || !candidate.IsActive() ||
                    candidate.Health <= 0f || SkillTargetProtection.IsProtected(candidate) || !caster.IsEnemyOf(candidate))
                    continue;
                float distanceSquared = (candidate.Position - point).LengthSquared;
                if (distanceSquared > bestDistanceSquared)
                    continue;
                bestDistanceSquared = distanceSquared;
                best = candidate;
            }
            return best;
        }

    }
}
