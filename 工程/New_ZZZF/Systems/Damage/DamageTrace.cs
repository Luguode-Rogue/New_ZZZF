using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal static class DamageTrace
    {
        public static bool Enabled = true;

        public static void LogFinal(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float damageBeforeReduction,
            float nativeWithoutArmor,
            float stateAdjustedDamage,
            float adjustedArmor,
            float finalDamage,
            bool customBattle)
        {
            if (!Enabled)
                return;

            Agent attacker = attackInformation.AttackerAgent;
            Agent victim = attackInformation.VictimAgent;
            bool zhanYi = false;
            bool jianRenBuQu = false;
            bool tianQi = false;

            if (attacker != null && SkillSystemBehavior.ActiveComponents.TryGetValue(attacker.Index, out var attackerComponent))
                zhanYi = attackerComponent.StateContainer.HasState("ZhanYiBuff");

            if (victim != null && SkillSystemBehavior.ActiveComponents.TryGetValue(victim.Index, out var victimComponent))
            {
                jianRenBuQu = victimComponent.StateContainer.HasState("JianRenBuQuuBuff") || victimComponent.StateContainer.HasState("JianRenBuQu");
                tianQi = victimComponent.StateContainer.HasState("TianQiBuff") || victimComponent.StateContainer.HasState("TianQi");
            }

            float minimumRatio = GetMinimumDamageRatio(attacker);
            float effectiveArmor = Math.Max(0f, adjustedArmor);
            float armorResult = stateAdjustedDamage - effectiveArmor;
            float minimumResult = stateAdjustedDamage * minimumRatio;
            float stateDelta = stateAdjustedDamage - nativeWithoutArmor;

            DamageLog.Info(string.Format(
                "[DAMAGE] atk={0} vic={1} mode={2} dmgType={3} in={4:F2} native0={5:F2} state={6:F2} stateDelta={7:+0.00;-0.00;0.00} armorAdj={8:F2} armorResult={9:F2} min={10:F2}@{11:P0} final={12:F2} FF={13} ZY={14} JR={15} TQ={16} missile={17}",
                attacker != null ? attacker.Index : -1,
                victim != null ? victim.Index : -1,
                customBattle ? "Custom" : "Campaign",
                collisionData.DamageType,
                damageBeforeReduction,
                nativeWithoutArmor,
                stateAdjustedDamage,
                stateDelta,
                effectiveArmor,
                armorResult,
                minimumResult,
                minimumRatio,
                finalDamage,
                attackInformation.IsFriendlyFire,
                zhanYi,
                jianRenBuQu,
                tianQi,
                collisionData.AffectorWeaponSlotOrMissileIndex));
        }

        public static void LogDisarmQueued(
            Agent attacker,
            Agent defender,
            EquipmentIndex equipmentIndex,
            float chance,
            float roll)
        {
            if (!Enabled)
                return;

            DamageLog.Info(string.Format(
                "[DISARM] phase=queued atk={0} vic={1} slot={2} chance={3:P1} roll={4:F4} result={5}",
                attacker != null ? attacker.Index : -1,
                defender != null ? defender.Index : -1,
                equipmentIndex,
                chance,
                roll,
                chance > roll ? "QUEUE" : "MISS"));
        }

        public static void LogDisarmExecution(
            Agent defender,
            EquipmentIndex requestedIndex,
            string result)
        {
            if (!Enabled)
                return;

            DamageLog.Info(string.Format(
                "[DISARM] phase=execute vic={0} requestedSlot={1} result={2}",
                defender != null ? defender.Index : -1,
                requestedIndex,
                result));
        }

        private static float GetMinimumDamageRatio(Agent attacker)
        {
            BasicCharacterObject character = attacker != null ? attacker.Character : null;
            if (character == null)
                return 0f;

            if (character.IsHero)
            {
                CharacterObject characterObject = character as CharacterObject;
                Hero hero = characterObject != null ? characterObject.HeroObject : null;
                return hero == null ? 0f : Math.Max(0f, hero.Level * 0.01f);
            }

            return character.IsSoldier ? Math.Max(0f, character.GetBattleTier() * 0.05f) : 0f;
        }
    }
}