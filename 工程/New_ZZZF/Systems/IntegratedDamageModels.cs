using System;
using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using MathF = TaleWorlds.Library.MathF;

namespace New_ZZZF
{
    /// <summary>
    /// Shared final reduction rules for the integrated damage pipeline.
    /// The native reduction phase is still executed, but it is given a copy of
    /// AttackInformation whose ArmorAmountFloat is zero so native armor reduction
    /// cannot run a second time.
    /// </summary>
    internal static class ZZZFIntegratedDamageRules
    {
        public static float GetMinimumDamageRatio(Agent attacker)
        {
            BasicCharacterObject character = attacker?.Character;
            if (character == null)
                return 0f;

            if (character.IsHero)
            {
                Hero hero = (character as CharacterObject)?.HeroObject;
                return hero == null ? 0f : MathF.Max(0f, hero.Level * 0.01f);
            }

            if (character.IsSoldier)
                return MathF.Max(0f, character.GetBattleTier() * 0.05f);

            return 0f;
        }

        public static float ApplyLegacyStateModifiers(
            in AttackInformation attackInformation,
            float baseDamage,
            bool disableFriendlyFire)
        {
            if (disableFriendlyFire && attackInformation.IsFriendlyFire)
                return 0f;

            Random random = new Random();

            Agent attacker = attackInformation.AttackerAgent;
            if (attacker != null && SkillSystemBehavior.ActiveComponents.TryGetValue(attacker.Index, out var attackerComponent))
            {
                if (attackerComponent.StateContainer.HasState("ZhanYiBuff") && random.NextFloat() > 0.5f)
                    baseDamage += 50f;
            }

            Agent victim = attackInformation.VictimAgent;
            if (victim != null && SkillSystemBehavior.ActiveComponents.TryGetValue(victim.Index, out var victimComponent))
            {
                if (victimComponent.StateContainer.HasState("JianRenBuQuuBuff") ||
                    victimComponent.StateContainer.HasState("TianQiBuff"))
                {
                    baseDamage = 1f;
                }
            }

            return baseDamage;
        }

        public static float ApplyRefactoredArmor(
            in AttackInformation attackInformation,
            float damageBeforeArmor)
        {
            if (damageBeforeArmor <= 0f)
                return 0f;

            float armor = MathF.Max(0f, attackInformation.ArmorAmountFloat);
            float armorResult = damageBeforeArmor - armor;
            float minimumResult = damageBeforeArmor * GetMinimumDamageRatio(attackInformation.AttackerAgent);

            return MathF.Max(0f, MathF.Max(armorResult, minimumResult));
        }
    }

    /// <summary>
    /// Campaign/Sandbox damage model.
    /// Keeps the original model's non-armor reduction pipeline while replacing
    /// the native armor calculation and legacy final armor subtraction with the
    /// unified New_ZZZF armor/minimum-damage rule.
    /// </summary>
    public sealed class ZZZFIntegratedSandboxAgentApplyDamageModel : WOW_SandboxAgentApplyDamageModel
    {
        public override float ApplyDamageReductions(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            float armor = attackInformation.ArmorAmountFloat;
            AttackInformation noArmorAttackInformation = attackInformation;
            noArmorAttackInformation.ArmorAmountFloat = 0f;

            // Run the native Sandbox reduction pipeline with armor neutralized.
            float damage = base.ApplyDamageReductions(
                in noArmorAttackInformation,
                in collisionData,
                baseDamage);

            damage = ZZZFIntegratedDamageRules.ApplyLegacyStateModifiers(
                in attackInformation,
                damage,
                disableFriendlyFire: false);

            // Use the real armor only here, exactly once, with the damage-refactor rule.
            AttackInformation finalAttackInformation = attackInformation;
            finalAttackInformation.ArmorAmountFloat = armor;
            return ZZZFIntegratedDamageRules.ApplyRefactoredArmor(
                in finalAttackInformation,
                damage);
        }
    }

    /// <summary>
    /// Custom Battle damage model.
    /// Same pipeline as the Campaign model, with the original Mod's explicit
    /// friendly-fire shutdown preserved.
    /// </summary>
    public sealed class ZZZFIntegratedCustomAgentApplyDamageModel : WOW_CustomAgentApplyDamageModel
    {
        public override float ApplyDamageReductions(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            float armor = attackInformation.ArmorAmountFloat;
            AttackInformation noArmorAttackInformation = attackInformation;
            noArmorAttackInformation.ArmorAmountFloat = 0f;

            if (attackInformation.IsFriendlyFire)
                return 0f;

            // Run the native Custom Battle reduction pipeline with armor neutralized.
            float damage = base.ApplyDamageReductions(
                in noArmorAttackInformation,
                in collisionData,
                baseDamage);

            damage = ZZZFIntegratedDamageRules.ApplyLegacyStateModifiers(
                in attackInformation,
                damage,
                disableFriendlyFire: false);

            AttackInformation finalAttackInformation = attackInformation;
            finalAttackInformation.ArmorAmountFloat = armor;
            return ZZZFIntegratedDamageRules.ApplyRefactoredArmor(
                in finalAttackInformation,
                damage);
        }
    }
}
