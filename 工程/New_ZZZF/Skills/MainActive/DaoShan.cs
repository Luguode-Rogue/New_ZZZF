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
    /// <summary>守望者式刀扇：向周围多个敌人分别发射真实投射物。</summary>
    internal sealed class DaoShan : SkillBase
    {
        private const float Range = 20f;
        private const float UnlimitedRange = 5f;
        private const float MeleeProjectileSpeed = 30f;
        private const int BaseThrowingTargetLimit = 10;
        private const int MeleeTargetLimit = 3;
        private const float AiHighStamina = 80f;
        private const int AiCrowdCount = 3;
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        private readonly List<Agent> _targets = new List<Agent>();

        public DaoShan()
        {
            SkillID = "DaoShan";
            Type = SPSkillType.MainActive;
            Cooldown = 10f;
            ResourceCost = 50f;
            Text = new TextObject("{=ZZZF0010}DaoShan");
            Description = new TextObject("攻击5米内全部敌人，并在20米内优先攻击视线附近的其他敌人。飞刀对每名目标攻击躯干、头和颈部，其余武器攻击躯干。消耗50体力，冷却10秒。");
            Difficulty = null;
        }

        public override bool CanActivateWhilePerformingAction => true;

        public override bool Activate(Agent caster)
        {
            if (!TryPrepareShot(caster, out MissionWeapon weapon, out MissionWeapon projectile,
                    out bool isThrowing, out bool isKnife, out float speed))
                return FailActivation("需要手持投掷武器或近战武器。");

            CollectTargets(caster, false);
            if (_targets.Count == 0)
                return FailActivation("20米内没有可攻击的敌人。");

            int limit = isThrowing ? GetThrowingTargetLimit(caster) : MeleeTargetLimit;
            int outsideCount = 0;
            int created = 0;
            Vec3 start = caster.GetEyeGlobalPosition();
            for (int i = 0; i < _targets.Count; i++)
            {
                Agent target = _targets[i];
                if (!IsTargetable(caster, target))
                    continue;
                bool insideUnlimitedRange = IsInUnlimitedRange(caster, target);
                if (!insideUnlimitedRange)
                {
                    if (outsideCount >= limit)
                        continue;
                    outsideCount++;
                }
                created += FireAtBone(caster, target, weapon, projectile, start,
                    speed, target.Monster.SpineUpperBoneIndex, target.GetChestGlobalPosition());
                if (!isKnife)
                    continue;
                created += FireAtBone(caster, target, weapon, projectile, start,
                    speed, target.Monster.HeadLookDirectionBoneIndex, target.GetEyeGlobalPosition());
                created += FireAtBone(caster, target, weapon, projectile, start,
                    speed, target.Monster.NeckRootBoneIndex,
                    target.AgentVisuals != null
                        ? target.AgentVisuals.GetGlobalStableNeckPoint(true)
                        : (target.GetChestGlobalPosition() + target.GetEyeGlobalPosition()) * 0.5f);
            }

            if (created == 0)
                return FailActivation("刀扇投射物创建失败或没有有效弹道。");
            PlayReleaseAction(caster, weapon.CurrentUsageItem, isThrowing);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) ||
                !TryPrepareShot(caster, out _, out _, out _, out _, out _))
                return false;
            CollectTargets(caster, true);
            if (_targets.Count >= AiCrowdCount)
                return true;
            AgentSkillComponent component = Script.GetActiveComponents(caster);
            return _targets.Count > 0 && component != null &&
                component._currentStamina >= AiHighStamina;
        }

        /// <summary>5米外的投掷武器目标上限；后续在此接入技能熟练度。</summary>
        internal static int GetThrowingTargetLimit(Agent caster)
        {
            return BaseThrowingTargetLimit;
        }

        private void CollectTargets(Agent caster, bool requireLineOfSight)
        {
            _targets.Clear();
            _nearby.Clear();
            if (caster?.Mission == null)
                return;
            if (caster.Team != null)
                caster.Mission.GetNearbyEnemyAgents(caster.Position.AsVec2,
                    Range, caster.Team, _nearby);
            else
                caster.Mission.GetNearbyAgents(caster.Position.AsVec2, Range, _nearby);

            Vec3 start = caster.GetEyeGlobalPosition();
            foreach (Agent target in _nearby)
            {
                if (!IsTargetable(caster, target) ||
                    (target.GetChestGlobalPosition() - start).Length > Range ||
                    (requireLineOfSight &&
                     !RushMovementMissionLogic.HasLineOfSight(caster, target)))
                    continue;
                _targets.Add(target);
            }

            Vec3 look = GetLookDirection(caster);
            if (!look.IsValid || look.LengthSquared < 0.001f)
                look = caster.LookDirection;
            if (look.LengthSquared < 0.001f)
                look = new Vec3(0f, 1f, 0f);
            look.Normalize();
            _targets.Sort((a, b) =>
            {
                Vec3 aDelta = a.GetChestGlobalPosition() - start;
                Vec3 bDelta = b.GetChestGlobalPosition() - start;
                bool aInside = IsInUnlimitedRange(caster, a);
                bool bInside = IsInUnlimitedRange(caster, b);
                if (aInside != bInside)
                    return aInside ? -1 : 1;
                float aAlignment = aDelta.LengthSquared > 0.001f
                    ? Vec3.DotProduct(look, aDelta.NormalizedCopy()) : -1f;
                float bAlignment = bDelta.LengthSquared > 0.001f
                    ? Vec3.DotProduct(look, bDelta.NormalizedCopy()) : -1f;
                int alignmentOrder = bAlignment.CompareTo(aAlignment);
                return alignmentOrder != 0 ? alignmentOrder :
                    aDelta.LengthSquared.CompareTo(bDelta.LengthSquared);
            });
        }

        private static bool IsInUnlimitedRange(Agent caster, Agent target)
        {
            return (target.Position - caster.Position).AsVec2.LengthSquared <=
                UnlimitedRange * UnlimitedRange;
        }

        private static bool IsTargetable(Agent caster, Agent target)
        {
            if (target == null || target == caster || !target.IsHuman ||
                !target.IsActive() || target.Health <= 0f || !caster.IsEnemyOf(target))
                return false;
            AgentSkillComponent component = Script.GetActiveComponents(target);
            return component == null || !component.StateContainer.HasState("BKBBuff");
        }

        private static bool TryPrepareShot(Agent caster, out MissionWeapon weapon,
            out MissionWeapon projectile, out bool isThrowing, out bool isKnife,
            out float speed)
        {
            weapon = MissionWeapon.Invalid;
            projectile = MissionWeapon.Invalid;
            isThrowing = false;
            isKnife = false;
            speed = 0f;
            if (caster == null || !caster.IsActive() || caster.Mission == null ||
                caster.Equipment == null)
                return false;
            EquipmentIndex slot = caster.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None)
                return false;
            weapon = caster.Equipment[slot];
            WeaponComponentData usage = weapon.CurrentUsageItem;
            if (weapon.IsEmpty || usage == null)
                return false;

            isThrowing = usage.IsRangedWeapon && usage.IsConsumable;
            isKnife = isThrowing && usage.WeaponClass == WeaponClass.ThrowingKnife;
            if (isThrowing)
            {
                projectile = weapon;
                speed = weapon.GetModifiedMissileSpeedForCurrentUsage();
                if (speed <= 0.01f || float.IsNaN(speed) || float.IsInfinity(speed))
                    speed = MeleeProjectileSpeed;
                return true;
            }
            if (!usage.IsMeleeWeapon || usage.IsRangedWeapon)
                return false;

            ItemObject fallback = Game.Current?.ObjectManager?.GetObject<ItemObject>(
                "western_javelin_1_t2");
            if (fallback == null)
                return false;
            projectile = new MissionWeapon(fallback, null, null, 1);
            speed = MeleeProjectileSpeed;
            return true;
        }

        private static int FireAtBone(Agent caster, Agent target, MissionWeapon weapon,
            MissionWeapon projectile, Vec3 start, float speed, sbyte boneIndex,
            Vec3 fallbackPosition)
        {
            Vec3 impact = GetBoneWorldPosition(target, boneIndex, fallbackPosition);
            Vec3 velocity = target.MovementVelocity.ToVec3();
            if (SkillSystemBehavior.ActiveComponents.TryGetValue(
                    target.Index, out AgentSkillComponent component) &&
                component?.Speed != null && component.Speed.speed.IsValid &&
                component.Speed.speed.LengthSquared <= 2500f)
                velocity = component.Speed.speed;
            if (!velocity.IsValid || velocity.LengthSquared > 2500f)
                velocity = Vec3.Zero;
            Vec3 direction = Vec3.Invalid;
            for (int i = 0; i < 2; i++)
            {
                direction = Script.CalculateProjectileFiringSolution(
                    start, impact, speed, 9.81f);
                if (!direction.IsValid || direction.LengthSquared < 0.01f)
                    return 0;
                float horizontalSpeed = speed * direction.AsVec2.Length;
                if (horizontalSpeed < 0.01f)
                    break;
                float flightTime = (impact - start).AsVec2.Length / horizontalSpeed;
                impact = GetBoneWorldPosition(target, boneIndex, fallbackPosition) +
                    velocity * flightTime;
            }
            // 交给通用射击函数按目标点求最终弹道；保留骨骼坐标，不应用旧投掷物的向下偏移。
            return Script.FireProjectileFromAgentWithWeaponAtPosition(
                caster, weapon, projectile, start, impact, speed,
                forceTargetPosition: true) > 0 ? 1 : 0;
        }

        private static Vec3 GetBoneWorldPosition(Agent target, sbyte boneIndex,
            Vec3 fallback)
        {
            if (boneIndex < 0 || target.AgentVisuals == null)
                return fallback;
            Skeleton skeleton = target.AgentVisuals.GetSkeleton();
            if (skeleton == null)
                return fallback;
            MatrixFrame boneFrame = skeleton.GetBoneEntitialFrame(boneIndex);
            Vec3 position = target.AgentVisuals.GetGlobalFrame()
                .TransformToParent(boneFrame.origin);
            return position.IsValid ? position : fallback;
        }

        private static Vec3 GetLookDirection(Agent caster)
        {
            MissionScreen screen = ScreenManager.TopScreen as MissionScreen;
            return caster.IsMainAgent && screen != null
                ? screen.CombatCamera.Direction : caster.LookDirection;
        }

        private static void PlayReleaseAction(Agent caster, WeaponComponentData usage,
            bool isThrowing)
        {
            if (caster.MountAgent == null)
            {
                string turnAction;
                if (isThrowing)
                    turnAction = "act_turn_thrown";
                else if (usage.WeaponClass == WeaponClass.OneHandedPolearm ||
                         usage.WeaponClass == WeaponClass.TwoHandedPolearm)
                    turnAction = "act_turn_polearm";
                else
                    turnAction = usage.IsTwoHanded ? "act_turn_2h" : "act_turn_1h";
                caster.SetActionChannel(0, ActionIndexCache.Create(turnAction));
            }
            // 上半身使用对应武器的原生释放动作与音效；不生成额外原生攻击。
            New_ZZZF.Skills.MagicShoot.PlayReleasePresentation(caster);
        }
    }
}
