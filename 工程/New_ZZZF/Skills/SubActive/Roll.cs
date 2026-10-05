using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF
{
    internal class Roll : SkillBase
    {
        private const float RollDistance = 7f;
        private const float RollDuration = 1.25f;
        private const float Clearance = 0.35f;
        private const float MinimumDistance = 0.65f;
        private const float ThreatRange = 5f;
        internal static readonly ActionIndexCache RollAction = ActionIndexCache.Create("act_horse_fall_roll");
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        private readonly Dictionary<int, EvadePlan> _plans = new Dictionary<int, EvadePlan>();
        private Mission _mission;
        private static readonly float[] CandidateAngles = { 0f, 45f, -45f, 90f, -90f, 180f };
        private sealed class EvadePlan
        {
            public Agent Agent;
            public Vec3 Start;
            public Vec3 Destination;
            public float Expires;
        }

        public Roll()
        {
            SkillID = "Roll";
            Type = SPSkillType.SubActive;
            Cooldown = 2f;
            ResourceCost = 10f;
            Text = new TextObject("{=ZZZF_ROLL_NAME}翻滚");
            Description = new TextObject("{=ZZZF_ROLL_DESC}朝移动方向翻滚，期间免除直接攻击伤害；清除火焰持续伤害，其他持续伤害剩余时间减半。无输入时向前，骑乘不可使用。消耗耐力10，冷却2秒。");
            Difficulty = null;
        }
        internal void ClearAiCache()
        {
            _plans.Clear();
            _nearby.Clear();
            _mission = null;
        }
        protected virtual bool HasActiveEffects => true;
        public override bool CanActivateWhilePerformingAction => true;

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || caster.IsPlayerControlled || caster.MountAgent != null ||
                RushMovementMissionLogic.Current?.IsRushing(caster) == true) return false;
            return TryGetAiPlan(caster, out _);
        }

        public override bool Activate(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return FailActivation("施法者不可用。");
            if (agent.MountAgent != null) return FailActivation("骑乘时无法翻滚。");
            var movement = RushMovementMissionLogic.Current;
            if (movement == null || movement.IsRushing(agent)) return FailActivation("当前无法开始翻滚。");
            Vec3 destination;
            if (agent.IsPlayerControlled || agent.IsMainAgent)
            {
                if (!TryResolveDestination(agent, GetPlayerDirection(agent), out destination))
                    return FailActivation("翻滚方向空间不足或无法落脚。");
            }
            else
            {
                if (!TryGetAiPlan(agent, out EvadePlan plan)) return FailActivation("没有需要规避的近战威胁。");
                destination = plan.Destination;
                _plans.Remove(agent.Index);
            }

            // 按该动作在当前 ActionSet 中的实际时长缩放，保持剩余动作与位移同步。
            float animationDuration = MBActionSet.GetActionAnimationDuration(agent.ActionSet, in RollAction);
            if (float.IsNaN(animationDuration) || float.IsInfinity(animationDuration) || animationDuration <= 0f)
                return FailActivation("当前模型没有可用翻滚动作。");
            const float startProgress = 0.3f;
            float actionSpeed = animationDuration * (1f - startProgress) / RollDuration;
            float travelDistance = (destination - agent.Position).AsVec2.Length;
            var options = new RushMovementOptions {
                Duration = RollDuration, StopDistance = Clearance,
                SpeedLimit = 15f, AllowMounted = false, UseSafeKinematicMovement = true,
                KinematicSpeed = (travelDistance + Clearance) / RollDuration,
                MaximumKinematicSlopeDegrees = 45f, IsRoll = HasActiveEffects, IsRollMovement = true, PreserveFacing = true
            };
            if (!movement.TryRushToPosition(agent, destination, options, out string reason))
                return FailActivation(reason ?? "落点不可到达。");

            bool accepted = agent.SetActionChannel(0, RollAction, true,
                AnimFlags.amf_priority_jump_loop | AnimFlags.anf_restart,
                0f, actionSpeed, 0.05f, 0.05f, startProgress, false, 0f, 0, false);
            if (!accepted) {
                movement.CancelRush(agent, RushEndReason.Cancelled);
                return FailActivation("当前姿态无法翻滚。");
            }
            agent.EventControlFlags &= ~Agent.EventControlFlag.Jump;
            // 两个版本都播放翻滚动作；被动版本不启用免伤，并在动作成功后跳过净化和音效。
            if (!HasActiveEffects) return true;
            agent.GetComponent<AgentSkillComponent>()?.StateContainer.ReduceDamageOverTimeForRoll();
            WeaponCombatMissionLogic.Current?.HalveRemainingBleedDuration(agent);
            // 不创建循环音源或粒子实体。
            try { SoundManager.StartOneShotEvent("event:/mission/combat/blunt/footstep", agent.Position); } catch (Exception) { }
            return true;
        }

        private static Vec2 GetPlayerDirection(Agent agent)
        {
            var screen = ScreenManager.TopScreen as MissionScreen;
            var input = screen?.SceneLayer?.Input;
            float forward = (input?.IsGameKeyDown(0) == true ? 1f : 0f) -
                (input?.IsGameKeyDown(1) == true ? 1f : 0f);
            float side = (input?.IsGameKeyDown(3) == true ? 1f : 0f) -
                (input?.IsGameKeyDown(2) == true ? 1f : 0f);
            Vec2 facing = agent.LookDirection.AsVec2;
            if (facing.LengthSquared < 0.001f) facing = Vec3.Forward.AsVec2;
            facing.Normalize();
            if (forward == 0f && side == 0f) return facing;
            // 原版正角旋转对应左侧；右侧向量为 (y,-x)。
            Vec2 direction = facing * forward + new Vec2(facing.y, -facing.x) * side;
            direction.Normalize();
            return direction;
        }

        private bool TryGetAiPlan(Agent caster, out EvadePlan plan)
        {
            plan = null;
            Mission mission = caster.Mission;
            if (mission == null) return false;
            if (_mission != mission) { _plans.Clear(); _nearby.Clear(); _mission = mission; }
            if (_plans.TryGetValue(caster.Index, out plan) && plan.Agent == caster &&
                plan.Expires >= mission.CurrentTime && (caster.Position - plan.Start).LengthSquared < 0.0625f)
                return true;
            _plans.Remove(caster.Index);
            plan = null;
            _nearby.Clear();
            // 同一查询覆盖近身威胁与7米落点附近敌人；CheckCondition/Activate复用计划。
            if (caster.Team != null) mission.GetNearbyEnemyAgents(caster.Position.AsVec2, 12f, caster.Team, _nearby);
            else mission.GetNearbyAgents(caster.Position.AsVec2, 12f, _nearby);
            Vec2 away = Vec2.Zero;
            int threats = 0, targeting = 0;
            bool incomingAttack = false;
            foreach (Agent enemy in _nearby)
            {
                if (!ValidEnemy(caster, enemy)) continue;
                Vec2 offset = caster.Position.AsVec2 - enemy.Position.AsVec2;
                float distance2 = offset.LengthSquared;
                if (distance2 > ThreatRange * ThreatRange || Math.Abs(enemy.Position.z - caster.Position.z) > 3f) continue;
                EquipmentIndex slot = enemy.GetPrimaryWieldedItemIndex();
                if (slot == EquipmentIndex.None || enemy.Equipment == null ||
                    enemy.Equipment[slot].CurrentUsageItem?.IsMeleeWeapon != true) continue;
                bool targetsCaster = enemy.GetTargetAgent() == caster;
                if (targetsCaster) targeting++;
                threats++;
                var action = enemy.GetCurrentActionType(1);
                bool attacking = action == Agent.ActionCodeType.AttackMeleeAllBegin ||
                    action == Agent.ActionCodeType.ReadyMelee || action == Agent.ActionCodeType.ReleaseMelee;
                Vec2 towardCaster = offset;
                if (towardCaster.LengthSquared > 0.001f) towardCaster.Normalize();
                bool facesCaster = Vec2.DotProduct(enemy.LookDirection.AsVec2, towardCaster) > 0.5f;
                incomingAttack |= attacking && (targetsCaster || distance2 <= 7.5625f && facesCaster);
                away += offset / MathF.Max(0.5f, distance2);
            }
            bool lowHealth = caster.HealthLimit > 0f && caster.Health <= caster.HealthLimit * 0.5f;
            if (!incomingAttack && !(targeting > 0 && (lowHealth || threats >= 3))) return false;
            if (away.LengthSquared < 0.001f) away = -caster.LookDirection.AsVec2;
            if (away.LengthSquared < 0.001f) return false;
            away.Normalize();
            float bestScore = float.NegativeInfinity;
            Vec3 best = Vec3.Invalid;
            foreach (float angle in CandidateAngles)
            {
                Vec3 rotated = away.ToVec3();
                rotated.RotateAboutZ(angle * MathF.PI / 180f);
                if (!TryResolveDestination(caster, rotated.AsVec2, out Vec3 candidate)) continue;
                float score = (candidate - caster.Position).AsVec2.Length + Vec2.DotProduct(rotated.AsVec2, away) * 2f;
                foreach (Agent enemy in _nearby) {
                    if (!ValidEnemy(caster, enemy) || Math.Abs(enemy.Position.z - candidate.z) > 3f) continue;
                    float d2 = (enemy.Position - candidate).AsVec2.LengthSquared;
                    if (d2 < 9f) score -= (9f - d2) * 2f;
                }
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            if (!best.IsValid) return false;
            plan = new EvadePlan { Agent = caster, Start = caster.Position, Destination = best,
                Expires = mission.CurrentTime + 0.15f };
            _plans[caster.Index] = plan;
            return true;
        }

        private static bool ValidEnemy(Agent caster, Agent enemy) => enemy != null && enemy != caster &&
            enemy.IsHuman && enemy.IsActive() && enemy.Health > 0f && caster.IsEnemyOf(enemy);

        private static bool TryResolveDestination(Agent agent, Vec2 direction, out Vec3 destination)
        {
            destination = Vec3.Invalid;
            Scene scene = agent.Mission?.Scene;
            if (scene == null || direction.LengthSquared < 0.001f) return false;
            direction.Normalize();
            float distance = RollDistance;
            Vec3 rayStart = agent.Position + Vec3.Up * 0.8f;
            Vec3 rayEnd = rayStart + direction.ToVec3() * distance;
            float hitDistance;
            var visuals = agent.AgentVisuals;
            bool hit = visuals != null
                ? scene.RayCastForClosestEntityOrTerrainIgnoreEntity(rayStart, rayEnd,
                    visuals.GetWeakEntity(), out hitDistance, out _, 0.01f, BodyFlags.CommonCollisionExcludeFlags)
                : scene.RayCastForClosestEntityOrTerrain(rayStart, rayEnd, out hitDistance, 0.01f, BodyFlags.CommonCollisionExcludeFlags);
            if (hit) distance = MathF.Max(0f, hitDistance - Clearance);
            // 缩短落点而不是把不可达的7米落点直接判为失败。
            for (; distance >= MinimumDistance; distance -= 0.5f)
            {
                Vec3 candidate = agent.Position + direction.ToVec3() * distance;
                Vec3 top = candidate + Vec3.Up * 1.5f, bottom = candidate - Vec3.Up * 1.5f;
                Vec3 surface;
                bool supported;
                if (visuals != null)
                {
                    // IgnoreEntity 的第五个 out 是 GameEntity；垂直射线由命中距离还原落脚点。
                    supported = scene.RayCastForClosestEntityOrTerrainIgnoreEntity(top, bottom,
                        visuals.GetWeakEntity(), out float supportDistance, out _,
                        0.01f, BodyFlags.CommonCollisionExcludeFlags);
                    surface = top;
                    surface.z -= supportDistance;
                    supported &= surface.IsValid;
                }
                else
                {
                    supported = scene.RayCastForClosestEntityOrTerrain(top, bottom, out _, out surface,
                        0.01f, BodyFlags.CommonCollisionExcludeFlags);
                }
                if (!supported) continue;
                candidate.z = surface.z;
                if (Math.Abs(candidate.z - agent.Position.z) > distance + 0.1f ||
                    scene.GetNavigationMeshForPosition(candidate, out _, 0.35f, false) == UIntPtr.Zero) continue;
                destination = candidate;
                return destination.IsValid;
            }
            return false;
        }
    }
}