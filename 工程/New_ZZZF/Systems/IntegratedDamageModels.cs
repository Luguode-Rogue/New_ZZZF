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
                return 0f;

            // ArmorAmountFloat has already passed Bannerlord's native armor-perk and
            // armor-penetration adjustment. Keep that real adjusted value for the new rule.
            float adjustedArmor = attackInformation.ArmorAmountFloat;

            // Skip only native armor reduction. All other native reduction logic remains active.
            AttackInformation noArmor = attackInformation;
            noArmor.ArmorAmountFloat = 0f;

            float damage = base.ApplyDamageReductions(
                in noArmor,
                in collisionData,
                baseDamage);

            damage = DamageCalculationRules.ApplyCampaignFinalRules(
                in attackInformation,
                damage);

            AttackInformation armorContext = attackInformation;
            armorContext.ArmorAmountFloat = adjustedArmor;
            return DamageCalculationRules.ApplyRefactoredArmor(
                in armorContext,
                damage);
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
                return 0f;

            float adjustedArmor = attackInformation.ArmorAmountFloat;

            AttackInformation noArmor = attackInformation;
            noArmor.ArmorAmountFloat = 0f;

            float damage = base.ApplyDamageReductions(
                in noArmor,
                in collisionData,
                baseDamage);

            damage = DamageCalculationRules.ApplyCustomBattleFinalRules(
                in attackInformation,
                damage);

            AttackInformation armorContext = attackInformation;
            armorContext.ArmorAmountFloat = adjustedArmor;
            return DamageCalculationRules.ApplyRefactoredArmor(
                in armorContext,
                damage);
        }
    }
}
