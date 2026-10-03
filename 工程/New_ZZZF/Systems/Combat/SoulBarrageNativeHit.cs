using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using New_ZZZF.Skills;
using MathF = TaleWorlds.Library.MathF;

namespace New_ZZZF
{
    internal static class SoulBarrageNativeHit
    {
        internal sealed class CastSnapshot {
            public Agent Caster;
            public SkillBase Skill;
            public MissionWeapon Weapon;
            public EquipmentIndex Slot;
            public int Proficiency;
            public float BaseDamage;
            public Vec3 Start;
            public readonly HashSet<int> Kills = new HashSet<int>();
        }
        [ThreadStatic] private static Agent _caster;
        [ThreadStatic] private static Agent _victim;
        [ThreadStatic] private static bool _forceSneak;


        internal static bool IsCurrentHit(Agent attacker, Agent victim) => attacker == _caster && victim == _victim && _caster != null;
        internal static bool ForcesSneak(Agent attacker, Agent victim) => _forceSneak && attacker == _caster && victim == _victim;
        private static readonly BoneBodyPartType[] Parts = {
            BoneBodyPartType.Chest, BoneBodyPartType.Abdomen, BoneBodyPartType.Head, BoneBodyPartType.Neck,
            BoneBodyPartType.ShoulderLeft, BoneBodyPartType.ShoulderRight, BoneBodyPartType.ArmLeft,
            BoneBodyPartType.ArmRight, BoneBodyPartType.Legs };
        private static BoneBodyPartType GetPart(Agent target, int proficiency) {
            if (proficiency < 100) return BoneBodyPartType.Chest;
            if (proficiency >= 200) return BoneBodyPartType.Head;
            BoneBodyPartType best = BoneBodyPartType.Chest;
            float armor = target.GetBaseArmorEffectivenessForBodyPart(best);
            foreach (var part in Parts) {
                float current = target.GetBaseArmorEffectivenessForBodyPart(part);
                if (current < armor) { armor = current; best = part; }
            }
            return best;
        }
        internal static void Register(CastSnapshot cast, Agent target, Vec3 impactPosition) {
            Agent caster = cast.Caster;
            if (caster == null || !caster.IsActive() || target == null || !target.IsActive() || target.Health <= 0f ||
                !caster.IsEnemyOf(target) || SkillTargetProtection.IsProtected(target)) return;
            var usage = cast.Weapon.CurrentUsageItem;
            var part = GetPart(target, cast.Proficiency);
            DamageTypes damageType = usage.GetModifiedThrustDamage(cast.Weapon.ItemModifier) >= usage.GetModifiedSwingDamage(cast.Weapon.ItemModifier)
                ? usage.ThrustDamageType : usage.SwingDamageType;
            if (damageType == DamageTypes.Invalid) damageType = DamageTypes.Pierce;
            Vec3 direction = impactPosition - cast.Start;
            if (direction.LengthSquared < 0.001f) direction = caster.LookDirection;
            direction.Normalize();
            Vec3 velocity = direction * 90f;
            sbyte bone = caster.Monster.MainHandItemBoneIndex;
            var collision = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                false, false, false, true, false, true, false, false,
                false, true, false, false, CombatCollisionResult.StrikeAgent,
                (int)cast.Slot, (int)StrikeType.Thrust, (int)damageType, 0, part, bone,
                Agent.UsageDirection.AttackDown, -1, CombatHitResultFlags.NormalHit,
                0.5f, 1f, 0f, 0f, cast.BaseDamage, 90f, 0f, 0f,
                Vec3.Up, direction, impactPosition, velocity, cast.Start, target.Velocity, Vec3.Up);
            var attack = new AttackInformation(caster, target, WeakGameEntity.Invalid, in collision, in cast.Weapon);
            Agent previousCaster = _caster, previousVictim = _victim;
            bool previousSneak = _forceSneak;


            _caster = caster; _victim = target; _forceSneak = cast.Proficiency >= 300;
            try {
                MissionCombatMechanicsHelper.GetAttackCollisionResults(in attack, false, 1f, false,
                    ref collision, out CombatLogData log, out _);
                int rawDamage = collision.InflictedDamage;
                int finalDamage = Math.Max(0, (int)MathF.Round(MissionGameModels.Current.AgentApplyDamageModel.CalculateDamage(
                    in attack, in collision, rawDamage)));
                collision.InflictedDamage = finalDamage;
                if (finalDamage <= 0) return;
                var blow = new Blow(caster.Index) {
                    DamageType = damageType, StrikeType = StrikeType.Thrust, AttackType = AgentAttackType.Standard,
                    BoneIndex = 0, VictimBodyPart = part, BaseMagnitude = collision.BaseMagnitude,
                    InflictedDamage = finalDamage, GlobalPosition = impactPosition, SwingDirection = direction,
                    Direction = direction, DamageCalculated = true, BlowFlag = BlowFlags.NoSound };
                // 飞行由自建管理器负责，没有原生 missile index。远程数据只用于上面的伤害计算；
                // 登记时必须使用普通武器命中，避免本机层读取不存在的飞弹。
                blow.WeaponRecord.FillAsMeleeBlow(cast.Weapon.Item, usage, (int)cast.Slot, bone);
                var registeredCollision = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                    false, false, false, true, false, false, false, false,
                    false, true, false, false, CombatCollisionResult.StrikeAgent,
                    (int)cast.Slot, (int)StrikeType.Thrust, (int)damageType, 0, part, bone,
                    Agent.UsageDirection.AttackDown, -1, CombatHitResultFlags.NormalHit,
                    0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f,
                    Vec3.Up, direction, impactPosition, Vec3.Zero, Vec3.Zero, target.Velocity, Vec3.Up);
                registeredCollision.BaseMagnitude = collision.BaseMagnitude;
                registeredCollision.InflictedDamage = finalDamage;
                registeredCollision.AbsorbedByArmor = collision.AbsorbedByArmor;
                registeredCollision.IsSneakAttack = collision.IsSneakAttack;
                log.ModifiedDamage = finalDamage - rawDamage; log.InflictedDamage = finalDamage;
                log.IsRangedAttack = true; log.IsSneakAttack = collision.IsSneakAttack;
                caster.Mission.AddCombatLogSafe(caster, target, log);
                float healthBefore = target.Health;
                target.RegisterBlow(blow, registeredCollision);
                if (healthBefore > 0f && target.Health <= 0f && cast.Kills.Add(target.Index))
                    caster.GetComponent<AgentSkillComponent>()?.ReduceSkillCooldown(cast.Skill, 3f);
            } finally { _caster = previousCaster; _victim = previousVictim; _forceSneak = previousSneak; }
        }
    }
}