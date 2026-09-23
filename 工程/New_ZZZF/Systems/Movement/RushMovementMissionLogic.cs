using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    public enum RushMovementMode
    {
        TrackAgent,
        FixedPosition,
        DirectionalCharge,
        ParabolicLeap
    }

    public enum RushEndReason
    {
        Arrived,
        DurationExpired,
        InvalidMover,
        InvalidTarget,
        LostSight,
        NoNavigation,
        Stuck,
        Cancelled,
        SpearInterrupted
    }

    public sealed class RushMovementOptions
    {
        public float Duration = 1f;
        public float StopDistance = 1f;
        public float SpeedLimit = 15f;
        public bool SpeedLimitIsMultiplier;
        public bool RequireLineOfSight;
        public float SightCheckInterval = 0.2f;
        public float LostSightGrace = 0.75f;
        public bool AllowMounted;
        public bool IsChongCiZhan;
        public bool TriggerRightAttackOnEnd;
        public bool UseSafeKinematicMovement;
        public float KinematicSpeed;
        /// <summary>可选的方向冲锋终点；有效时骑乘冲锋到达该点即结束，仍保留穿阵撞倒逻辑。</summary>
        public Vec3 DirectionalStopPosition = Vec3.Invalid;
        public Action<Agent, Agent, RushEndReason> OnEnded;
    }

    public sealed class ParabolicLeapOptions
    {
        public float Duration = 1.3f;
        public float Distance = 3.5f;
        public float ApexHeight = 2.2f;
        public bool FaceTargetDuringLeap;
        public Action<Agent, Agent> OnApex;
        public Action<Agent, Agent, RushEndReason> OnEnded;
    }

    /// <summary>
    /// 统一管理技能强制移动。常规位移与骑乘冲锋使用原生移动；徒步冲刺斩因原生
    /// 输入没有最低/目标速度接口，使用带导航、地形和碰撞校验的短步连续位移。
    /// 冲刺斩的攻击、骑乘冲撞与枪兵中断也在这里收口。
    /// </summary>
    public sealed class RushMovementMissionLogic : MissionLogic, IPlayerInputEffector
    {
        private sealed class RushRecord
        {
            public Agent Mover;
            public Agent Target;
            public Vec3 FixedPosition;
            public Vec2 Direction;
            public Vec2 DesiredMovementDirection;
            public AgentControllerType OriginalController;
            public bool TemporarilyAiControlled;
            public RushMovementMode Mode;
            public RushMovementOptions Options;
            public float Remaining;
            public float NavigationTimer;
            public float SightTimer;
            public float LostSightTime;
            public float StuckTime;
            public Vec3 LastProgressPosition;
            public Vec3 StartPosition;
            public float DiagnosticsTimer;
            public bool InputInjectionLogged;
            public bool NativeSprintTriggered;
            public Agent LastMount;
            public ParabolicLeapOptions LeapOptions;
            public Vec3 LeapLandingPosition;
            public bool ApexTriggered;
        }

        private readonly Dictionary<int, RushRecord> _records = new Dictionary<int, RushRecord>();
        private readonly List<RushRecord> _tickSnapshot = new List<RushRecord>();
        private readonly Dictionary<int, Agent> _pendingRightAttacks = new Dictionary<int, Agent>();

        public static RushMovementMissionLogic Current
        {
            get { return Mission.Current?.GetMissionBehavior<RushMovementMissionLogic>(); }
        }

        public bool IsRushing(Agent agent)
        {
            return agent != null && _records.ContainsKey(agent.Index);
        }

        public bool TryRushToAgent(Agent mover, Agent target, RushMovementOptions options, out string failureReason)
        {
            failureReason = null;
            if (!ValidateStart(mover, options, out failureReason))
                return false;
            if (!IsValidEnemy(mover, target))
            {
                failureReason = "没有有效的敌方目标。";
                return false;
            }
            if (options.RequireLineOfSight && !HasLineOfSight(mover, target))
            {
                failureReason = "目标不在视线内。";
                return false;
            }
            if (!TryGetNavigablePosition(target.Position, out _))
            {
                failureReason = "目标附近没有可用的导航区域。";
                return false;
            }

            RushRecord record = CreateRecord(mover, options, RushMovementMode.TrackAgent);
            record.Target = target;
            _records.Add(mover.Index, record);
#if false
            // 旧方案：把玩家临时切成 AI。实测会让主角进入其他 AI 系统，且不能
            // 可靠驱动玩家冲锋，因此保留代码但停用。
            TakeTemporaryAiControl(record);
#endif
            ApplySpeedLimit(record);
#if false
            // 已验证只改变最高速度，无法提高当前/目标速度，保留供追溯。
            RefreshOnFootChongCiZhanStats(record, "Start");
#endif
            RefreshMountedChongCiZhanStats(record, "Start");
            LogStart(record);
            return true;
        }

        public bool TryRushToPosition(Agent mover, Vec3 position, RushMovementOptions options, out string failureReason)
        {
            failureReason = null;
            if (!ValidateStart(mover, options, out failureReason))
                return false;
            if (!TryGetNavigablePosition(position, out Vec3 navigable))
            {
                failureReason = "目标地点没有可用的导航区域。";
                return false;
            }

            RushRecord record = CreateRecord(mover, options, RushMovementMode.FixedPosition);
            record.FixedPosition = navigable;
            _records.Add(mover.Index, record);
#if false
            TakeTemporaryAiControl(record);
#endif
            ApplySpeedLimit(record);
#if false
            RefreshOnFootChongCiZhanStats(record, "Start");
#endif
            RefreshMountedChongCiZhanStats(record, "Start");
            LogStart(record);
            return true;
        }

        public bool TryDirectionalCharge(Agent mover, Agent aimedTarget, Vec2 direction, RushMovementOptions options, out string failureReason)
        {
            failureReason = null;
            if (!ValidateStart(mover, options, out failureReason))
                return false;
            if (mover.MountAgent == null)
            {
                failureReason = "方向冲锋需要处于骑乘状态。";
                return false;
            }
            if (direction.LengthSquared < 0.01f)
            {
                failureReason = "无法确定冲锋方向。";
                return false;
            }

            RushRecord record = CreateRecord(mover, options, RushMovementMode.DirectionalCharge);
            record.Target = aimedTarget;
            record.Direction = direction.Normalized();
            if (options.DirectionalStopPosition.IsValid)
            {
                if (!TryGetNavigablePosition(options.DirectionalStopPosition, out Vec3 stopPosition))
                {
                    failureReason = "目标地点没有可用的导航区域。";
                    return false;
                }
                record.FixedPosition = stopPosition;
            }
            record.LastMount = mover.MountAgent;
            _records.Add(mover.Index, record);
#if false
            TakeTemporaryAiControl(record);
#endif
            ApplySpeedLimit(record);
#if false
            RefreshOnFootChongCiZhanStats(record, "Start");
#endif
            RefreshMountedChongCiZhanStats(record, "Start");
            LogStart(record);
            return true;
        }

        /// <summary>
        /// 启动技能专用抛物线后跃。落点不作为施法门槛；空间不足时仍会起跳，
        /// 并由飞行中的场景射线在墙体前正常终止。
        /// </summary>
        public bool TryParabolicLeap(
            Agent mover,
            Agent target,
            Vec2 direction,
            ParabolicLeapOptions leapOptions,
            out string failureReason)
        {
            RushMovementOptions movementOptions = new RushMovementOptions
            {
                Duration = leapOptions?.Duration ?? 0f,
                AllowMounted = false,
                OnEnded = leapOptions?.OnEnded
            };
            failureReason = null;
            if (!ValidateStart(mover, movementOptions, out failureReason))
                return false;
            if (leapOptions == null || leapOptions.Distance <= 0f || leapOptions.ApexHeight <= 0f)
            {
                failureReason = "后跃配置无效。";
                return false;
            }
            if (direction.LengthSquared < 0.001f)
            {
                failureReason = "无法确定后跃方向。";
                return false;
            }

            RushRecord record = CreateRecord(mover, movementOptions, RushMovementMode.ParabolicLeap);
            record.Target = target;
            record.Direction = direction.Normalized();
            record.LeapOptions = leapOptions;
            record.LeapLandingPosition = mover.Position + record.Direction.ToVec3() * leapOptions.Distance;
            record.LeapLandingPosition.z = Mission.Scene.GetGroundHeightAtPosition(
                record.LeapLandingPosition, BodyFlags.CommonCollisionExcludeFlags);
            record.FixedPosition = record.LeapLandingPosition;
            _records.Add(mover.Index, record);
            return true;
        }

        public void CancelRush(Agent mover, RushEndReason reason = RushEndReason.Cancelled)
        {
            if (mover == null || !_records.TryGetValue(mover.Index, out RushRecord record))
                return;
            EndRecord(record, reason);
        }

        public static bool IsMountedChongCiZhanCharge(Agent possibleRiderOrMount)
        {
            RushMovementMissionLogic manager = Current;
            if (manager == null || possibleRiderOrMount == null)
                return false;

            Agent rider = possibleRiderOrMount.IsMount ? possibleRiderOrMount.RiderAgent : possibleRiderOrMount;
            if (rider == null || !manager._records.TryGetValue(rider.Index, out RushRecord record))
                return false;
            return record.Mode == RushMovementMode.DirectionalCharge &&
                   record.Options.IsChongCiZhan && rider.MountAgent != null;
        }

        public static bool IsOnFootChongCiZhanRush(Agent agent)
        {
            RushMovementMissionLogic manager = Current;
            return manager != null && agent != null && agent.MountAgent == null &&
                   manager._records.TryGetValue(agent.Index, out RushRecord record) &&
                   record.Options.IsChongCiZhan;
        }

        public static bool ShouldForceHorseChargeKnockDown(
            Agent attacker,
            Agent victim,
            in AttackCollisionData collisionData)
        {
            if (!collisionData.IsHorseCharge || attacker == null || victim == null ||
                !victim.IsHuman || victim.MountAgent != null)
                return false;

            Agent rider = attacker.IsMount ? attacker.RiderAgent : attacker;
            return rider != null && rider.IsEnemyOf(victim) && IsMountedChongCiZhanCharge(rider);
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_records.Count == 0)
                return;

            _tickSnapshot.Clear();
            _tickSnapshot.AddRange(_records.Values);
            for (int i = 0; i < _tickSnapshot.Count; i++)
            {
                RushRecord record = _tickSnapshot[i];
                if (record.Mover == null || !_records.ContainsKey(record.Mover.Index))
                    continue;
                TickRecord(record, dt);
            }
        }

        private void TickRecord(RushRecord record, float dt)
        {
            Agent mover = record.Mover;
            if (mover == null || !mover.IsActive())
            {
                EndRecord(record, RushEndReason.InvalidMover);
                return;
            }

            if (!record.Options.AllowMounted && mover.MountAgent != null)
            {
                EndRecord(record, RushEndReason.Cancelled);
                return;
            }
            if (record.Mode == RushMovementMode.DirectionalCharge && mover.MountAgent == null)
            {
                EndRecord(record, RushEndReason.Cancelled);
                return;
            }

            if (record.Mode == RushMovementMode.ParabolicLeap)
            {
                TickParabolicLeap(record, dt);
                return;
            }

            record.Remaining -= dt;
            if (record.Remaining <= 0f)
            {
                EndRecord(record, RushEndReason.DurationExpired);
                return;
            }

            if (record.Mode == RushMovementMode.TrackAgent)
            {
                if (!IsValidEnemy(mover, record.Target))
                {
                    EndRecord(record, RushEndReason.InvalidTarget);
                    return;
                }

                if (record.Options.RequireLineOfSight)
                {
                    record.SightTimer -= dt;
                    if (record.SightTimer <= 0f)
                    {
                        record.SightTimer = Math.Max(0.05f, record.Options.SightCheckInterval);
                        if (HasLineOfSight(mover, record.Target))
                            record.LostSightTime = 0f;
                        else
                            record.LostSightTime += record.SightTimer;
                    }
                    if (record.LostSightTime >= record.Options.LostSightGrace)
                    {
                        EndRecord(record, RushEndReason.LostSight);
                        return;
                    }
                }

                float distance = (record.Target.Position - mover.Position).AsVec2.Length;
                if (distance <= Math.Max(0.35f, record.Options.StopDistance))
                {
                    EndRecord(record, RushEndReason.Arrived);
                    return;
                }
            }
            else if (record.Mode == RushMovementMode.FixedPosition &&
                     (record.FixedPosition - mover.Position).AsVec2.Length <=
                     Math.Max(0.35f, record.Options.StopDistance))
            {
                EndRecord(record, RushEndReason.Arrived);
                return;
            }
            else if (record.Mode == RushMovementMode.DirectionalCharge &&
                     record.FixedPosition.IsValid &&
                     (record.FixedPosition - mover.Position).AsVec2.Length <=
                     Math.Max(0.35f, record.Options.StopDistance))
            {
                EndRecord(record, RushEndReason.Arrived);
                return;
            }

            record.DiagnosticsTimer -= dt;
            if (record.DiagnosticsTimer <= 0f)
            {
                record.DiagnosticsTimer = 0.5f;
                float trackedSpeed = -1f;
                if (SkillSystemBehavior.ActiveComponents.TryGetValue(
                        mover.Index, out AgentSkillComponent component) && component?.Speed != null)
                    trackedSpeed = component.Speed.speed.Length;
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            }

            if (record.Options.UseSafeKinematicMovement)
            {
                if (!TryResolveDestination(record, out Vec3 kinematicDestination) ||
                    !TryAdvanceKinematically(record, kinematicDestination, dt))
                {
                    EndRecord(record, RushEndReason.NoNavigation);
                }
                return;
            }

            record.NavigationTimer -= dt;
            if (record.NavigationTimer > 0f)
                return;
            record.NavigationTimer = record.Mode == RushMovementMode.DirectionalCharge ? 0.15f : 0.1f;

            Vec3 destination;
            if (!TryResolveDestination(record, out destination))
            {
                EndRecord(record, RushEndReason.NoNavigation);
                return;
            }

            float progress = (mover.Position - record.LastProgressPosition).AsVec2.Length;
            record.StuckTime = progress < 0.04f ? record.StuckTime + record.NavigationTimer : 0f;
            record.LastProgressPosition = mover.Position;
            if (record.StuckTime >= 1f)
            {
                EndRecord(record, RushEndReason.Stuck);
                return;
            }

            Vec2 facing = destination.AsVec2 - mover.Position.AsVec2;
            if (facing.LengthSquared > 0.001f)
            {
                record.DesiredMovementDirection = facing.Normalized();
                mover.LookDirection = record.DesiredMovementDirection.ToVec3();
            }

            // SetScriptedPosition only drives AI. Applying it to Agent.Main leaves the
            // player under normal input control, so the old implementation appeared to
            // do absolutely nothing both on foot and while mounted. Player movement is
            // injected from OnCollectPlayerEventControlFlags instead; AI keeps using the
            // engine navigation system here.
            if (mover.IsAIControlled)
            {
                WorldPosition worldPosition = new WorldPosition(Mission.Scene, destination);
                Agent.AIScriptedFrameFlags flags = Agent.AIScriptedFrameFlags.NeverSlowDown |
                                                   Agent.AIScriptedFrameFlags.NoAttack;
                mover.SetScriptedPosition(ref worldPosition, false, flags);
            }

            if (record.LastMount != mover.MountAgent)
            {
                ResetSpeedLimit(record.LastMount);
                record.LastMount = mover.MountAgent;
                ApplySpeedLimit(record);
            }
        }

        private void TickParabolicLeap(RushRecord record, float dt)
        {
            Agent mover = record.Mover;
            ParabolicLeapOptions options = record.LeapOptions;
            if (options == null)
            {
                EndRecord(record, RushEndReason.Cancelled);
                return;
            }

            float previousProgress = MathF.Clamp(
                (options.Duration - record.Remaining) / options.Duration, 0f, 1f);
            record.Remaining = Math.Max(0f, record.Remaining - dt);
            float progress = MathF.Clamp(
                (options.Duration - record.Remaining) / options.Duration, 0f, 1f);

            Vec3 candidate = record.StartPosition +
                             record.Direction.ToVec3() * (options.Distance * progress);
            float groundLine = record.StartPosition.z +
                               (record.LeapLandingPosition.z - record.StartPosition.z) * progress;
            candidate.z = groundLine + 4f * options.ApexHeight * progress * (1f - progress);

            // 身体中心做一条短射线。没有预检落点，因此狭窄空间允许发动，
            // 但不会借助逐帧坐标驱动穿过墙体。
            if (IsLeapStepBlocked(mover.Position, candidate))
            {
                EndRecord(record, RushEndReason.Stuck);
                return;
            }

            // 下落过程中若地面提前抬高，直接在接触点落地，避免钻入斜坡。
            float groundHeight = Mission.Scene.GetGroundHeightAtPosition(
                candidate, BodyFlags.CommonCollisionExcludeFlags);
            bool touchedGround = progress > 0.5f && candidate.z <= groundHeight + 0.05f;
            if (touchedGround)
                candidate.z = groundHeight;

            // TeleportToPosition 每次都会通知所有 AgentComponent 执行
            // OnAgentTeleported。连续调用会造成闪烁，也会放大大量 AI 同时后跃的开销。
            // SetInitialFrame 直接写入原生帧，并同时维持正确朝向。
            Vec2 facing = mover.LookDirection.AsVec2;
            if (options.FaceTargetDuringLeap && IsValidEnemy(mover, record.Target))
            {
                Vec2 towardTarget = record.Target.Position.AsVec2 - candidate.AsVec2;
                if (towardTarget.LengthSquared > 0.001f)
                    facing = towardTarget.Normalized();
            }
            if (facing.LengthSquared < 0.001f)
                facing = -record.Direction;
            mover.SetInitialFrame(candidate, facing.Normalized());

            if (!record.ApexTriggered && previousProgress < 0.5f && progress >= 0.5f)
            {
                record.ApexTriggered = true;
                try { options.OnApex?.Invoke(mover, record.Target); }
                catch (Exception ex) { /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */; }
            }

            if (touchedGround || progress >= 1f)
                EndRecord(record, RushEndReason.Arrived);
        }

        private bool IsLeapStepBlocked(Vec3 fromPosition, Vec3 toPosition)
        {
            Vec3 delta = toPosition - fromPosition;
            float length = delta.Length;
            if (length <= 0.01f)
                return false;

            float collisionDistance;
            Vec3 bodyStart = fromPosition + Vec3.Up * 1.05f;
            Vec3 bodyEnd = toPosition + Vec3.Up * 1.05f;
            bool bodyHit = Mission.Scene.RayCastForClosestEntityOrTerrain(
                bodyStart, bodyEnd, out collisionDistance, 0.01f,
                BodyFlags.CommonCollisionExcludeFlags);
            return bodyHit && collisionDistance + 0.05f < length;
        }

        private bool TryResolveDestination(RushRecord record, out Vec3 destination)
        {
            if (record.Mode == RushMovementMode.FixedPosition)
            {
                destination = record.FixedPosition;
                return true;
            }

            if (record.Mode == RushMovementMode.DirectionalCharge)
            {
                if (record.FixedPosition.IsValid)
                {
                    destination = record.FixedPosition;
                    return true;
                }
                for (float lookAhead = 24f; lookAhead >= 4f; lookAhead -= 4f)
                {
                    Vec3 candidate = record.Mover.Position + record.Direction.ToVec3() * lookAhead;
                    if (TryGetNavigablePosition(candidate, out destination))
                        return true;
                }
                destination = Vec3.Invalid;
                return false;
            }

            Vec2 towardTarget = record.Target.Position.AsVec2 - record.Mover.Position.AsVec2;
            if (towardTarget.LengthSquared < 0.001f)
            {
                destination = record.Mover.Position;
                return true;
            }
            Vec3 desired = record.Target.Position - towardTarget.Normalized().ToVec3() * record.Options.StopDistance;
            return TryGetNavigablePosition(desired, out destination);
        }

        private bool TryAdvanceKinematically(RushRecord record, Vec3 destination, float dt)
        {
            Agent mover = record.Mover;
            Vec2 direction = destination.AsVec2 - mover.Position.AsVec2;
            float remainingDistance = direction.Length;
            if (remainingDistance <= 0.01f)
                return true;

            direction /= remainingDistance;
            float requestedStep = Math.Min(
                remainingDistance,
                Math.Min(Math.Max(1f, record.Options.KinematicSpeed) * dt, 0.75f));
            Vec3 candidate = mover.Position + direction.ToVec3() * requestedStep;
            candidate.z = Mission.Scene.GetGroundHeightAtPosition(
                candidate, BodyFlags.CommonCollisionExcludeFlags);

            // 不允许用单步位移跨越过大的高度差。
            if (Math.Abs(candidate.z - mover.Position.z) > 0.8f)
                return false;

            int faceGroupId;
            if (Mission.Scene.GetNavigationMeshForPosition(
                    candidate, out faceGroupId, 0.35f, false) == UIntPtr.Zero)
                return false;

            // 从腰部高度检查本次短步前方；命中墙体或场景实体时不穿过去。
            Vec3 rayStart = mover.Position + Vec3.Up * 0.8f;
            Vec3 rayEnd = candidate + Vec3.Up * 0.8f;
            float collisionDistance;
            bool blocked = Mission.Scene.RayCastForClosestEntityOrTerrain(
                rayStart, rayEnd, out collisionDistance, 0.01f,
                BodyFlags.CommonCollisionExcludeFlags);
            if (blocked && collisionDistance + 0.05f < (rayEnd - rayStart).Length)
                return false;

            record.DesiredMovementDirection = direction;
            mover.LookDirection = direction.ToVec3();
            mover.SetMovementDirection(direction);
            mover.TeleportToPosition(candidate);
            return true;
        }

        private bool TryGetNavigablePosition(Vec3 requested, out Vec3 result)
        {
            result = requested;
            if (Mission?.Scene == null || !requested.IsValid)
                return false;

            result.z = Mission.Scene.GetGroundHeightAtPosition(result, BodyFlags.CommonCollisionExcludeFlags);
            int faceGroupId;
            if (Mission.Scene.GetNavigationMeshForPosition(result, out faceGroupId, 1.5f, false) != UIntPtr.Zero)
                return true;

            for (float radius = 0.75f; radius <= 3f; radius += 0.75f)
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = (float)(Math.PI * 2.0 * i / 8.0);
                    Vec3 candidate = requested + new Vec3(
                        (float)Math.Cos(angle) * radius,
                        (float)Math.Sin(angle) * radius,
                        0f);
                    candidate.z = Mission.Scene.GetGroundHeightAtPosition(candidate, BodyFlags.CommonCollisionExcludeFlags);
                    if (Mission.Scene.GetNavigationMeshForPosition(candidate, out faceGroupId, 1.5f, false) != UIntPtr.Zero)
                    {
                        result = candidate;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool ValidateStart(Agent mover, RushMovementOptions options, out string failureReason)
        {
            failureReason = null;
            if (Mission == null || Mission.Scene == null)
            {
                failureReason = "当前任务没有可用场景。";
                return false;
            }
            if (mover == null || !mover.IsActive())
            {
                failureReason = "施法者当前不可用。";
                return false;
            }
            if (options == null || options.Duration <= 0f)
            {
                failureReason = "冲刺配置无效。";
                return false;
            }
            if (_records.ContainsKey(mover.Index))
            {
                failureReason = "当前已经处于强制移动状态。";
                return false;
            }
            if (!options.AllowMounted && mover.MountAgent != null)
            {
                failureReason = "骑乘状态下不能使用该位移。";
                return false;
            }
            return true;
        }

        private static RushRecord CreateRecord(Agent mover, RushMovementOptions options, RushMovementMode mode)
        {
            return new RushRecord
            {
                Mover = mover,
                Mode = mode,
                Options = options,
                Remaining = options.Duration,
                NavigationTimer = 0f,
                SightTimer = 0f,
                LastProgressPosition = mover.Position,
                StartPosition = mover.Position,
                DiagnosticsTimer = 0f,
                LastMount = mover.MountAgent,
                OriginalController = mover.Controller,
                TemporarilyAiControlled = mover.Controller == AgentControllerType.Player
            };
        }

        private static bool IsValidEnemy(Agent mover, Agent target)
        {
            return mover != null && target != null && target.IsActive() && target.Health > 0f &&
                   target != mover && mover.IsEnemyOf(target);
        }

#if false
        private static void TakeTemporaryAiControl(RushRecord record)
        {
            if (record.TemporarilyAiControlled && record.Mover != null && record.Mover.IsActive())
                record.Mover.Controller = AgentControllerType.AI;
        }
#endif

        private static void LogStart(RushRecord record)
        {
            /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
        }

        public static bool HasLineOfSight(Agent mover, Agent target)
        {
            if (mover == null || target == null || Mission.Current?.Scene == null)
                return false;
            Vec3 from = mover.GetEyeGlobalPosition();
            Vec3 to = target.GetEyeGlobalPosition();
            float collisionDistance;
            bool hit = Mission.Current.Scene.RayCastForClosestEntityOrTerrain(
                from, to, out collisionDistance, 0.01f, BodyFlags.CommonFocusRayCastExcludeFlags);
            return !hit || collisionDistance + 0.15f >= (to - from).Length;
        }

        private static void ApplySpeedLimit(RushRecord record)
        {
            record.Mover.SetMaximumSpeedLimit(record.Options.SpeedLimit, record.Options.SpeedLimitIsMultiplier);
            if (record.Mover.MountAgent != null)
                record.Mover.MountAgent.SetMaximumSpeedLimit(record.Options.SpeedLimit, record.Options.SpeedLimitIsMultiplier);
        }

#if false
        private static void RefreshOnFootChongCiZhanStats(RushRecord record, string phase)
        {
            if (record?.Mover == null || !record.Mover.IsActive() ||
                !record.Options.IsChongCiZhan || record.Mover.MountAgent != null)
                return;

            try
            {
                MissionGameModels.Current.AgentStatCalculateModel.UpdateAgentStats(
                    record.Mover, record.Mover.AgentDrivenProperties);
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            }
            catch (Exception ex)
            {
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            }
        }
#endif

        private static void ResetSpeedLimit(Agent agent)
        {
            if (agent != null && agent.IsActive())
                agent.SetMaximumSpeedLimit(-1f, false);
        }

        private static void RefreshMountedChongCiZhanStats(RushRecord record, string phase)
        {
            if (record?.Mover == null || !record.Mover.IsActive() ||
                !record.Options.IsChongCiZhan || record.Mover.MountAgent == null)
                return;

            try
            {
                record.Mover.UpdateAgentProperties();
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            }
            catch (Exception ex)
            {
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            }
        }

        private void EndRecord(RushRecord record, RushEndReason reason)
        {
            if (record == null || record.Mover == null || !_records.Remove(record.Mover.Index))
                return;

            Agent mover = record.Mover;
            if (mover.IsActive())
            {
                // 后跃没有开启 scripted movement，不能误关 AI 原有的战术移动。
                if (record.Mode != RushMovementMode.ParabolicLeap)
                    mover.DisableScriptedMovement();
                ResetSpeedLimit(mover);
                ResetSpeedLimit(record.LastMount);
                ResetSpeedLimit(mover.MountAgent);
#if false
                // 旧的临时 AI 接管方案已停用，保留恢复分支供对照。
                if (record.TemporarilyAiControlled)
                    mover.Controller = record.OriginalController;
#endif
            }

#if false
            RefreshOnFootChongCiZhanStats(record, "End");
#endif
            RefreshMountedChongCiZhanStats(record, "End");

            /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;

            // 冲刺斩的斩击属于“冲锋结束动作”，不只在成功抵达时发生。超时、
            // 丢失视线、路径失败、卡住、主动取消或枪兵中断都会请求一次原生右砍。
            // 施法者已经失效时无法执行动作；目标失效时仍向最后朝向挥砍。
            if (record.Options.TriggerRightAttackOnEnd && mover.IsActive())
            {
                QueueRightAttack(mover, record.Target);
            }

            try { record.Options.OnEnded?.Invoke(mover, record.Target, reason); }
            catch (Exception ex) { /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */; }
        }

        private void QueueRightAttack(Agent attacker, Agent target)
        {
            _pendingRightAttacks[attacker.Index] = target;
            if (attacker.IsAIControlled)
                attacker.SetHasOnAiInputSetCallback(true);
        }

        public Agent.EventControlFlag OnCollectPlayerEventControlFlags()
        {
            Agent mainAgent = Mission?.MainAgent;
            if (mainAgent == null)
                return Agent.EventControlFlag.None;

#if false
            // 旧方案：这里发生在 MissionMainAgentController 写入玩家移动轴之前，
            // MovementInputVector 会在同一帧稍后被覆盖，所以只能触发攻击，不能冲刺。
            if (mainAgent.Controller == AgentControllerType.Player &&
                _records.TryGetValue(mainAgent.Index, out RushRecord record) &&
                record.DesiredMovementDirection.LengthSquared > 0.001f)
            {
                Vec2 direction = record.DesiredMovementDirection.Normalized();
                mainAgent.LookDirection = direction.ToVec3();
                mainAgent.SetMovementDirection(direction);
                mainAgent.MovementFlags |= Agent.MovementControlFlag.Forward;
                if (!record.InputInjectionLogged)
                {
                    record.InputInjectionLogged = true;
                    /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                }
            }
#endif

            if (_pendingRightAttacks.TryGetValue(mainAgent.Index, out Agent target))
            {
                _pendingRightAttacks.Remove(mainAgent.Index);
                InjectRightAttack(mainAgent, target);
            }
            return Agent.EventControlFlag.None;
        }

        /// <summary>
        /// 在游戏自身的 MissionMainAgentController.ControlTick 完成之后覆写移动轴。
        /// 这样既保留玩家控制器，也不会被本帧的原生输入收集再次清零。
        /// </summary>
        public void ApplyPlayerRushMovementAfterControlTick(Agent mainAgent)
        {
            if (mainAgent == null || mainAgent.Controller != AgentControllerType.Player ||
                !_records.TryGetValue(mainAgent.Index, out RushRecord record) ||
                record.DesiredMovementDirection.LengthSquared <= 0.001f)
                return;

            Vec2 direction = record.DesiredMovementDirection.Normalized();
            float inputMagnitude = 1f;
#if false
            // 已验证引擎会把超出范围的玩家移动轴按普通全速处理，5.0 与 1.0
            // 的实际速度相同。安全连续位移分支负责徒步冲刺速度。
            inputMagnitude = record.Options.IsChongCiZhan && mainAgent.MountAgent == null
                ? Math.Max(1f, record.Options.SpeedLimit)
                : 1f;
#endif
            mainAgent.LookDirection = direction.ToVec3();
            mainAgent.SetMovementDirection(direction);
            // 玩家普通输入会被归一化到 1；仅提高 MaxSpeedMultiplier 不会主动请求
            // 更高速度。冲刺斩徒步分支以技能倍率作为目标输入强度，补足原生 API
            // 没有 SetMinimumSpeedLimit 的缺口。骑乘分支仍保持标准 1，避免坐骑物理失稳。
            mainAgent.MovementInputVector = new Vec2(0f, inputMagnitude);
            mainAgent.MovementFlags &= ~Agent.MovementControlFlag.MoveMask;
            mainAgent.MovementFlags |= Agent.MovementControlFlag.Forward;

            // 只在冲刺开始时发送一次原生“切换奔跑 + 向前双击”。持续写入会反复
            // 触发动作；一次事件足以让步兵进入冲刺步态，并让坐骑触发原生疾驰加速。
            if (!record.NativeSprintTriggered)
            {
                record.NativeSprintTriggered = true;
                if (mainAgent.WalkMode)
                    mainAgent.EventControlFlags |= Agent.EventControlFlag.Run;
                mainAgent.EventControlFlags |= Agent.EventControlFlag.DoubleTapToDirectionUp;
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            }

            if (!record.InputInjectionLogged)
            {
                record.InputInjectionLogged = true;
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            }
        }

        public void ApplyPendingAiAttack(Agent agent, ref Agent.MovementControlFlag movementFlags)
        {
            if (agent == null || !_pendingRightAttacks.TryGetValue(agent.Index, out Agent target))
                return;
            _pendingRightAttacks.Remove(agent.Index);
            FaceTarget(agent, target);
            movementFlags |= Agent.MovementControlFlag.AttackRight;
        }

        private static void InjectRightAttack(Agent attacker, Agent target)
        {
            if (attacker == null || !attacker.IsActive() || !attacker.CombatActionsEnabled)
                return;
            FaceTarget(attacker, target);
            attacker.MovementFlags |= Agent.MovementControlFlag.AttackRight;
        }

        private static void FaceTarget(Agent attacker, Agent target)
        {
            if (target == null || !target.IsActive())
                return;
            Vec2 direction = target.Position.AsVec2 - attacker.Position.AsVec2;
            if (direction.LengthSquared > 0.001f)
                attacker.LookDirection = direction.Normalized().ToVec3();
            attacker.SetLookAgent(target);
            attacker.SetTargetAgent(target);
        }

        public override void OnAgentHit(
            Agent affectedAgent,
            Agent affectorAgent,
            in MissionWeapon affectorWeapon,
            in Blow blow,
            in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, affectorWeapon, blow, attackCollisionData);
            if (affectedAgent == null || affectorAgent == null || affectorWeapon.IsEmpty)
                return;

            Agent rider = affectedAgent.IsMount ? affectedAgent.RiderAgent : affectedAgent;
            if (rider == null || !_records.TryGetValue(rider.Index, out RushRecord record) ||
                record.Mode != RushMovementMode.DirectionalCharge || !record.Options.IsChongCiZhan)
                return;

            WeaponComponentData weapon = affectorWeapon.CurrentUsageItem;
            bool isFootSpearThrust = affectorAgent.MountAgent == null &&
                                     affectorAgent.IsEnemyOf(rider) &&
                                     weapon != null && weapon.IsPolearm &&
                                     blow.StrikeType == StrikeType.Thrust;
            if (isFootSpearThrust)
                EndRecord(record, RushEndReason.SpearInterrupted);
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (affectedAgent == null)
                return;

            if (_records.TryGetValue(affectedAgent.Index, out RushRecord ownRecord))
                EndRecord(ownRecord, RushEndReason.InvalidMover);

            List<RushRecord> snapshot = new List<RushRecord>(_records.Values);
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].Target == affectedAgent)
                    EndRecord(snapshot[i], RushEndReason.InvalidTarget);
            }
            _pendingRightAttacks.Remove(affectedAgent.Index);
        }

        protected override void OnEndMission()
        {
            List<RushRecord> snapshot = new List<RushRecord>(_records.Values);
            for (int i = 0; i < snapshot.Count; i++)
                EndRecord(snapshot[i], RushEndReason.Cancelled);
            _records.Clear();
            _pendingRightAttacks.Clear();
            base.OnEndMission();
        }
    }
}
