using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal static class DamageCalculationRules
    {
        public static float GetMinimumDamageRatio(Agent attacker)
        {
            BasicCharacterObject character = attacker?.Character;
            if (character == null)
                return 0f;

            if (character.IsHero)
            {
                Hero hero = (character as CharacterObject)?.HeroObject;
                return hero == null ? 0f : TaleWorlds.Library.MathF.Max(0f, hero.Level * 0.01f);
            }

            if (character.IsSoldier)
                return TaleWorlds.Library.MathF.Max(0f, character.GetBattleTier() * 0.05f);

            return 0f;
        }

        /// <summary>
        /// Applies the damage-refactor rule after the native damage reduction stage
        /// has been run with armor neutralized.
        ///
        /// The armor passed in here is AttackInformation.ArmorAmountFloat, i.e. the
        /// native adjusted armor value after Bannerlord's armor perks/penetration
        /// handling. We deliberately do not recompute those perks here.
        /// </summary>
        public static float ApplyRefactoredArmor(
            in AttackInformation attackInformation,
            float damageBeforeArmor)
        {
            if (damageBeforeArmor <= 0f)
                return 0f;

            float armor = TaleWorlds.Library.MathF.Max(0f, attackInformation.ArmorAmountFloat);
            float armorResult = damageBeforeArmor - armor;
            float minimumResult = damageBeforeArmor * GetMinimumDamageRatio(attackInformation.AttackerAgent);

            return TaleWorlds.Library.MathF.Clamp(
                TaleWorlds.Library.MathF.Max(armorResult, minimumResult),
                0f,
                1000f);
        }

        public static float ApplyCampaignFinalRules(
            in AttackInformation attackInformation,
            float damage)
        {
            if (attackInformation.IsFriendlyFire)
                return 0f;

            Agent attacker = attackInformation.AttackerAgent;
            if (attacker != null &&
                SkillSystemBehavior.ActiveComponents.TryGetValue(attacker.Index, out var attackerComponent) &&
                attackerComponent.StateContainer.HasState("ZhanYiBuff") &&
                SkillSystemBehavior.Random.NextFloat() > 0.5f)
            {
                damage += 50f;
            }

            Agent victim = attackInformation.VictimAgent;
            if (victim != null &&
                SkillSystemBehavior.ActiveComponents.TryGetValue(victim.Index, out var victimComponent))
            {
                if (victimComponent.StateContainer.HasState("JianRenBuQuuBuff"))
                    damage = 1f;
                else if (victimComponent.StateContainer.HasState("TianQiBuff"))
                    damage = 1f;
            }

            return damage;
        }

        public static float ApplyCustomBattleFinalRules(
            in AttackInformation attackInformation,
            float damage)
        {
            Agent attacker = attackInformation.AttackerAgent;
            if (attacker != null &&
                SkillSystemBehavior.ActiveComponents.TryGetValue(attacker.Index, out var attackerComponent) &&
                attackerComponent.StateContainer.HasState("ZhanYiBuff") &&
                SkillSystemBehavior.Random.NextFloat() > 0.5f)
            {
                damage += 50f;
            }

            Agent victim = attackInformation.VictimAgent;
            if (victim != null &&
                SkillSystemBehavior.ActiveComponents.TryGetValue(victim.Index, out var victimComponent))
            {
                if (victimComponent.StateContainer.HasState("JianRenBuQu"))
                    damage = 1f;
                else if (victimComponent.StateContainer.HasState("TianQi"))
                    damage = 1f;
            }

            return damage;
        }
    }
}
