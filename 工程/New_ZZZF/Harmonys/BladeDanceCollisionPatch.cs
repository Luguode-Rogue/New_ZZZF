using HarmonyLib;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>原生回调会再次写入友军接触等攻击者硬直；仅乱舞的单位接触免除该硬直。</summary>
    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class BladeDanceCollisionPatch
    {
        private static readonly FieldInfo StrikeField = AccessTools.Field(typeof(AttackCollisionData), "<StrikeType>k__BackingField");
        private static readonly FieldInfo DamageField = AccessTools.Field(typeof(AttackCollisionData), "<DamageType>k__BackingField");
        private static readonly FieldInfo TipField = AccessTools.Field(typeof(AttackCollisionData), "_thrustTipHit");

        private static void Prefix(Agent attacker, ref AttackCollisionData collisionData)
        {
            if (collisionData.IsMissile || collisionData.IsHorseCharge || collisionData.IsAlternativeAttack ||
                collisionData.CollisionResult == CombatCollisionResult.HitWorld)
                return;
            WeaponComponentData usage = ThrustSwingWeaponLease.GetOriginalUsage(attacker);
            if (usage == null || (usage.SwingDamage > 0 && usage.SwingDamageType != DamageTypes.Invalid) || usage.ThrustDamage <= 0 ||
                StrikeField == null || DamageField == null || TipField == null) return;
            // 完整保留真实扫掠碰撞的骨骼、位置及其他字段，只改伤害计算选用的攻击通道。
            // 原生 ComputeBlowMagnitudeMelee 因此读取 ThrustDamageFactor/ThrustSpeed，而不是零挥砍伤害。
            object boxed = collisionData;
            StrikeField.SetValue(boxed, (int)StrikeType.Thrust);
            DamageField.SetValue(boxed, (int)usage.ThrustDamageType);
            TipField.SetValue(boxed, true);
            collisionData = (AttackCollisionData)boxed;
        }
        private static void Postfix(Agent attacker, ref AttackCollisionData collisionData)
        {
            if (!(JiFengLianZhanMissionLogic.Current?.IsBladeDanceActive(attacker) == true ||
                  BkbCombatRules.IsOverhead(attacker, collisionData.AttackDirection, (StrikeType)collisionData.StrikeType)) ||
                collisionData.IsMissile || collisionData.IsHorseCharge ||
                collisionData.CollisionResult == CombatCollisionResult.HitWorld)
                return;
            collisionData.AttackerStunPeriod = 0f;
        }
    }
}