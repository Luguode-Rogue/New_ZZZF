using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using New_ZZZF.Skills;

namespace New_ZZZF
{
    internal static class ArcWeaponNativeHit
    {
        [ThreadStatic] private static Agent _registeredCaster, _registeredVictim;
        internal static bool IsRegistering(Agent attacker, Agent victim) =>
            attacker != null && victim != null &&
            ReferenceEquals(attacker, _registeredCaster) && ReferenceEquals(victim, _registeredVictim);
        internal sealed class Snapshot
        {
            internal Agent Caster;
            internal MissionWeapon Weapon;
            internal EquipmentIndex Slot;
            internal StrikeType Strike;
            internal Agent.UsageDirection AttackDirection;
        }
        internal static bool TryCapture(Agent caster, out Snapshot snapshot)
        {
            snapshot = null;
            if (caster == null || !caster.IsActive() || !caster.IsHuman || caster.Character == null || caster.Equipment == null) return false;
            EquipmentIndex slot = caster.GetPrimaryWieldedItemIndex();
            if (slot < EquipmentIndex.WeaponItemBeginSlot || slot >= EquipmentIndex.NumAllWeaponSlots) return false;
            MissionWeapon weapon = caster.Equipment[slot];
            WeaponComponentData usage = weapon.CurrentUsageItem;
            if (weapon.IsEmpty || usage == null || !usage.IsMeleeWeapon || usage.IsRangedWeapon || usage.IsShield) return false;
            WeaponComponentData original = ThrustSwingWeaponLease.GetOriginalUsage(caster) ?? usage;
            StrikeType strike = original.SwingDamage > 0 && original.SwingDamageType != DamageTypes.Invalid ? StrikeType.Swing : StrikeType.Thrust;
            if (strike == StrikeType.Thrust && (original.ThrustDamage <= 0 || original.ThrustDamageType == DamageTypes.Invalid)) return false;
            snapshot = new Snapshot { Caster = caster, Weapon = weapon, Slot = slot, Strike = strike,
                AttackDirection = strike == StrikeType.Swing ? Agent.UsageDirection.AttackLeft : Agent.UsageDirection.AttackDown };
            return true;
        }
        internal static void Register(Snapshot cast, Agent victim, Vec3 position, Vec3 direction, float divisor, bool swordEnergy)
        {
            Agent caster = cast.Caster;
            if (caster == null || !caster.IsActive() || caster.Equipment == null || victim == null || !victim.IsActive() ||
                victim.Health <= 0f || victim.Equipment == null || !caster.IsEnemyOf(victim) || SkillTargetProtection.IsProtected(victim)) return;
            if (caster.GetPrimaryWieldedItemIndex() != cast.Slot ||
                caster.Equipment[cast.Slot].Item != cast.Weapon.Item ||
                caster.Equipment[cast.Slot].ItemModifier != cast.Weapon.ItemModifier) return;
            WeaponComponentData usage = cast.Weapon.CurrentUsageItem;
            DamageTypes type = cast.Strike == StrikeType.Swing ? usage.SwingDamageType : usage.ThrustDamageType;
            direction.Normalize();
            sbyte bone = caster.Monster.MainHandItemBoneIndex;
            float impactDistance = Math.Max(0.05f, usage.GetRealWeaponLength() * 0.9f);
            var collision = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                false, false, false, true, false, false, false, false, false, true, false, false,
                CombatCollisionResult.StrikeAgent, (int)cast.Slot, (int)cast.Strike, (int)type,
                0, BoneBodyPartType.Chest, bone, cast.AttackDirection, -1, CombatHitResultFlags.NormalHit,
                0.5f, impactDistance, 0f, 0f, 0f, 0f, 0f, 0f,
                Vec3.Up, direction, position, Vec3.Zero, Vec3.Zero, victim.Velocity, Vec3.Up);
            var attack = new AttackInformation(caster, victim, WeakGameEntity.Invalid, in collision, in cast.Weapon);
            MissionCombatMechanicsHelper.GetAttackCollisionResults(in attack, false, 1f, false,
                ref collision, out CombatLogData log, out _);
            int armorDamage = collision.InflictedDamage;
            float native = MissionGameModels.Current.AgentApplyDamageModel.CalculateDamage(in attack, in collision, armorDamage);
            int final = Math.Max(0, (int)TaleWorlds.Library.MathF.Round(native / divisor));
            if (final <= 0) return;
            collision.InflictedDamage = final;
            var blow = new Blow(caster.Index) { DamageType = type, StrikeType = cast.Strike,
                AttackType = AgentAttackType.Standard, BoneIndex = 0, VictimBodyPart = BoneBodyPartType.Chest,
                BaseMagnitude = collision.BaseMagnitude, InflictedDamage = final, GlobalPosition = position,
                SwingDirection = direction, Direction = direction, DamageCalculated = true, BlowFlag = BlowFlags.None };
            // 与灵魂弹幕同样只登记安全的普通武器命中，不伪造原生 missile index。
            blow.WeaponRecord.FillAsMeleeBlow(cast.Weapon.Item, usage, (int)cast.Slot, bone);
            blow.WeaponRecord.CurrentPosition = position;
            blow.WeaponRecord.StartingPosition = position;
            log.InflictedDamage = final;
            // TotalDamage = InflictedDamage + ModifiedDamage；final 已包含全部修正和技能折算。
            // 再写入折算差值会让真银斩显示为零、剑气斩显示为负数。
            log.ModifiedDamage = 0;
            log.AbsorbedDamage = (int)TaleWorlds.Library.MathF.Round(collision.AbsorbedByArmor / divisor);
            collision.AbsorbedByArmor = log.AbsorbedDamage;
            caster.Mission.AddCombatLogSafe(caster, victim, log);
            Agent previousCaster = _registeredCaster, previousVictim = _registeredVictim;
            _registeredCaster = caster; _registeredVictim = victim;
            if (swordEnergy) JianQiHitContext.Begin(caster, victim);
            try { victim.RegisterBlow(blow, collision); }
            finally {
                _registeredCaster = previousCaster; _registeredVictim = previousVictim;
                if (swordEnergy) JianQiHitContext.End();
            }
            // RegisterBlow 分发 OnAgentHit；OnMeleeHit 按原版命中流程单独分发。
            if (Mission.Current != null)
                foreach (MissionBehavior behavior in Mission.Current.MissionBehaviors)
                    behavior.OnMeleeHit(caster, victim, false, collision);
        }
    }
}
