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
        public object Payload { get; internal set; }
    }

    /// <summary>
    /// 通用法术投射物请求。直线弹与追踪弹共用同一移动、胶囊碰撞、
    /// 场景碰撞和生命周期实现，技能只提供参数与命中回调。
    /// </summary>
    public sealed class SpellProjectileRequest
    {
        public Agent Caster { get; set; }
        public Vec3 StartPosition { get; set; }
        public Vec3 Direction { get; set; }
        public float Speed { get; set; } = 25f;
        public float Lifetime { get; set; } = 4f;

        /// <summary>投射物自身半径；与 Agent.CollisionCapsule.Radius 叠加后判定单位命中。</summary>
        public float HitRadius { get; set; } = 0.75f;
        /// <summary>场景扫掠射线厚度，与单位命中半径独立调整。</summary>
        public float WorldHitRadius { get; set; } = 0.05f;
        /// <summary>连续线段碰撞的检测周期。降频不会丢失中间飞行路径。</summary>
        public float CollisionInterval { get; set; } = 0.05f;
        /// <summary>穿透单位并将每段实际飞行路径交给技能结算；其他投射物保持首次命中即结束。</summary>
        public bool PiercesAgents { get; set; }
        public float MaxTravelDistance { get; set; }
        public Action<Vec3, Vec3> OnTravelSegment { get; set; }

        public string MeshResourceName { get; set; }
        /// <summary>可选的完整预制体；用于保留旧法术原有的模型外观。</summary>
        public string PrefabResourceName { get; set; }
        public string ParticleSystemName { get; set; }
        public bool HitHumanAgentsOnly { get; set; } = true;
        public bool HitEnemiesOnly { get; set; } = true;
        public bool InvokeImpactWhenLifetimeExpires { get; set; } = true;
        public Func<Agent, bool> AdditionalAgentFilter { get; set; }

        /// <summary>追踪目标；为空或失效时才调用 AcquireTarget，不每帧搜索。</summary>
        public Agent TargetAgent { get; set; }
        public Func<Agent> AcquireTarget { get; set; }
        public float HomingTurnRateDegrees { get; set; }
        public float HomingInterval { get; set; } = 0.05f;
        public float HomingDelay { get; set; }

        /// <summary>
        /// 可选的共享候选集。多弹技能可让整批弹体共用一次范围查询，
        /// 避免每枚弹体分别调用 Mission.GetNearbyAgents。
        /// </summary>
        public Func<IReadOnlyList<Agent>> CollisionCandidatesProvider { get; set; }

        public Action<SpellProjectileImpact> OnImpact { get; set; }
        public object Payload { get; set; }
    }

    /// <summary>全法术共用的投射物管理器。</summary>
    public sealed class SpellProjectileMissionLogic : MissionLogic
    {
        private sealed class ProjectileRecord
        {
            public GameEntity Entity;
            public SpellProjectileRequest Request;
            public Vec3 Direction;
            public Vec3 LastCollisionPosition;
            public float RemainingLifetime;
            public float CollisionTimer;
            public float HomingTimer;
            public float HomingDelayRemaining;
            public float TravelledDistance;
        }

        private sealed class TimedEffect
        {
            public GameEntity Entity;
            public float RemainingLifetime;
            public Vec3 Origin;
            public Vec3 ExpansionOffset;
            public float ExpansionDuration;
            public float Elapsed;
        }

        private struct PendingSound
        {
            public string EventName;
            public Vec3 Position;
        }

        private readonly List<ProjectileRecord> _projectiles = new List<ProjectileRecord>();
        private readonly List<TimedEffect> _effects = new List<TimedEffect>();
        private readonly List<PendingSound> _pendingSounds = new List<PendingSound>();
        private readonly MBList<Agent> _nearbyAgents = new MBList<Agent>();
        // 仅同一个 MissionTick 内复用；下一帧重新读取，避免命中使用过期胶囊。
        private readonly Dictionary<Agent, CapsuleData> _frameCapsules = new Dictionary<Agent, CapsuleData>();
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
            if (request.Speed <= 0f || request.Lifetime <= 0f || request.HitRadius <= 0f ||
                request.WorldHitRadius < 0f || request.CollisionInterval <= 0f ||
                request.MaxTravelDistance < 0f)
            {
                failureReason = "投射物飞行或碰撞参数无效。";
                return false;
            }

            Vec3 direction = request.Direction.NormalizedCopy();
            GameEntity entity = string.IsNullOrEmpty(request.PrefabResourceName)
                ? GameEntity.CreateEmpty(Mission.Scene)
                : GameEntity.Instantiate(Mission.Scene, request.PrefabResourceName, false, false);
            if (entity == null)
            {
                failureReason = "投射物预制体不存在。";
                return false;
            }
            if (!string.IsNullOrEmpty(request.MeshResourceName))
            {
                Mesh mesh = Mesh.GetFromResource(request.MeshResourceName);
                if (mesh != null)
                    entity.AddMesh(mesh, true);
            }
            if (!string.IsNullOrEmpty(request.ParticleSystemName))
                entity.AddParticleSystemComponent(request.ParticleSystemName);
            entity.SetGlobalFrame(new MatrixFrame(Mat3.CreateMat3WithForward(direction), request.StartPosition));

            int staggerIndex = _projectiles.Count & 3;
            _projectiles.Add(new ProjectileRecord
            {
                Entity = entity,
                Request = request,
                Direction = direction,
                LastCollisionPosition = request.StartPosition,
                RemainingLifetime = request.Lifetime,
                CollisionTimer = request.CollisionInterval * staggerIndex * 0.25f,
                HomingTimer = request.HomingInterval * staggerIndex * 0.25f,
                HomingDelayRemaining = request.HomingDelay
            });
            return true;
        }

        public void SpawnTimedParticle(string particleSystemName, Vec3 position, float lifetime, float scale = 1f)
        {
            if (Mission?.Scene == null || string.IsNullOrEmpty(particleSystemName) || lifetime <= 0f || scale <= 0f)
                return;
            GameEntity effect = GameEntity.CreateEmpty(Mission.Scene);
            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin = position;
            frame.rotation.ApplyScaleLocal(scale);
            effect.SetGlobalFrame(frame);
            effect.AddParticleSystemComponent(particleSystemName);
            _effects.Add(new TimedEffect { Entity = effect, RemainingLifetime = lifetime });
        }

        /// <summary>命中音效排到任务帧，保证施法成功后框架的施法声先播放。</summary>
        public void QueueOneShotSound(string eventName, Vec3 position)
        {
            if (!string.IsNullOrEmpty(eventName) && position.IsValid)
                _pendingSounds.Add(new PendingSound { EventName = eventName, Position = position });
        }

        /// <summary>让一圈火焰从碰撞点扩散至实际伤害半径，之后统一回收实体。</summary>
        public void SpawnExpandingParticleRing(
            string particleSystemName, string secondaryParticleSystemName,
            Vec3 center, float radius, int count, float expansionDuration, float lifetime)
        {
            if (Mission?.Scene == null || !center.IsValid ||
                string.IsNullOrEmpty(particleSystemName) || radius <= 0f ||
                count < 3 || expansionDuration <= 0f || lifetime < expansionDuration)
                return;

            for (int i = 0; i < count; i++)
            {
                double angle = 2.0 * Math.PI * i / count;
                Vec3 offset = new Vec3(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius, 0f);
                GameEntity effect = GameEntity.CreateEmpty(Mission.Scene);
                effect.SetLocalPosition(center);
                effect.AddParticleSystemComponent(particleSystemName);
                if (!string.IsNullOrEmpty(secondaryParticleSystemName))
                    effect.AddParticleSystemComponent(secondaryParticleSystemName);
                _effects.Add(new TimedEffect
                {
                    Entity = effect,
                    RemainingLifetime = lifetime,
                    Origin = center,
                    ExpansionOffset = offset,
                    ExpansionDuration = expansionDuration
                });
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            TickEffects(dt);
            if (dt <= 0f || Mission?.Scene == null)
                return;

            for (int i = 0; i < _pendingSounds.Count; i++)
            {
                PendingSound sound = _pendingSounds[i];
                try { SoundManager.StartOneShotEvent(sound.EventName, sound.Position); }
                catch (Exception) { /* 音效故障不得中断战斗任务帧。 */ }
            }
            _pendingSounds.Clear();

            _frameCapsules.Clear();

            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                ProjectileRecord record = _projectiles[i];
                SpellProjectileRequest request = record.Request;
                record.RemainingLifetime -= dt;
                UpdateHoming(record, dt);

                Vec3 visualStart = record.Entity.GlobalPosition;
                float travelStep = request.Speed * dt;
                if (request.MaxTravelDistance > 0f)
                    travelStep = MathF.Min(travelStep,
                        MathF.Max(0f, request.MaxTravelDistance - record.TravelledDistance));
                Vec3 visualEnd = visualStart + record.Direction * travelStep;
                record.TravelledDistance += travelStep;
                record.Entity.SetGlobalFrame(
                    new MatrixFrame(Mat3.CreateMat3WithForward(record.Direction), visualEnd));

                record.CollisionTimer -= dt;
                bool reachedMaximumDistance = request.MaxTravelDistance > 0f &&
                    record.TravelledDistance >= request.MaxTravelDistance;
                bool checkCollision = request.PiercesAgents || record.CollisionTimer <= 0f ||
                    record.RemainingLifetime <= 0f || reachedMaximumDistance;
                if (checkCollision)
                {
                    record.CollisionTimer += request.CollisionInterval;
                    Vec3 collisionStart = record.LastCollisionPosition;
                    Vec3 collisionEnd = visualEnd;
                    record.LastCollisionPosition = collisionEnd;

                    float agentDistance = float.MaxValue;
                    Vec3 agentHitPosition = collisionEnd;
                    Agent hitAgent = request.PiercesAgents ? null : FindFirstAgentOnSegment(
                        request, collisionStart, collisionEnd,
                        out agentDistance, out agentHitPosition);
                    bool hitWorld = Mission.Scene.RayCastForClosestEntityOrTerrain(
                        collisionStart,
                        collisionEnd,
                        out float worldDistance,
                        out Vec3 worldHitPosition,
                        request.WorldHitRadius,
                        BodyFlags.CommonCollisionExcludeFlags);

                    if (request.PiercesAgents && request.OnTravelSegment != null)
                    {
                        Vec3 traversedEnd = hitWorld ? worldHitPosition : collisionEnd;
                        record.Entity.SetGlobalFrame(new MatrixFrame(
                            Mat3.CreateMat3WithForward(record.Direction), traversedEnd));
                        try { request.OnTravelSegment(collisionStart, traversedEnd); }
                        catch (Exception ex) { Debug.Print("[New_ZZZF][法术投射物] 穿透回调异常: " + ex); }
                    }

                    if (hitAgent != null && (!hitWorld || agentDistance <= worldDistance))
                    {
                        InvokeImpact(record, agentHitPosition, hitAgent, SpellProjectileImpactReason.Agent);
                        RemoveProjectileAt(i);
                        continue;
                    }
                    if (hitWorld)
                    {
                        InvokeImpact(record, worldHitPosition, null, SpellProjectileImpactReason.World);
                        RemoveProjectileAt(i);
                        continue;
                    }
                }

                if (record.RemainingLifetime <= 0f || reachedMaximumDistance)
                {
                    if (request.InvokeImpactWhenLifetimeExpires)
                        InvokeImpact(record, visualEnd, null, SpellProjectileImpactReason.LifetimeExpired);
                    RemoveProjectileAt(i);
                }
            }
        }

        private void UpdateHoming(ProjectileRecord record, float dt)
        {
            SpellProjectileRequest request = record.Request;
            if (request.HomingTurnRateDegrees <= 0f)
                return;

            if (record.HomingDelayRemaining > 0f)
            {
                record.HomingDelayRemaining -= dt;
                return;
            }

            record.HomingTimer -= dt;
            if (record.HomingTimer > 0f)
                return;

            float steeringStep = request.HomingInterval > 0f ? request.HomingInterval : dt;
            record.HomingTimer += steeringStep;
            Agent target = request.TargetAgent;
            if (!CanHitAgent(request, target))
            {
                target = request.AcquireTarget == null ? null : request.AcquireTarget();
                request.TargetAgent = target;
            }
            if (!CanHitAgent(request, target))
                return;

            CapsuleData targetCapsule = GetFrameCapsule(target);
            Vec3 desiredDirection = (targetCapsule.P1 + targetCapsule.P2) * 0.5f -
                                    record.Entity.GlobalPosition;
            if (desiredDirection.LengthSquared < 0.0001f)
                return;
            desiredDirection.Normalize();
            float maxAngle = request.HomingTurnRateDegrees * (MathF.PI / 180f) * steeringStep;
            record.Direction = TurnTowards(record.Direction, desiredDirection, maxAngle);
        }

        private Agent FindFirstAgentOnSegment(
            SpellProjectileRequest request,
            Vec3 start,
            Vec3 end,
            out float distanceAlongSegment,
            out Vec3 hitPosition)
        {
            distanceAlongSegment = float.MaxValue;
            hitPosition = end;
            Agent closest = null;
            Vec3 projectileSegment = end - start;
            float projectileLength = projectileSegment.Length;
            if (projectileLength <= 0.0001f)
                return null;

            IReadOnlyList<Agent> sharedCandidates = request.CollisionCandidatesProvider?.Invoke();
            if (sharedCandidates != null)
            {
                for (int i = 0; i < sharedCandidates.Count; i++)
                    TestCandidate(request, sharedCandidates[i], start, end, projectileLength,
                        ref closest, ref distanceAlongSegment, ref hitPosition);
                return closest;
            }

            Vec3 midpoint = (start + end) * 0.5f;
            float queryRadius = projectileLength * 0.5f + request.HitRadius + 1f;
            FillNearbyAgents(request, midpoint, queryRadius);
            foreach (Agent candidate in _nearbyAgents)
                TestCandidate(request, candidate, start, end, projectileLength,
                    ref closest, ref distanceAlongSegment, ref hitPosition);
            return closest;
        }

        private CapsuleData GetFrameCapsule(Agent agent)
        {
            if (!_frameCapsules.TryGetValue(agent, out CapsuleData capsule))
            {
                capsule = agent.CollisionCapsule;
                _frameCapsules.Add(agent, capsule);
            }
            return capsule;
        }

        private void TestCandidate(
            SpellProjectileRequest request,
            Agent candidate,
            Vec3 start,
            Vec3 end,
            float projectileLength,
            ref Agent closest,
            ref float closestDistance,
            ref Vec3 closestHitPosition)
        {
            if (!CanHitAgent(request, candidate))
                return;

            CapsuleData capsule = GetFrameCapsule(candidate);
            float projectileT;
            float distanceSquared = SegmentToSegmentDistanceSquared(
                start, end, capsule.P1, capsule.P2, out projectileT);
            float combinedRadius = request.HitRadius + capsule.Radius;
            if (distanceSquared > combinedRadius * combinedRadius)
                return;

            float distance = projectileLength * projectileT;
            if (distance >= closestDistance)
                return;
            closestDistance = distance;
            closestHitPosition = start + (end - start) * projectileT;
            closest = candidate;
        }

        /// <summary>返回两线段最近距离平方，同时返回第一条线段的 0..1 位置。</summary>
        private static float SegmentToSegmentDistanceSquared(
            Vec3 p1,
            Vec3 q1,
            Vec3 p2,
            Vec3 q2,
            out float firstSegmentT)
        {
            const float epsilon = 0.000001f;
            Vec3 d1 = q1 - p1;
            Vec3 d2 = q2 - p2;
            Vec3 r = p1 - p2;
            float a = Vec3.DotProduct(d1, d1);
            float e = Vec3.DotProduct(d2, d2);
            float f = Vec3.DotProduct(d2, r);
            float s;
            float t;

            if (a <= epsilon && e <= epsilon)
            {
                firstSegmentT = 0f;
                return r.LengthSquared;
            }
            if (a <= epsilon)
            {
                s = 0f;
                t = Clamp01(f / e);
            }
            else
            {
                float c = Vec3.DotProduct(d1, r);
                if (e <= epsilon)
                {
                    t = 0f;
                    s = Clamp01(-c / a);
                }
                else
                {
                    float b = Vec3.DotProduct(d1, d2);
                    float denominator = a * e - b * b;
                    s = denominator == 0f ? 0f : Clamp01((b * f - c * e) / denominator);
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Clamp01((b - c) / a);
                    }
                }
            }

            firstSegmentT = s;
            Vec3 closest1 = p1 + d1 * s;
            Vec3 closest2 = p2 + d2 * t;
            return (closest1 - closest2).LengthSquared;
        }

        private static Vec3 TurnTowards(Vec3 current, Vec3 target, float maxAngle)
        {
            current.Normalize();
            target.Normalize();
            float dot = MathF.Clamp(Vec3.DotProduct(current, target), -1f, 1f);
            float angle = (float)Math.Acos(dot);
            if (angle <= maxAngle || angle <= 0.0001f)
                return target;

            Vec3 axis = Vec3.CrossProduct(current, target);
            if (axis.LengthSquared <= 0.0001f)
            {
                axis = Vec3.CrossProduct(current, Vec3.Up);
                if (axis.LengthSquared <= 0.0001f)
                    axis = Vec3.Side;
            }
            axis.Normalize();
            Mat3 rotation = Mat3.Identity;
            rotation.RotateAboutAnArbitraryVector(axis, maxAngle);
            return rotation.TransformToParent(current).NormalizedCopy();
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
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
            if (candidate == null || candidate == request.Caster || !candidate.IsActive() || candidate.Health <= 0f)
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
            try
            {
                record.Request.OnImpact(new SpellProjectileImpact
                {
                    Caster = record.Request.Caster,
                    DirectTarget = directTarget,
                    Position = position,
                    Reason = reason,
                    Payload = record.Request.Payload
                });
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
                TimedEffect effect = _effects[i];
                effect.RemainingLifetime -= dt;
                if (effect.RemainingLifetime > 0f)
                {
                    if (effect.ExpansionDuration > 0f && effect.Elapsed < effect.ExpansionDuration)
                    {
                        effect.Elapsed += dt;
                        float progress = effect.Elapsed >= effect.ExpansionDuration
                            ? 1f : effect.Elapsed / effect.ExpansionDuration;
                        effect.Entity.SetLocalPosition(effect.Origin + effect.ExpansionOffset * progress);
                    }
                    continue;
                }
                effect.Entity?.Remove(1);
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
            _pendingSounds.Clear();
            if (ReferenceEquals(Current, this))
                Current = null;
            base.OnEndMission();
        }
    }
}
