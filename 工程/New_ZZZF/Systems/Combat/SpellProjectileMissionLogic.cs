using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    public enum SpellProjectileImpactReason
    {
        Agent,
        World,
        LifetimeExpired
    }

    /// <summary>投射物结束飞行时交给技能的只读命中信息。</summary>
    public sealed class SpellProjectileImpact
    {
        public Agent Caster { get; internal set; }
        public Agent DirectTarget { get; internal set; }
        public Vec3 Position { get; internal set; }
        public SpellProjectileImpactReason Reason { get; internal set; }
        /// <summary>技能自定义的施法快照，例如法强、层数或组合后的法术参数。</summary>
        public object Payload { get; internal set; }
    }

    /// <summary>
    /// 通用直线法术投射物请求。技能负责填写表现、飞行参数和命中回调，
    /// 管理器统一承担实体生命周期、连续碰撞检测和任务结束清理。
    /// </summary>
    public sealed class SpellProjectileRequest
    {
        public Agent Caster { get; set; }
        public Vec3 StartPosition { get; set; }
        public Vec3 Direction { get; set; }
        public float Speed { get; set; } = 25f;
        public float Lifetime { get; set; } = 4f;
        public float HitRadius { get; set; } = 0.75f;
        /// <summary>跟随投射物移动的可视网格资源；为空时只保留逻辑弹道。</summary>
        public string MeshResourceName { get; set; }
        public string ParticleSystemName { get; set; }
        public bool HitHumanAgentsOnly { get; set; } = true;
        public bool HitEnemiesOnly { get; set; } = true;
        public bool InvokeImpactWhenLifetimeExpires { get; set; } = true;
        public Func<Agent, bool> AdditionalAgentFilter { get; set; }
        public Action<SpellProjectileImpact> OnImpact { get; set; }
        public object Payload { get; set; }
    }

    /// <summary>
    /// 全法术共用的直线投射物管理器。新增法术只提交 SpellProjectileRequest，
    /// 禁止再为单个法术创建独立 MissionLogic。
    /// </summary>
    public sealed class SpellProjectileMissionLogic : MissionLogic
    {
        private sealed class ProjectileRecord
        {
            public GameEntity Entity;
            public SpellProjectileRequest Request;
            public Vec3 Direction;
            public float RemainingLifetime;
        }

        private sealed class TimedEffect
        {
            public GameEntity Entity;
            public float RemainingLifetime;
        }

        private readonly List<ProjectileRecord> _projectiles = new List<ProjectileRecord>();
        private readonly List<TimedEffect> _effects = new List<TimedEffect>();
        private readonly MBList<Agent> _nearbyAgents = new MBList<Agent>();

        public static SpellProjectileMissionLogic Current { get; private set; }
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Logic;

        public override void OnCreated()
        {
            base.OnCreated();
            Current = this;
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Current = this;
        }

        public static SpellProjectileMissionLogic GetForCurrentMission()
        {
            Mission mission = Mission.Current;
            if (mission == null)
                return null;
            if (Current != null && ReferenceEquals(Current.Mission, mission))
                return Current;
            Current = mission.GetMissionBehavior<SpellProjectileMissionLogic>();
            return Current;
        }

        public bool TrySpawn(SpellProjectileRequest request, out string failureReason)
        {
            failureReason = null;
            if (request?.Caster == null || !request.Caster.IsActive() || Mission?.Scene == null)
            {
                failureReason = "施法者或当前场景不可用。";
                return false;
            }
            if (!request.StartPosition.IsValid || request.Direction.LengthSquared < 0.001f)
            {
                failureReason = "投射物起点或方向无效。";
                return false;
            }
            if (request.Speed <= 0f || request.Lifetime <= 0f || request.HitRadius <= 0f)
            {
                failureReason = "投射物飞行参数无效。";
                return false;
            }

            Vec3 direction = request.Direction.NormalizedCopy();
            GameEntity entity = GameEntity.CreateEmpty(Mission.Scene);
            if (!string.IsNullOrEmpty(request.MeshResourceName))
            {
                Mesh mesh = Mesh.GetFromResource(request.MeshResourceName);
                if (mesh != null)
                    entity.AddMesh(mesh, true);
            }
            if (!string.IsNullOrEmpty(request.ParticleSystemName))
                entity.AddParticleSystemComponent(request.ParticleSystemName);
            entity.SetGlobalFrame(
                new MatrixFrame(Mat3.CreateMat3WithForward(direction), request.StartPosition));

            _projectiles.Add(new ProjectileRecord
            {
                Entity = entity,
                Request = request,
                Direction = direction,
                RemainingLifetime = request.Lifetime
            });
            return true;
        }

        /// <summary>生成由统一管理器负责回收的一次性粒子表现。</summary>
        public void SpawnTimedParticle(string particleSystemName, Vec3 position, float lifetime)
        {
            if (Mission?.Scene == null || string.IsNullOrEmpty(particleSystemName) || lifetime <= 0f)
                return;
            GameEntity effect = GameEntity.CreateEmpty(Mission.Scene);
            effect.SetLocalPosition(position);
            effect.AddParticleSystemComponent(particleSystemName);
            _effects.Add(new TimedEffect { Entity = effect, RemainingLifetime = lifetime });
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            TickEffects(dt);
            if (dt <= 0f || Mission?.Scene == null)
                return;

            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                ProjectileRecord record = _projectiles[i];
                record.RemainingLifetime -= dt;
                Vec3 start = record.Entity.GlobalPosition;
                Vec3 end = start + record.Direction * (record.Request.Speed * dt);

                Agent hitAgent = FindFirstAgentOnSegment(
                    record.Request, start, end, out float agentDistance);
                bool hitWorld = Mission.Scene.RayCastForClosestEntityOrTerrain(
                    start, end, out float worldDistance, 0.02f,
                    BodyFlags.CommonCollisionExcludeFlags);

                if (hitAgent != null && (!hitWorld || agentDistance <= worldDistance))
                {
                    InvokeImpact(record, hitAgent.Position + Vec3.Up,
                        hitAgent, SpellProjectileImpactReason.Agent);
                    RemoveProjectileAt(i);
                    continue;
                }
                if (hitWorld)
                {
                    InvokeImpact(record, start + record.Direction * worldDistance,
                        null, SpellProjectileImpactReason.World);
                    RemoveProjectileAt(i);
                    continue;
                }
                if (record.RemainingLifetime <= 0f)
                {
                    if (record.Request.InvokeImpactWhenLifetimeExpires)
                        InvokeImpact(record, end, null, SpellProjectileImpactReason.LifetimeExpired);
                    RemoveProjectileAt(i);
                    continue;
                }

                record.Entity.SetGlobalFrame(
                    new MatrixFrame(Mat3.CreateMat3WithForward(record.Direction), end));
            }
        }

        private Agent FindFirstAgentOnSegment(
            SpellProjectileRequest request,
            Vec3 start,
            Vec3 end,
            out float distanceAlongSegment)
        {
            distanceAlongSegment = float.MaxValue;
            Agent closest = null;
            Vec3 segment = end - start;
            float lengthSquared = segment.LengthSquared;
            if (lengthSquared <= 0.0001f)
                return null;

            Vec3 midpoint = (start + end) * 0.5f;
            float queryRadius = segment.Length * 0.5f + request.HitRadius;
            FillNearbyAgents(request, midpoint, queryRadius);
            foreach (Agent candidate in _nearbyAgents)
            {
                if (!CanHitAgent(request, candidate))
                    continue;
                Vec3 targetPoint = candidate.Position + Vec3.Up;
                float t = Vec3.DotProduct(targetPoint - start, segment) / lengthSquared;
                if (t < 0f || t > 1f)
                    continue;
                Vec3 nearest = start + segment * t;
                if ((targetPoint - nearest).LengthSquared > request.HitRadius * request.HitRadius)
                    continue;
                float along = (nearest - start).Length;
                if (along < distanceAlongSegment)
                {
                    distanceAlongSegment = along;
                    closest = candidate;
                }
            }
            return closest;
        }

        private void FillNearbyAgents(SpellProjectileRequest request, Vec3 center, float radius)
        {
            if (request.HitEnemiesOnly && request.Caster.Team != null)
                Mission.GetNearbyEnemyAgents(center.AsVec2, radius, request.Caster.Team, _nearbyAgents);
            else
                Mission.GetNearbyAgents(center.AsVec2, radius, _nearbyAgents);
        }

        private static bool CanHitAgent(SpellProjectileRequest request, Agent candidate)
        {
            if (candidate == null || candidate == request.Caster || !candidate.IsActive())
                return false;
            if (request.HitHumanAgentsOnly && !candidate.IsHuman)
                return false;
            if (request.HitEnemiesOnly && !request.Caster.IsEnemyOf(candidate))
                return false;
            return request.AdditionalAgentFilter == null || request.AdditionalAgentFilter(candidate);
        }

        private static void InvokeImpact(
            ProjectileRecord record,
            Vec3 position,
            Agent directTarget,
            SpellProjectileImpactReason reason)
        {
            if (record.Request.OnImpact == null)
                return;
            SpellProjectileImpact impact = new SpellProjectileImpact
            {
                Caster = record.Request.Caster,
                DirectTarget = directTarget,
                Position = position,
                Reason = reason,
                Payload = record.Request.Payload
            };
            try
            {
                record.Request.OnImpact(impact);
            }
            catch (Exception ex)
            {
                Debug.Print("[New_ZZZF][法术投射物] 命中回调异常: " + ex);
            }
        }

        private void TickEffects(float dt)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                _effects[i].RemainingLifetime -= dt;
                if (_effects[i].RemainingLifetime > 0f)
                    continue;
                _effects[i].Entity?.Remove(1);
                _effects.RemoveAt(i);
            }
        }

        private void RemoveProjectileAt(int index)
        {
            _projectiles[index].Entity?.Remove(1);
            _projectiles.RemoveAt(index);
        }

        protected override void OnEndMission()
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
                _projectiles[i].Entity?.Remove(1);
            for (int i = _effects.Count - 1; i >= 0; i--)
                _effects[i].Entity?.Remove(1);
            _projectiles.Clear();
            _effects.Clear();
            if (ReferenceEquals(Current, this))
                Current = null;
            base.OnEndMission();
        }
    }
}
