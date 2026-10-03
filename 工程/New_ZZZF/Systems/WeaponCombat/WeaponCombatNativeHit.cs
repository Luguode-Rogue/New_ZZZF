using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal static class WeaponCombatNativeHit
    {
        internal static AttackCollisionData Collision(Agent attacker, Agent victim, MissionWeapon weapon,
            in AttackCollisionData original, float progress, float impactDistance, Vec3 position, Vec3 direction, Vec3 weaponAxis)
        {
            return AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                false, false, false, true, false, false, false, false, false, original.ThrustTipHit, false, false,
                CombatCollisionResult.StrikeAgent, (int)attacker.GetPrimaryWieldedItemIndex(), (int)StrikeType.Thrust,
                (int)weapon.CurrentUsageItem.ThrustDamageType, original.CollisionBoneIndex, original.VictimHitBodyPart,
                attacker.Monster.MainHandItemBoneIndex, attacker.GetCurrentActionDirection(1), -1,
                CombatHitResultFlags.NormalHit, progress, impactDistance,
                0f, 0f, 0f, 0f, 0f, 0f, weaponAxis, direction, position,
                Vec3.Zero, Vec3.Zero, victim.Velocity, Vec3.Up);
        }

        internal static void Repeat(WeaponCombatMissionLogic.Hit original)
        {
            Agent attacker = original.Attacker, victim = original.Victim;
            if (attacker == null || victim == null || !attacker.IsActive() || !victim.IsActive() || victim.Health <= 0f ||
                !attacker.IsEnemyOf(victim) || !WeaponCombatThrustGeometry.InRelease(original) ||
                SkillTargetProtection.IsProtected(victim)) return;
            MissionWeapon weapon = WeaponCombatMissionLogic.HeldWeapon(attacker);
            if (weapon.IsEmpty || !WeaponCombatRules.Spear(weapon.CurrentUsageItem) ||
                !WeaponCombatThrustGeometry.Sample(original, out Vec3 tip, out Vec3 axis)) return;
            var defense = victim.GetCurrentActionStage(1);
            if (defense == Agent.ActionStage.Defend || defense == Agent.ActionStage.DefendParry) return;
            Vec3 delta = victim.Position - attacker.Position;
            float reach = original.ThrustPose.MaximumReach + Math.Max(0.15f, victim.CollisionCapsule.Radius) + 0.25f;
            if ((victim.Position - original.ThrustPose.AttackerAtHit).AsVec2.Length > reach || Math.Abs(delta.z) > 1.8f) return;
            Vec3 direction = delta.NormalizedCopy();
            if (Vec3.DotProduct(attacker.LookDirection, direction) < 0.5f) return;
            Vec3 position = victim.Position + original.Collision.CollisionGlobalPosition - original.ThrustPose.VictimAtHit;
            // 已由首次真实接触建立短推连接；追加点击按当前释放进度结算。
            // 不能再要求枪尖追上每一帧被推开的目标，否则连刺在推开过程中几乎全部被过滤。
            if (Vec3.DotProduct(axis, direction) < 0.5f) return;
            float length = Math.Max(0.01f, weapon.CurrentUsageItem.GetRealWeaponLength() + attacker.GetCurWeaponOffset().z);
            Vec3 start = tip - axis * length;
            if (attacker.Mission.Scene.RayCastForClosestEntityOrTerrain(start, position, out float distance, 0.01f,
                BodyFlags.CommonCollisionExcludeFlags) && distance + 0.05f < (position - start).Length) return;
            float impactDistance = Math.Max(0f, Math.Min(length, Vec3.DotProduct(position - start, axis)));
            var collision = Collision(attacker, victim, weapon, original.Collision, attacker.GetCurrentActionProgress(1), impactDistance, position, direction, axis);
            bool wasAdditional = WeaponCombatMissionLogic.AdditionalThrust;
            WeaponCombatMissionLogic.AdditionalThrust = true;
            WeaponCombatHitContext.Clear();
            try
            {
                var info = new AttackInformation(attacker, victim, WeakGameEntity.Invalid, in collision, in weapon);
                // 原生helper按AttackProgress生成速度曲线，继续走当前mod的攻击量、护甲和最终伤害模型。
                MissionCombatMechanicsHelper.GetAttackCollisionResults(in info, false, 1f, false, ref collision, out CombatLogData log, out _);
                int raw = collision.InflictedDamage;
                int damage = Math.Max(0, (int)Math.Round(MissionGameModels.Current.AgentApplyDamageModel.CalculateDamage(in info, in collision, raw)));
                if (damage <= 0) return;
                collision.InflictedDamage = damage;
                var blow = new Blow(attacker.Index) {
                    DamageType = weapon.CurrentUsageItem.ThrustDamageType, StrikeType = StrikeType.Thrust,
                    AttackType = AgentAttackType.Standard, BoneIndex = collision.CollisionBoneIndex,
                    VictimBodyPart = collision.VictimHitBodyPart, BaseMagnitude = collision.BaseMagnitude,
                    InflictedDamage = damage, GlobalPosition = position, SwingDirection = direction, Direction = direction,
                    DamageCalculated = true, BlowFlag = BlowFlags.None };
                blow.WeaponRecord.FillAsMeleeBlow(weapon.Item, weapon.CurrentUsageItem,
                    (int)attacker.GetPrimaryWieldedItemIndex(), attacker.Monster.MainHandItemBoneIndex);
                log.ModifiedDamage = damage - raw;
                log.InflictedDamage = (int)Math.Floor(damage * 0.30f);
                attacker.Mission.AddCombatLogSafe(attacker, victim, log);
                // 本路径不经过 Mission.GetAttackCollisionResults，需显式传递本次武器与护甲前伤害。
                WeaponCombatHitContext.Capture(attacker, victim, weapon, in collision);
                victim.RegisterBlow(blow, collision);
            }
            finally
            {
                WeaponCombatHitContext.Clear();
                WeaponCombatMissionLogic.AdditionalThrust = wasAdditional;
            }
        }
    }
}
