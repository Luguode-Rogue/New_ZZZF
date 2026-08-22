using SandBox.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// Integrates the damage-refactor armor rule into the existing New_ZZZF
    /// AgentApplyDamageModel pipeline without replacing any of the existing
    /// StrikeMagnitude, missile, affix, crush-through or skill logic.
    ///
    /// AttackInformation.ArmorAmountFloat is already the native adjusted armor
    /// value. Bannerlord performs armor-perk and armor-penetration adjustments
    /// before this stage, so we consume that value rather than reimplementing
    /// those perks here.
    /// </summary>
    public sealed class ZZZFIntegratedSandboxAgentApplyDamageModel : WOW_SandboxAgentApplyDamageModel
    {
        public override float ApplyDamageReductions(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            // The original New_ZZZF final formula explicitly disabled friendly fire.
            if (attackInformation.IsFriendlyFire)
                return 0f;

            // Preserve the real, already-adjusted armor for the new armor rule.
            float adjustedArmor = attackInformation.ArmorAmountFloat;

            // Run Bannerlord's native reduction stage with armor neutralized.
            // This skips only native armor subtraction while retaining every other
            // native reduction/perk/banner effect.
            AttackInformation noArmor = attackInformation;
            noArmor.ArmorAmountFloat = 0f;

            float damage = base.ApplyDamageReductions(
                in noArmor,
                in collisionData,
                baseDamage);

            // Preserve the original New_ZZZF final-state rules first.
            damage = DamageCalculationRules.ApplyCampaignFinalRules(
                in attackInformation,
                damage);

            // Replace the old Armor * 0.1 subtraction with damage-refactor:
            // max(damage - adjustedArmor, damage * minimumRatio).
            AttackInformation armorContext = attackInformation;
            armorContext.ArmorAmountFloat = adjustedArmor;
            return DamageCalculationRules.ApplyRefactoredArmor(
                in armorContext,
                damage);
        }
    }

    /// <summary>
    /// Custom Battle equivalent of the integrated pipeline.
    /// Existing New_ZZZF skill/strike rules remain in their original models.
    /// </summary>
    public sealed class ZZZFIntegratedCustomAgentApplyDamageModel : WOW_CustomAgentApplyDamageModel
    {
        public override float ApplyDamageReductions(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            float adjustedArmor = attackInformation.ArmorAmountFloat;

            AttackInformation noArmor = attackInformation;
            noArmor.ArmorAmountFloat = 0f;

            float damage = base.ApplyDamageReductions(
                in noArmor,
                in collisionData,
                baseDamage);

            // Preserve the exact Custom Battle final-state names used by the
            // existing New_ZZZF code, then apply the new armor rule.
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
