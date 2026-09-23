using System;
using System.Collections.Generic;
using New_ZZZF.Systems;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    /// <summary>从施法者上方弧形阵列发射二十一枚追踪火焰飞弹。</summary>
    internal sealed class LiuXingFeiDan : SkillBase
    {
        private const int ProjectileCount = 21;
        private const float SearchRange = 50f;
        private const float ProjectileSpeed = 30f;
        private const float ProjectileLifetime = 7f;
        private const float ProjectileHitRadius = 0.45f;
        private const float WorldHitRadius = 0.15f;
        private const float BaseDamagePerProjectile = 20f;
        private const float TurnRateDegrees = 270f;
        private const float HomingDelay = 0.2f;
        private const float LogicInterval = 0.05f;
        private static readonly int[] LayerCounts = { 5, 7, 9 };
        private static readonly float[] LayerRadii = { 0.8f, 1.5f, 2.2f };
        private static readonly float[] LayerHeights = { 0.35f, 0.65f, 0.95f };

        private sealed class DamageSnapshot
        {
            public float SpellPowerCoefficient;
        }

        /// <summary>
        /// 同一次施法的二十一枚飞弹共用此上下文。
        /// 范围搜索最多每 0.25 秒执行一次，不会被每枚飞弹重复执行。
        /// </summary>
        private sealed class VolleyContext
        {
            private const float CandidateRefreshInterval = 0.25f;
            private const float MinimumViewDot = 0.15f;

            private readonly Agent _caster;
            private readonly MBList<Agent> _nearbyAgents = new MBList<Agent>();
            private readonly List<Agent> _collisionCandidates = new List<Agent>();
            private readonly List<Agent> _targetCandidates = new List<Agent>();
            private float _nextCollisionRefreshTime;
            private int _retargetCursor;

            public VolleyContext(Agent caster, Agent primaryTarget)
            {
                _caster = caster;
                RefreshCollisionCandidates(true);
                BuildTargetCandidates(primaryTarget);
            }

            public int TargetCount => _targetCandidates.Count;

            public Agent GetInitialTarget(int projectileIndex)
            {
                if (_targetCandidates.Count == 0)
                    return null;
                return _targetCandidates[projectileIndex % _targetCandidates.Count];
            }

            public Agent AcquireTarget()
            {
                RefreshCollisionCandidates(false);
                Agent target = FindNextActiveTarget();
                if (target != null)
                    return target;

                BuildTargetCandidates(null);
                return FindNextActiveTarget();
            }

            public IReadOnlyList<Agent> GetCollisionCandidates()
            {
                RefreshCollisionCandidates(false);
                return _collisionCandidates;
            }

            private Agent FindNextActiveTarget()
            {
                int count = _targetCandidates.Count;
                for (int i = 0; i < count; i++)
                {
                    int index = (_retargetCursor + i) % count;
                    Agent candidate = _targetCandidates[index];
                    if (!IsValidEnemy(candidate))
                        continue;
                    _retargetCursor = (index + 1) % count;
                    return candidate;
                }
                return null;
            }

            private void RefreshCollisionCandidates(bool force)
            {
                Mission mission = Mission.Current;
                if (mission == null)
                    return;
                if (!force && mission.CurrentTime < _nextCollisionRefreshTime)
                    return;
                _nextCollisionRefreshTime = mission.CurrentTime + CandidateRefreshInterval;

                _nearbyAgents.Clear();
                if (_caster.Team != null)
                    mission.GetNearbyEnemyAgents(
                        _caster.Position.AsVec2, SearchRange + 5f, _caster.Team, _nearbyAgents);
                else
                    mission.GetNearbyAgents(_caster.Position.AsVec2, SearchRange + 5f, _nearbyAgents);

                _collisionCandidates.Clear();
                foreach (Agent candidate in _nearbyAgents)
                {
                    if (IsValidEnemy(candidate))
                        _collisionCandidates.Add(candidate);
                }
            }

            private void BuildTargetCandidates(Agent primaryTarget)
            {
                _targetCandidates.Clear();
                if (IsValidEnemy(primaryTarget))
                    _targetCandidates.Add(primaryTarget);

                Vec3 viewDirection = _caster.LookDirection;
                if (viewDirection.LengthSquared < 0.001f)
                    viewDirection = Vec3.Forward;
                viewDirection.Normalize();

                for (int i = 0; i < _collisionCandidates.Count; i++)
                {
                    Agent candidate = _collisionCandidates[i];
                    if (candidate == primaryTarget || !IsValidEnemy(candidate))
                        continue;
                    Vec3 direction = candidate.GetEyeGlobalPosition() - _caster.GetEyeGlobalPosition();
                    if (direction.LengthSquared < 0.001f)
                        continue;
                    direction.Normalize();
                    if (Vec3.DotProduct(viewDirection, direction) < MinimumViewDot ||
                        !RushMovementMissionLogic.HasLineOfSight(_caster, candidate))
                        continue;
                    _targetCandidates.Add(candidate);
                }

                _targetCandidates.Sort((left, right) =>
                    (_caster.Position - left.Position).LengthSquared.CompareTo(
                        (_caster.Position - right.Position).LengthSquared));

                // 保证当前锁定目标仍是分配队列的第一个。
                if (primaryTarget != null)
                {
                    int primaryIndex = _targetCandidates.IndexOf(primaryTarget);
                    if (primaryIndex > 0)
                    {
                        _targetCandidates.RemoveAt(primaryIndex);
                        _targetCandidates.Insert(0, primaryTarget);
                    }
                }
                _retargetCursor = 0;
            }

            private bool IsValidEnemy(Agent target)
            {
                return target != null && target != _caster && target.IsActive() && target.IsHuman &&
                    target.Health > 0f && _caster.IsEnemyOf(target) &&
                    (target.Position - _caster.Position).LengthSquared <=
                    (SearchRange + 5f) * (SearchRange + 5f);
            }
        }

        public LiuXingFeiDan()
        {
            SkillID = "LiuXingFeiDan";
            Type = SPSkillType.Spell;
            Cooldown = 10f;
            ResourceCost = 30f;
            Text = new TextObject("{=ZZZF_LiuXingFeiDan}流星飞弹");
            Description = new TextObject(
                "{=ZZZF_LiuXingFeiDan_DESC}在施法者上方展开二十一枚流星飞弹，飞弹优先均匀追踪视野内的敌人，目标失效时会重新索敌。每枚命中造成20点基础火焰魔法伤害，并乘以施法者的技能法强系数。消耗法力：30。冷却时间：10秒。");
            Difficulty = null;
        }

        public override bool Activate(Agent caster)
        {
            if (caster == null || !caster.IsActive())
                return FailActivation("施法者当前不可用。");

            SpellProjectileMissionLogic manager = SpellProjectileMissionLogic.GetForCurrentMission();
            if (manager == null)
                return FailActivation("当前任务未加载法术投射物管理器。");

            if (!SpellTargetingSystem.TryResolveSingleTarget(
                    caster, SearchRange, out SpellTargetingSystem.Result targeting) ||
                targeting.Target == null)
                return FailActivation("视野内没有可用目标。");

            VolleyContext volley = new VolleyContext(caster, targeting.Target);
            if (volley.TargetCount == 0)
                return FailActivation("视野内没有可用目标。");

            float spellPower = MagicDamageSystem.GetSpellPowerCoefficient(caster);
            DamageSnapshot damageSnapshot = new DamageSnapshot { SpellPowerCoefficient = spellPower };
            Func<Agent> acquireTarget = volley.AcquireTarget;
            Func<IReadOnlyList<Agent>> collisionCandidatesProvider = volley.GetCollisionCandidates;
            int spawnedCount = 0;
            int projectileIndex = 0;

            Vec3 eyePosition = caster.GetEyeGlobalPosition();
            Vec3 forward = caster.LookDirection;
            forward.z = 0f;
            if (forward.LengthSquared < 0.001f)
                forward = Vec3.Forward;
            forward.Normalize();
            Vec3 right = Vec3.CrossProduct(forward, Vec3.Up);
            if (right.LengthSquared < 0.001f)
                right = Vec3.Side;
            right.Normalize();

            for (int layer = 0; layer < LayerCounts.Length; layer++)
            {
                int count = LayerCounts[layer];
                float radius = LayerRadii[layer];
                for (int i = 0; i < count; i++)
                {
                    float angle = count == 1 ? MathF.PI * 0.5f : MathF.PI * i / (count - 1);
                    Vec3 offset = right * ((float)Math.Cos(angle) * radius) +
                        Vec3.Up * (LayerHeights[layer] + (float)Math.Sin(angle) * radius) +
                        forward * 0.25f;
                    Vec3 initialDirection = offset + forward * 0.75f;
                    if (initialDirection.LengthSquared < 0.001f)
                        initialDirection = forward;
                    initialDirection.Normalize();

                    SpellProjectileRequest request = new SpellProjectileRequest
                    {
                        Caster = caster,
                        StartPosition = eyePosition + offset,
                        Direction = initialDirection,
                        Speed = ProjectileSpeed,
                        Lifetime = ProjectileLifetime,
                        HitRadius = ProjectileHitRadius,
                        WorldHitRadius = WorldHitRadius,
                        CollisionInterval = LogicInterval,
                        PrefabResourceName = "mangonel_mapicon_projectile",
                        ParticleSystemName = null,
                        HitHumanAgentsOnly = true,
                        HitEnemiesOnly = true,
                        InvokeImpactWhenLifetimeExpires = false,
                        TargetAgent = volley.GetInitialTarget(projectileIndex),
                        AcquireTarget = acquireTarget,
                        HomingTurnRateDegrees = TurnRateDegrees,
                        HomingInterval = LogicInterval,
                        HomingDelay = HomingDelay,
                        CollisionCandidatesProvider = collisionCandidatesProvider,
                        OnImpact = OnProjectileImpact,
                        Payload = damageSnapshot
                    };

                    if (manager.TrySpawn(request, out _))
                        spawnedCount++;
                    projectileIndex++;
                }
            }

            if (spawnedCount == 0)
                return FailActivation("流星飞弹生成失败。");

            MagicShoot.PlayReleasePresentation(caster);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) ||
                SpellProjectileMissionLogic.GetForCurrentMission() == null)
                return false;

            Agent target = caster.GetTargetAgent();
            if (target == null || !target.IsActive() || target.Health <= 0f ||
                !caster.IsEnemyOf(target))
                return false;
            float distanceSquared = (target.Position - caster.Position).LengthSquared;
            return distanceSquared >= 64f && distanceSquared <= SearchRange * SearchRange &&
                RushMovementMissionLogic.HasLineOfSight(caster, target);
        }

        private static void OnProjectileImpact(SpellProjectileImpact impact)
        {
            if (impact == null)
                return;

            SpellProjectileMissionLogic.GetForCurrentMission()?.SpawnTimedParticle(
                "psys_battleground_env_fire", impact.Position, 0.5f);
            if (impact.Caster == null || impact.DirectTarget == null ||
                !impact.DirectTarget.IsActive())
                return;

            DamageSnapshot snapshot = impact.Payload as DamageSnapshot;
            float spellPower = snapshot == null
                ? MagicDamageSystem.GetSpellPowerCoefficient(impact.Caster)
                : snapshot.SpellPowerCoefficient;
            MagicDamageSystem.Apply(
                impact.Caster,
                impact.DirectTarget,
                BaseDamagePerProjectile,
                spellPower,
                DamageType.FIRE_DAMAGE,
                MagicDamageFlags.None,
                impact.Position);
        }
    }
}
