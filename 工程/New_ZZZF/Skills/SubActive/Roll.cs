using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 翻滚：玩家按移动输入选择八方向；AI 朝主要近战威胁的反方向脱离。
    /// 实际位移交给 RushMovementMissionLogic，技能只负责方向、障碍和动作。
    /// </summary>
    internal sealed class Roll : SkillBase
    {
        private const float RollDistance = 7f;
        private const float RollDuration = 1.25f;
        private const float RollSpeed = 15f;
        private const float ObstacleClearance = 0.35f;
        private const float MinimumRollDistance = 0.65f;
        private const float AiThreatRange = 5f;

        public Roll()
        {
            SkillID = "Roll";
            Type = SPSkillType.SubActive;
            Cooldown = 2f;
            ResourceCost = 3f;
            Text = new TextObject("{=ZZZF_ROLL_NAME}翻滚");
            Description = new TextObject(
                "{=ZZZF_ROLL_DESC}朝移动方向快速翻滚；没有移动输入时默认向前。"
                + "翻滚会在障碍物前停下，骑乘时无法使用。消耗耐力：3。冷却时间：2秒。");
            Difficulty = null;
        }

        public override bool CanActivateWhilePerformingAction => true;

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || caster.IsPlayerControlled ||
                caster.MountAgent != null || RushMovementMissionLogic.Current?.IsRushing(caster) == true)
                return false;

            return TryGetAiEvadeDirection(caster, out _, out _);
        }

        public override bool Activate(Agent agent)
        {
            if (agent == null || !agent.IsActive())
                return FailActivation("施法者当前不可用。");
            if (agent.MountAgent != null)
                return FailActivation("骑乘状态下无法翻滚。");

            RushMovementMissionLogic movement = RushMovementMissionLogic.Current;
            if (movement == null)
                return FailActivation("当前任务未加载强制移动管理器。");
            if (movement.IsRushing(agent))
                return FailActivation("当前正在执行其他强制位移。");

            Vec2 direction;
            if (agent.IsPlayerControlled || agent.IsMainAgent)
            {
                direction = GetPlayerRollDirection(agent);
            }
            else if (!TryGetAiEvadeDirection(agent, out direction, out _))
            {
                return FailActivation("附近没有需要规避的有效威胁。");
            }

            if (direction.LengthSquared < 0.001f)
                return FailActivation("无法确定翻滚方向。");
            direction.Normalize();

            if (!TryResolveDestination(agent, direction, out Vec3 destination, out string destinationFailure))
                return FailActivation(destinationFailure);

            var options = new RushMovementOptions
            {
                Duration = RollDuration,
                StopDistance = ObstacleClearance,
                SpeedLimit = RollSpeed,
                SpeedLimitIsMultiplier = false,
                AllowMounted = false,
                UseSafeKinematicMovement = true,
                KinematicSpeed = RollDistance / RollDuration
            };
            if (!movement.TryRushToPosition(agent, destination, options, out string movementFailure))
                return FailActivation(movementFailure ?? "翻滚目标地点不可到达。");

            bool actionAccepted = agent.SetActionChannel(
                0, ActionIndexCache.Create("act_horse_fall_roll"), false,
                (AnimFlags)0UL, 0f, 1f, -0.2f, 0.4f, 0.25f, false, 0f, 0, false);
            if (!actionAccepted)
            {
                movement.CancelRush(agent, RushEndReason.Cancelled);
                return FailActivation("当前姿态无法执行翻滚动作。");
            }

            agent.SetCurrentActionProgress(0, 0.3f);
            agent.SetCurrentActionSpeed(0, 2f);
            Debug.Print(string.Format(
                "[New_ZZZF][翻滚][Start] agent={0}, ai={1}, direction={2}, destination={3}",
                agent.Name, agent.IsAIControlled, direction, destination));
            return true;
        }

        private static Vec2 GetPlayerRollDirection(Agent agent)
        {
            Vec3 direction = agent.LookDirection;
            direction.z = 0f;
            if (direction.AsVec2.LengthSquared < 0.001f)
                direction = Vec3.Forward;

            bool forward = Input.IsKeyDown(InputKey.W);
            bool backward = Input.IsKeyDown(InputKey.S);
            bool left = Input.IsKeyDown(InputKey.A);
            bool right = Input.IsKeyDown(InputKey.D);

            float angle = 0f;
            if (forward && left) angle = 45f;
            else if (backward && left) angle = 135f;
            else if (forward && right) angle = -45f;
            else if (backward && right) angle = -135f;
            else if (backward) angle = 180f;
            else if (left) angle = 90f;
            else if (right) angle = -90f;

            direction.RotateAboutZ(angle * (MathF.PI / 180f));
            return direction.AsVec2.Normalized();
        }

        private static bool TryGetAiEvadeDirection(
            Agent caster,
            out Vec2 evadeDirection,
            out int threatCount)
        {
            evadeDirection = Vec2.Zero;
            threatCount = 0;
            Mission mission = Mission.Current;
            if (caster == null || mission == null)
                return false;

            float rangeSquared = AiThreatRange * AiThreatRange;
            bool immediateThreat = false;
            foreach (Agent other in mission.Agents)
            {
                if (other == null || other == caster || !other.IsActive() ||
                    !other.IsHuman || other.IsMount || !caster.IsEnemyOf(other))
                    continue;

                Vec2 away = caster.Position.AsVec2 - other.Position.AsVec2;
                float distanceSquared = away.LengthSquared;
                if (distanceSquared > rangeSquared || distanceSquared < 0.0001f)
                    continue;

                bool targetsCaster = other.GetTargetAgent() == caster;
                bool veryClose = distanceSquared <= 7.5625f;
                if (!targetsCaster && !veryClose)
                    continue;

                Agent.ActionCodeType actionType = other.GetCurrentActionType(1);
                bool isAttacking = actionType == Agent.ActionCodeType.AttackMeleeAllBegin ||
                    actionType == Agent.ActionCodeType.ReadyMelee ||
                    actionType == Agent.ActionCodeType.ReleaseMelee;

                threatCount++;
                immediateThreat |= veryClose || (targetsCaster && isAttacking);
                evadeDirection += away / MathF.Max(0.5f, distanceSquared);
            }

            bool lowHealth = caster.HealthLimit > 0f && caster.Health < caster.HealthLimit * 0.5f;
            if (threatCount == 0 || (!immediateThreat && threatCount < 2 && !lowHealth))
                return false;

            if (evadeDirection.LengthSquared < 0.001f)
                evadeDirection = -caster.LookDirection.AsVec2;
            if (evadeDirection.LengthSquared < 0.001f)
                return false;

            evadeDirection.Normalize();
            return true;
        }

        private static bool TryResolveDestination(
            Agent agent,
            Vec2 direction,
            out Vec3 destination,
            out string failureReason)
        {
            destination = Vec3.Invalid;
            failureReason = null;
            Scene scene = Mission.Current?.Scene;
            if (scene == null)
            {
                failureReason = "当前任务没有可用场景。";
                return false;
            }

            Vec3 planarDirection = direction.ToVec3();
            Vec3 requested = agent.Position + planarDirection * RollDistance;
            Vec3 rayStart = agent.Position + Vec3.Up * 0.8f;
            Vec3 rayEnd = requested + Vec3.Up * 0.8f;
            float availableDistance = RollDistance;
            if (scene.RayCastForClosestEntityOrTerrain(
                    rayStart, rayEnd, out float collisionDistance, 0.01f,
                    BodyFlags.CommonCollisionExcludeFlags))
            {
                availableDistance = MathF.Max(0f, collisionDistance - ObstacleClearance);
            }

            if (availableDistance < MinimumRollDistance)
            {
                failureReason = "翻滚方向空间不足。";
                return false;
            }

            destination = agent.Position + planarDirection * availableDistance;
            destination.z = scene.GetGroundHeightAtPosition(
                destination, BodyFlags.CommonCollisionExcludeFlags);
            return destination.IsValid;
        }
    }
}
