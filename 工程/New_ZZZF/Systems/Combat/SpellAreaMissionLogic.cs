using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    public enum SpellAreaShape
    {
        Circle,
        Capsule
    }

    /// <summary>持续区域每次结算时传递给技能的目标信息。</summary>
    public struct SpellAreaTargetContext
    {
        public int AreaId { get; internal set; }
        public Agent Caster { get; internal set; }
        public Agent Target { get; internal set; }
        public Vec3 Center { get; internal set; }
        public int TickIndex { get; internal set; }
        /// <summary>技能自定义快照，例如法强系数、元素类型或组合技能参数。</summary>
        public object Payload { get; internal set; }
    }

    /// <summary>
    /// 通用持续法术区域请求。Circle 表示圆形范围；Capsule 表示从 Center 沿
    /// Direction 延伸 Length 的线段，并向两侧扩张 Radius，适合火墙等带状区域。
    /// </summary>
    public sealed class SpellAreaRequest
    {
        public Agent Caster { get; set; }
        public Vec3 Center { get; set; }
        public Vec3 Direction { get; set; } = Vec3.Forward;
        public SpellAreaShape Shape { get; set; } = SpellAreaShape.Circle;
        public float Radius { get; set; } = 3f;
        public float Length { get; set; }
        public float HeightTolerance { get; set; } = 2.5f;
        public float Duration { get; set; } = 5f;
        /// <summary>区域伤害默认每0.5秒结算一次；技能应将原每秒伤害均分到每跳。</summary>
        public float TickInterval { get; set; } = 0.5f;
        public bool TickImmediately { get; set; } = true;
        public bool HitHumanAgentsOnly { get; set; } = true;
        public bool HitEnemiesOnly { get; set; } = true;

        /// <summary>区域表现使用的粒子；胶囊区域会按 ParticleSpacing 沿线铺设。</summary>
        public string ParticleSystemName { get; set; }
        /// <summary>可选的第二表现层，与主粒子共用实体，不额外增加区域实体数。</summary>
        public string SecondaryParticleSystemName { get; set; }
        public float ParticleSpacing { get; set; } = 1.5f;

        /// <summary>
        /// 可选动态中心。用于跟随施法者或任务物体；为空时区域固定在 Center。
        /// 返回无效坐标时继续保留上一帧位置。
        /// </summary>
        public Func<Vec3> CenterProvider { get; set; }
        public Func<Agent, bool> AdditionalAgentFilter { get; set; }
        public Action<SpellAreaTargetContext> OnAffectTarget { get; set; }
        public Action<int, object> OnExpired { get; set; }
        public object Payload { get; set; }
    }

    /// <summary>
    /// 全法术共用的持续区域管理器。负责区域生命周期、低频目标查询、形状判定、
    /// 粒子铺设与回收；具体技能只负责在 OnAffectTarget 中结算伤害或状态。
    /// </summary>
    public sealed class SpellAreaMissionLogic : MissionLogic
    {
        // 同一帧最多执行固定次数的原生空间查询。大量光环同时到期时，其余查询
        // 顺延到后续帧，避免形成明显的1% low尖峰。
        private const int MaximumAreaQueriesPerFrame = 8;
        private const float DynamicCenterUpdateInterval = 0.10f;
        private const int MaximumParticleCountPerArea = 32;
        private const float MinimumVisualMovementSquared = 0.0025f;

        private sealed class AreaEffectEntity
        {
            public GameEntity Entity;
            public Vec3 Offset;
        }

        private sealed class AreaRecord
        {
            public int Id;
            public SpellAreaRequest Request;
            public Vec3 Center;
            public Vec3 Direction;
            public float RemainingDuration;
            public float TickTimer;
            public float CenterUpdateTimer;
            public int TickIndex;
            public readonly List<AreaEffectEntity> Effects = new List<AreaEffectEntity>();
        }

        private readonly List<AreaRecord> _areas = new List<AreaRecord>();
        private readonly MBList<Agent> _nearbyAgents = new MBList<Agent>();
        private int _nextAreaId = 1;

        public static SpellAreaMissionLogic Current { get; private set; }
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

        public static SpellAreaMissionLogic GetForCurrentMission()
        {
            Mission mission = Mission.Current;
            if (mission == null)
                return null;
            if (Current != null && ReferenceEquals(Current.Mission, mission))
                return Current;
            Current = mission.GetMissionBehavior<SpellAreaMissionLogic>();
            return Current;
        }

        public bool TryCreate(
            SpellAreaRequest request,
            out int areaId,
            out string failureReason)
        {
            areaId = 0;
            failureReason = null;
            if (request?.Caster == null || !request.Caster.IsActive() || Mission?.Scene == null)
            {
                failureReason = "施法者或当前场景不可用。";
                return false;
            }
            if (!request.Center.IsValid || request.Radius <= 0f ||
                request.Duration <= 0f || request.TickInterval <= 0f ||
                request.HeightTolerance <= 0f)
            {
                failureReason = "持续区域参数无效。";
                return false;
            }
            if (request.Shape == SpellAreaShape.Capsule &&
                (request.Length <= 0f || request.Direction.AsVec2.LengthSquared < 0.001f))
            {
                failureReason = "带状区域的方向或长度无效。";
                return false;
            }

            Vec3 direction = request.Direction;
            direction.z = 0f;
            if (direction.LengthSquared > 0.001f)
                direction.Normalize();

            AreaRecord record = new AreaRecord
            {
                Id = _nextAreaId++,
                Request = request,
                Center = request.Center,
                Direction = direction,
                RemainingDuration = request.Duration,
                TickTimer = request.TickImmediately ? request.TickInterval : 0f
            };
            CreateEffects(record);
            _areas.Add(record);
            areaId = record.Id;
            return true;
        }

        public bool Cancel(int areaId, bool invokeExpiredCallback = false)
        {
            for (int i = _areas.Count - 1; i >= 0; i--)
            {
                if (_areas[i].Id != areaId)
                    continue;
                RemoveAreaAt(i, invokeExpiredCallback);
                return true;
            }
            return false;
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (dt <= 0f || Mission?.Scene == null)
                return;

            int remainingQueryBudget = MaximumAreaQueriesPerFrame;
            for (int i = _areas.Count - 1; i >= 0; i--)
            {
                AreaRecord record = _areas[i];
                UpdateDynamicCenter(record, dt);
                record.RemainingDuration -= dt;
                record.TickTimer += dt;

                // 不使用 while 补算：严重掉帧后集中补出多次范围查询和伤害，反而会
                // 延长卡顿。到期区域每帧最多结算一次，超出的时间直接丢弃。
                if (record.TickTimer >= record.Request.TickInterval &&
                    remainingQueryBudget > 0)
                {
                    remainingQueryBudget--;
                    record.TickTimer = 0f;
                    AffectTargets(record);
                    record.TickIndex++;

                    // 第一次立即结算后加入极小且确定性的错峰，使同批生成的光环
                    // 后续不会永远在同一帧触发。
                    if (record.TickIndex == 1 && record.Request.TickImmediately)
                    {
                        float maximumStagger = MathF.Min(
                            0.10f, record.Request.TickInterval * 0.25f);
                        record.TickTimer = -maximumStagger * ((record.Id % 8) / 8f);
                    }
                }

                if (record.RemainingDuration <= 0f)
                    RemoveAreaAt(i, true);
            }
        }

        private void AffectTargets(AreaRecord record)
        {
            if (record.Request.OnAffectTarget == null)
                return;

            Vec3 queryCenter = record.Request.Shape == SpellAreaShape.Capsule
                ? record.Center + record.Direction * (record.Request.Length * 0.5f)
                : record.Center;
            float queryRadius = record.Request.Shape == SpellAreaShape.Capsule
                ? record.Request.Length * 0.5f + record.Request.Radius
                : record.Request.Radius;

            if (record.Request.HitEnemiesOnly && record.Request.Caster.Team != null)
            {
                Mission.GetNearbyEnemyAgents(
                    queryCenter.AsVec2, queryRadius,
                    record.Request.Caster.Team, _nearbyAgents);
            }
            else
            {
                Mission.GetNearbyAgents(queryCenter.AsVec2, queryRadius, _nearbyAgents);
            }

            foreach (Agent target in _nearbyAgents)
            {
                if (!CanAffect(record, target) || !Contains(record, target.Position + Vec3.Up))
                    continue;
                SpellAreaTargetContext context = new SpellAreaTargetContext
                {
                    AreaId = record.Id,
                    Caster = record.Request.Caster,
                    Target = target,
                    Center = record.Center,
                    TickIndex = record.TickIndex,
                    Payload = record.Request.Payload
                };
                try
                {
                    record.Request.OnAffectTarget(context);
                }
                catch (Exception ex)
                {
                    /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                }
            }
        }

        private static bool CanAffect(AreaRecord record, Agent target)
        {
            if (target == null || target == record.Request.Caster || !target.IsActive())
                return false;
            if (record.Request.HitHumanAgentsOnly && !target.IsHuman)
                return false;
            if (record.Request.HitEnemiesOnly && !record.Request.Caster.IsEnemyOf(target))
                return false;
            return record.Request.AdditionalAgentFilter == null ||
                   record.Request.AdditionalAgentFilter(target);
        }

        private static bool Contains(AreaRecord record, Vec3 point)
        {
            if (MathF.Abs(point.z - record.Center.z) > record.Request.HeightTolerance)
                return false;

            Vec2 offset = point.AsVec2 - record.Center.AsVec2;
            float radiusSquared = record.Request.Radius * record.Request.Radius;
            if (record.Request.Shape == SpellAreaShape.Circle)
                return offset.LengthSquared <= radiusSquared;

            Vec2 segment = record.Direction.AsVec2 * record.Request.Length;
            float segmentLengthSquared = segment.LengthSquared;
            float t = segmentLengthSquared <= 0.0001f
                ? 0f
                : MathF.Clamp(Vec2.DotProduct(offset, segment) / segmentLengthSquared, 0f, 1f);
            Vec2 nearest = record.Center.AsVec2 + segment * t;
            return (point.AsVec2 - nearest).LengthSquared <= radiusSquared;
        }

        private void UpdateDynamicCenter(AreaRecord record, float dt)
        {
            if (record.Request.CenterProvider == null)
                return;
            record.CenterUpdateTimer += dt;
            if (record.CenterUpdateTimer < DynamicCenterUpdateInterval)
                return;
            record.CenterUpdateTimer = 0f;

            Vec3 newCenter;
            try
            {
                newCenter = record.Request.CenterProvider();
            }
            catch (Exception ex)
            {
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                return;
            }
            if (!newCenter.IsValid)
                return;
            if ((newCenter - record.Center).LengthSquared < MinimumVisualMovementSquared)
                return;
            record.Center = newCenter;
            for (int i = 0; i < record.Effects.Count; i++)
                record.Effects[i].Entity.SetLocalPosition(record.Center + record.Effects[i].Offset);
        }

        private void CreateEffects(AreaRecord record)
        {
            string particle = record.Request.ParticleSystemName;
            if (string.IsNullOrEmpty(particle))
                return;

            if (record.Request.Shape == SpellAreaShape.Circle)
            {
                AddEffect(record, Vec3.Zero, particle, record.Request.SecondaryParticleSystemName);
                return;
            }

            float spacing = MathF.Max(0.25f, record.Request.ParticleSpacing);
            int count = Math.Min(
                MaximumParticleCountPerArea - 1,
                Math.Max(1, (int)Math.Ceiling(record.Request.Length / spacing)));
            for (int i = 0; i <= count; i++)
            {
                float distance = record.Request.Length * i / count;
                AddEffect(record, record.Direction * distance, particle,
                    record.Request.SecondaryParticleSystemName);
            }
        }

        private void AddEffect(
            AreaRecord record,
            Vec3 offset,
            string particle,
            string secondaryParticle)
        {
            GameEntity entity = GameEntity.CreateEmpty(Mission.Scene);
            entity.SetLocalPosition(record.Center + offset);
            entity.AddParticleSystemComponent(particle);
            if (!string.IsNullOrEmpty(secondaryParticle))
                entity.AddParticleSystemComponent(secondaryParticle);
            record.Effects.Add(new AreaEffectEntity { Entity = entity, Offset = offset });
        }

        private void RemoveAreaAt(int index, bool invokeExpiredCallback)
        {
            AreaRecord record = _areas[index];
            for (int i = record.Effects.Count - 1; i >= 0; i--)
                record.Effects[i].Entity?.Remove(1);
            _areas.RemoveAt(index);
            if (!invokeExpiredCallback || record.Request.OnExpired == null)
                return;
            try
            {
                record.Request.OnExpired(record.Id, record.Request.Payload);
            }
            catch (Exception ex)
            {
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            }
        }

        protected override void OnEndMission()
        {
            for (int i = _areas.Count - 1; i >= 0; i--)
                RemoveAreaAt(i, false);
            if (ReferenceEquals(Current, this))
                Current = null;
            base.OnEndMission();
        }
    }
}
