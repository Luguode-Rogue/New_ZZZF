using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal static class DamageCalculationRules
    {
        private static readonly Random Random = new Random();

        public static float GetMinimumDamageRatio(Agent attacker)
        {
            BasicCharacterObject character = attacker?.Character;
            if (character == null)
                return 0f;

            if (character.IsHero)
            {
                CharacterObject characterObject = character as CharacterObject;
                Hero hero = characterObject?.HeroObject;
                return hero == null ? 0f : MathF.Max(0f, hero.Level * 0.01f);
            }

            if (character.IsSoldier)
                return MathF.Max(0f, character.GetBattleTier() * 0.05f);

            return 0f;
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

            return MathF.Clamp(
                MathF.Max(armorResult, minimumResult),
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
                Random.NextDouble() > 0.5)
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
                Random.NextDouble() > 0.5)
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