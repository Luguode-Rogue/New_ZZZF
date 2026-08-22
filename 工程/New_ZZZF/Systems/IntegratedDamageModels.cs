using SandBox.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// Campaign/Sandbox integrated damage model.
    /// Existing New_ZZZF strike/missile/affix rules stay in their strike models;
    /// final damage and block-breaking are handled by the refactored shared rules.
    /// </summary>
    public sealed class ZZZFIntegratedSandboxAgentApplyDamageModel : WOW_SandboxAgentApplyDamageModel
    {
        public override bool DecideCrushedThrough(
            Agent attackerAgent,
            Agent defenderAgent,
            float totalAttackEnergy,
            Agent.UsageDirection attackDirection,
            StrikeType strikeType,
            WeaponComponentData defendItem,
            bool isPassiveUsage)
        {
            return ZZZFBlockBreakRules.Decide(
                attackerAgent,
                defenderAgent,
                totalAttackEnergy,
                attackDirection,
                strikeType,
                defendItem,
                isPassiveUsage);
        }

        public override float ApplyDamageReductions(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            if (attackInformation.IsFriendlyFire)
            {
                DamageTrace.LogFinal(
                    in attackInformation,
                    in collisionData,
                    baseDamage,
                    0f,
                    0f,
                    0f,
                    0f,
                    customBattle: false);
                return 0f;
            }

            float adjustedArmor = attackInformation.ArmorAmountFloat;

            // Skip only native armor reduction. All other native reduction logic remains active.
            AttackInformation noArmor = attackInformation;
            noArmor.ArmorAmountFloat = 0f;

            float nativeWithoutArmor = base.ApplyDamageReductions(
                in noArmor,
                in collisionData,
                baseDamage);

            float stateAdjustedDamage = DamageCalculationRules.ApplyCampaignFinalRules(
                in attackInformation,
                nativeWithoutArmor);

            AttackInformation armorContext = attackInformation;
            armorContext.ArmorAmountFloat = adjustedArmor;
            float finalDamage = DamageCalculationRules.ApplyRefactoredArmor(
                in armorContext,
                stateAdjustedDamage);

            DamageTrace.LogFinal(
                in attackInformation,
                in collisionData,
                baseDamage,
                nativeWithoutArmor,
                stateAdjustedDamage,
                adjustedArmor,
                finalDamage,
                customBattle: false);

            return finalDamage;
        }
    }

    /// <summary>
    /// Custom Battle integrated damage model.
    /// </summary>
    public sealed class ZZZFIntegratedCustomAgentApplyDamageModel : WOW_CustomAgentApplyDamageModel
    {
        public override bool DecideCrushedThrough(
            Agent attackerAgent,
            Agent defenderAgent,
            float totalAttackEnergy,
            Agent.UsageDirection attackDirection,
            StrikeType strikeType,
            WeaponComponentData defendItem,
            bool isPassiveUsage)
        {
            return ZZZFBlockBreakRules.Decide(
                attackerAgent,
                defenderAgent,
                totalAttackEnergy,
                attackDirection,
                strikeType,
                defendItem,
                isPassiveUsage);
        }

        public override float ApplyDamageReductions(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            if (attackInformation.IsFriendlyFire)
            {
                DamageTrace.LogFinal(
                    in attackInformation,
                    in collisionData,
                    baseDamage,
                    0f,
                    0f,
                    0f,
                    0f,
                    customBattle: true);
                return 0f;
            }

            float adjustedArmor = attackInformation.ArmorAmountFloat;

            AttackInformation noArmor = attackInformation;
            noArmor.ArmorAmountFloat = 0f;

            float nativeWithoutArmor = base.ApplyDamageReductions(
                in noArmor,
                in collisionData,
                baseDamage);

            float stateAdjustedDamage = DamageCalculationRules.ApplyCustomBattleFinalRules(
                in attackInformation,
                nativeWithoutArmor);

            AttackInformation armorContext = attackInformation;
            armorContext.ArmorAmountFloat = adjustedArmor;
            float finalDamage = DamageCalculationRules.ApplyRefactoredArmor(
                in armorContext,
                stateAdjustedDamage);

            DamageTrace.LogFinal(
                in attackInformation,
                in collisionData,
                baseDamage,
                nativeWithoutArmor,
                stateAdjustedDamage,
                adjustedArmor,
                finalDamage,
                customBattle: true);

            return finalDamage;
        }
    }
}
