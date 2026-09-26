using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF.Skills
{
    /// <summary>使用原生弹丸的霰弹技能。弹丸数量由 GetPelletCount 统一提供。</summary>
    internal sealed class ConeOfArrows : SkillBase
    {
        private const int BasePelletCount = 10;

        private const float HalfAngleDegrees = 3f;
        private const float AiMaximumRange = 180f;
        private const float AiMinimumAimDot = 0.99f;
        private readonly MBList<Agent> _nearbyEnemies = new MBList<Agent>();

        public ConeOfArrows()
        {
            SkillID = "ConeOfArrows";
            Type = SPSkillType.MainActive;
            Cooldown = 10f;
            ResourceCost = 30f;
            Text = new TextObject("{=ZZZF0008}ConeOfArrows");
            Description = new TextObject("向视野方向射出霰弹状散布的弹丸；可覆盖多个敌人，近距离可多次命中同一目标。消耗30体力，冷却10秒。");
            Difficulty = null;
        }

        public override bool CanActivateWhilePerformingAction => true;

        public override bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area)
        {
            area = default;
            if (index != 0 || !TryGetShot(caster, out _, out _, out _))
                return false;
            area = new SkillDamageArea
            {
                Shape = SkillDamageAreaShape.SampledCone,
                Length = AiMaximumRange,
                Radius = 0.1f,
                HalfAngleDegrees = HalfAngleDegrees
            };
            return true;
        }

        public override bool Activate(Agent caster)
        {
            if (!TryGetShot(caster, out MissionWeapon weapon, out MissionWeapon ammo, out float speed))
                return FailActivation("需要有效远程武器、匹配弹药及原生射击弹速记录。");

            Vec3 forward;
            if (!caster.IsPlayerControlled)
            {
                if (!TryChooseAiAim(caster, speed, out forward))
                    return FailActivation("当前没有适合霰弹射击的目标。");
            }
            else
            {
                forward = GetFireDirection(caster);
            }
            if (!forward.IsValid || forward.LengthSquared < 0.01f)
                return FailActivation("射击方向无效。");
            forward.Normalize();
            Vec3 right = Vec3.CrossProduct(forward, Vec3.Up);
            if (right.LengthSquared < 0.001f)
                return FailActivation("射击方向无效。");
            right.Normalize();
            Vec3 up = Vec3.CrossProduct(right, forward);
            up.Normalize();

            Vec3 start = caster.GetEyeGlobalPosition();
            float spread = (float)Math.Tan(HalfAngleDegrees * Math.PI / 180.0);
            float phase = MBRandom.RandomFloat * (float)(Math.PI * 2.0);
            int pelletCount = GetPelletCount(caster);
            GetRingCounts(pelletCount, out int centerCount, out int innerCount, out int outerCount);
            int created = 0;
            for (int i = 0; i < pelletCount; i++)
            {
                // 基准十枚是中心1、内圈3、外圈6；数量变化时按同一比例分配。
                float radius = i < centerCount ? 0f : i < centerCount + innerCount ? 0.35f : 1f;
                int ringIndex = i < centerCount + innerCount ? i - centerCount : i - centerCount - innerCount;
                int ringCount = i < centerCount + innerCount ? innerCount : outerCount;
                float angle = radius == 0f ? 0f : phase +
                    (float)(Math.PI * 2.0 * ringIndex / ringCount);
                Vec3 direction = forward +
                    right * (spread * radius * (float)Math.Cos(angle)) +
                    up * (spread * radius * (float)Math.Sin(angle));
                direction.Normalize();
                if (Script.FireProjectileFromAgentWithWeaponAtPosition(
                        caster, weapon, ammo, start, direction, speed) > 0)
                    created++;
            }

            if (created == 0)
                return FailActivation("弹丸创建失败。");
            MagicShoot.PlayReleasePresentation(caster);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            return base.CheckCondition(caster) &&
                TryGetShot(caster, out _, out _, out float speed) &&
                TryChooseAiAim(caster, speed, out _);
        }

        /// <summary>技能熟练度接入点；目前保持原技能的十枚基准。</summary>
        internal static int GetPelletCount(Agent caster)
        {
            return BasePelletCount;
        }

        private static void GetRingCounts(int total, out int center, out int inner, out int outer)
        {
            total = Math.Max(1, total);
            center = Math.Max(1, (int)Math.Round(total * 0.1f));
            inner = Math.Min(total - center, (int)Math.Round(total * 0.3f));
            outer = total - center - inner;
        }

        private bool TryChooseAiAim(Agent caster, float speed, out Vec3 direction)
        {
            direction = Vec3.Invalid;
            Agent target = caster.GetTargetAgent();
            if (!IsUsableEnemy(caster, target))
                return false;

            Vec3 start = caster.GetEyeGlobalPosition();
            Vec3 forward = caster.LookDirection;
            Vec3 targetDelta = target.GetEyeGlobalPosition() - start;
            float distance = targetDelta.Length;
            if (distance < 0.1f || distance > AiMaximumRange ||
                forward.LengthSquared < 0.01f ||
                Vec3.DotProduct(forward.NormalizedCopy(), targetDelta / distance) < AiMinimumAimDot ||
                !RushMovementMissionLogic.HasLineOfSight(caster, target))
                return false;

            Vec3 targetPosition = target.GetEyeGlobalPosition();
            Vec3 singleDirection = Script.CalculateProjectileFiringSolution(
                start, targetPosition, speed, 9.81f);
            if (!singleDirection.IsValid || singleDirection.LengthSquared <= 0.01f)
                return false;

            // 先尝试把当前目标和邻近敌人一同罩住；没有多人机会时仍对当前目标射击。
            float spreadAtTarget = distance *
                (float)Math.Tan(HalfAngleDegrees * Math.PI / 180.0);
            float queryRadius = 0.5f + 2f * spreadAtTarget;
            _nearbyEnemies.Clear();
            if (caster.Team != null)
                caster.Mission.GetNearbyEnemyAgents(target.Position.AsVec2,
                    queryRadius, caster.Team, _nearbyEnemies);
            else
                caster.Mission.GetNearbyAgents(target.Position.AsVec2,
                    queryRadius, _nearbyEnemies);

            Vec3 targetRay = targetDelta / distance;
            Vec3 groupCenter = targetPosition;
            int covered = 1;
            foreach (Agent enemy in _nearbyEnemies)
            {
                if (enemy == target || !IsUsableEnemy(caster, enemy))
                    continue;
                Vec3 delta = enemy.GetEyeGlobalPosition() - start;
                float depth = Vec3.DotProduct(targetRay, delta);
                float lateralDistance = (delta - targetRay * depth).Length;
                float coverageWidth = 2f * (0.15f + depth *
                    (float)Math.Tan(HalfAngleDegrees * Math.PI / 180.0));
                if (depth <= 0f || delta.Length > AiMaximumRange ||
                    lateralDistance > coverageWidth ||
                    !RushMovementMissionLogic.HasLineOfSight(caster, enemy))
                    continue;
                groupCenter += enemy.GetEyeGlobalPosition();
                covered++;
            }

            if (covered >= 2)
            {
                Vec3 groupDirection = Script.CalculateProjectileFiringSolution(
                    start, groupCenter / covered, speed, 9.81f);
                if (groupDirection.IsValid && groupDirection.LengthSquared > 0.01f)
                {
                    direction = groupDirection;
                    return true;
                }
            }
            direction = singleDirection;
            return true;
        }

        private static bool TryGetShot(Agent caster, out MissionWeapon weapon,
            out MissionWeapon ammo, out float speed)
        {
            weapon = MissionWeapon.Invalid;
            ammo = MissionWeapon.Invalid;
            speed = 0f;
            if (caster == null || !caster.IsActive() || caster.Mission == null ||
                caster.Equipment == null)
                return false;
            EquipmentIndex slot = caster.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None)
                return false;
            weapon = caster.Equipment[slot];
            WeaponComponentData usage = weapon.CurrentUsageItem;
            if (weapon.IsEmpty || usage == null ||
                !(usage.WeaponClass == WeaponClass.Bow ||
                  usage.WeaponClass == WeaponClass.Crossbow ||
                  (1 == 0 && usage.IsRangedWeapon && usage.IsConsumable)) ||
                !Script.TryGetActualAmmoWeapon(caster, weapon, out ammo))
                return false;

            // 只接受原生射击回调测得的实际弹速；没有记录时不能发动。
            if (SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(
                    caster.Index, out var speeds) && speeds != null)
            {
                foreach (AgentMissileSpeedData data in speeds)
                {
                    if (data.Weapon.Item.Id == weapon.Item.Id &&
                        data.MissileSpeed > 0.01f &&
                        !float.IsNaN(data.MissileSpeed) &&
                        !float.IsInfinity(data.MissileSpeed))
                    {
                        speed = data.MissileSpeed;
                        break;
                    }
                }
            }
            return speed > 0.01f && !float.IsNaN(speed) && !float.IsInfinity(speed);
        }

        private static Vec3 GetFireDirection(Agent caster)
        {
            MissionScreen screen = ScreenManager.TopScreen as MissionScreen;
            Vec3 direction = caster.IsMainAgent && screen != null
                ? screen.CombatCamera.Direction : caster.LookDirection;
            return direction.IsValid && direction.LengthSquared >= 0.01f
                ? direction : caster.LookDirection;
        }

        private static bool IsUsableEnemy(Agent caster, Agent target)
        {
            return target != null && target != caster && target.IsActive() &&
                target.Health > 0f && caster.IsEnemyOf(target);
        }
    }
}
