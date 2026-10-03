using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace New_ZZZF
{
    internal static class WeaponCombatHitContext
    {
        [System.ThreadStatic] internal static float PreArmorDamage;
        [System.ThreadStatic] private static Agent _attacker, _victim;
        [System.ThreadStatic] private static MissionWeapon _weapon;
        [System.ThreadStatic] private static int _collisionIndex;
        [System.ThreadStatic] private static float _damage;
        internal static void Capture(Agent attacker, Agent victim, MissionWeapon weapon, in AttackCollisionData collision)
        {
            _attacker = attacker; _victim = victim; _weapon = weapon;
            _collisionIndex = collision.AffectorWeaponSlotOrMissileIndex; _damage = PreArmorDamage;
        }
        internal static bool Take(Agent attacker, Agent victim, in AttackCollisionData collision, out MissionWeapon weapon, out float damage)
        {
            weapon = default; damage = 0f;
            if (attacker != _attacker || victim != _victim || collision.AffectorWeaponSlotOrMissileIndex != _collisionIndex) return false;
            weapon = _weapon; damage = _damage; Clear(); return true;
        }
        internal static void Clear() { _attacker = _victim = null; _weapon = default; _damage = PreArmorDamage = 0f; }
    }

    [HarmonyPatch(typeof(Mission), "GetAttackCollisionResults")]
    internal static class WeaponCombatCalculatedHitPatch
    {
        [HarmonyPrefix]
        private static void Prefix() { if (WeaponCombatRules.Enabled) WeaponCombatHitContext.Clear(); }
        [HarmonyPostfix]
        private static void Postfix(Agent attackerAgent, Agent victimAgent, in MissionWeapon attackerWeapon, ref AttackCollisionData attackCollisionData)
        {
            if (WeaponCombatRules.Enabled) WeaponCombatHitContext.Capture(attackerAgent, victimAgent, attackerWeapon, in attackCollisionData);
        }
    }

    [HarmonyPatch(typeof(Agent), "HandleBlow")]
    internal static class WeaponCombatBlowPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Agent __instance, ref Blow b, ref AttackCollisionData collisionData,
            out WeaponCombatMissionLogic.Hit __state)
        {
            __state = null;
            if (!WeaponCombatRules.Enabled || WeaponCombatMissionLogic.DeliveringBleed || b.InflictedDamage <= 0) return;
            Agent attacker = b.OwnerId >= 0 ? __instance.Mission.FindAgentWithIndex(b.OwnerId) : null;
            MissionWeapon weapon = default;
            float preArmor = 0f;
            bool captured = WeaponCombatHitContext.Take(attacker, __instance, in collisionData, out weapon, out preArmor);
            if (!captured && attacker != null && !collisionData.IsMissile)
            {
                var slot = (EquipmentIndex)collisionData.AffectorWeaponSlotOrMissileIndex;
                if ((int)slot >= 0 && (int)slot < 4) weapon = attacker.Equipment[slot];
            }
            bool body = collisionData.CollisionResult == CombatCollisionResult.StrikeAgent &&
                !collisionData.AttackBlockedWithShield && !collisionData.CollidedWithShieldOnBack &&
                !collisionData.IsAlternativeAttack && !collisionData.IsHorseCharge && !collisionData.IsFallDamage;
            bool spear = body && !collisionData.IsMissile && b.StrikeType == StrikeType.Thrust &&
                WeaponCombatRules.Spear(weapon.CurrentUsageItem);
            __state = new WeaponCombatMissionLogic.Hit { Attacker = attacker, Victim = __instance,
                Weapon = weapon, Blow = b, Collision = collisionData, FullDamage = b.InflictedDamage,
                HealthBefore = __instance.Health, SpearSplit = spear, Synthetic = WeaponCombatMissionLogic.AdditionalThrust || ArcWeaponNativeHit.IsRegistering(attacker, __instance),
                PreArmorDamage = preArmor,
                Time = __instance.Mission.CurrentTime };
            if (spear && !__state.Synthetic)
                WeaponCombatThrustGeometry.Capture(__state);
            if (spear)
            {
                b.InflictedDamage = (int)System.Math.Floor(__state.FullDamage * 0.30f);
                __state.DelayedDamage = System.Math.Max(0, (int)System.Math.Round(__state.FullDamage * 1.20f) - b.InflictedDamage);
                collisionData.InflictedDamage = b.InflictedDamage;
            }
        }
        [HarmonyPostfix]
        private static void Postfix(Agent __instance, WeaponCombatMissionLogic.Hit __state)
        {
            // Immortal、对话场景、原生取消伤害均不会创建控制效果。
            if (__state == null) return;
            __state.ActualDamage = __instance.Health < __state.HealthBefore;
            // 即时30%不足1点时依然兑现延迟伤害，但不能推开。原生免死/禁止受伤场景不入队。
            bool zeroInstant = __state.SpearSplit && (int)System.Math.Floor(__state.FullDamage * 0.30f) == 0 &&
                __instance.CurrentMortalityState != Agent.MortalityState.Immortal && !__instance.Mission.DisableDying;
            if (__state.ActualDamage || zeroInstant) WeaponCombatMissionLogic.Current?.EnqueueHit(__state);
        }
    }

    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class WeaponCombatMeleeCollisionPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Agent attacker)
        { if (WeaponCombatRules.Enabled) WeaponCombatMissionLogic.Current.Contact(attacker); }
        [HarmonyPostfix]
        private static void Postfix(ref AttackCollisionData collisionData, ref MeleeCollisionReaction colReaction,
            ref float inOutMomentumRemaining)
        {
            if (!WeaponCombatRules.Enabled || collisionData.IsAlternativeAttack || collisionData.StrikeType != (int)StrikeType.Thrust) return;
            if (colReaction == MeleeCollisionReaction.Bounced || colReaction == MeleeCollisionReaction.Staggered || colReaction == MeleeCollisionReaction.Stuck)
            {
                colReaction = MeleeCollisionReaction.ContinueChecking;
                // 取消弹刀不等于穿过盾或墙继续伤人。
                if (collisionData.AttackBlockedWithShield || collisionData.CollisionResult == CombatCollisionResult.HitWorld)
                    inOutMomentumRemaining = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(Mission), "CreateMeleeBlow")]
    internal static class WeaponCombatMeleeBlowPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref Blow __result)
        {
            if (WeaponCombatRules.Enabled && __result.StrikeType == StrikeType.Thrust && __result.AttackType == AgentAttackType.Standard)
                __result.AttackerStunPeriod = 0f;
        }
    }

    [HarmonyPatch(typeof(MissionCombatMechanicsHelper), nameof(MissionCombatMechanicsHelper.GetAttackCollisionResults))]
    internal static class WeaponCombatContactPatch
    {
        [HarmonyPrefix]
        private static void Prefix(in AttackInformation attackInformation, ref AttackCollisionData attackCollisionData,
            out StrikeType? __state)
        {
            __state = WeaponCombatRules.CurrentStrike;
            WeaponCombatRules.CurrentStrike = attackCollisionData.IsMissile ? (StrikeType?)null : (StrikeType)attackCollisionData.StrikeType;
            if (!WeaponCombatRules.Enabled || attackCollisionData.IsMissile || WeaponCombatMissionLogic.AdditionalThrust ||
                ArcWeaponNativeHit.IsRegistering(attackInformation.AttackerAgent, attackInformation.VictimAgent)) return;
            WeaponCombatMissionLogic.Current.Contact(attackInformation.AttackerAgent);
        }
        [HarmonyFinalizer]
        private static void Finalizer(StrikeType? __state) { WeaponCombatRules.CurrentStrike = __state; }
    }

    [HarmonyPatch(typeof(MissionCombatMechanicsHelper), "GetDefendCollisionResults")]
    internal static class WeaponCombatDefendPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Agent attackerAgent, Agent defenderAgent, StrikeType strikeType, ref float attackerStunPeriod)
        {
            if (!WeaponCombatRules.Enabled) return;
            var weapon = attackerAgent?.WieldedWeapon.CurrentUsageItem;
            WeaponCombatRules.Stun(weapon, strikeType, ref attackerStunPeriod);
            WeaponCombatMissionLogic.Current.Contact(attackerAgent);
        }
    }

    [HarmonyPatch(typeof(MissionMainAgentController), "ControlTick")]
    internal static class WeaponCombatPlayerInputPatch
    {
        [HarmonyPostfix]
        private static void Postfix() { if (WeaponCombatRules.Enabled) WeaponCombatMissionLogic.Current.PlayerControl(); }
    }
}
