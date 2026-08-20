using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace New_ZZZF
{
    /// <summary>
    /// Compatibility layer for Bannerlord 1.5.0 API signature changes.
    /// Legacy call sites remain unchanged; the shim forwards to the 1.5.0 APIs.
    /// </summary>
    internal static class PerkHelper
    {
        public static void AddPerkBonusFromCaptain(PerkObject perk, CharacterObject character, ref ExplainedNumber bonuses)
        {
            if (character == null)
                return;

            global::Helpers.PerkHelper.AddPerkBonusFromCaptain(
                perk,
                character.CurrentBattleEnvironment,
                character,
                ref bonuses);
        }

        public static void AddPerkBonusFromCaptain(PerkObject perk, BattleEnvironment battleEnvironment, CharacterObject character, ref ExplainedNumber bonuses)
        {
            global::Helpers.PerkHelper.AddPerkBonusFromCaptain(perk, battleEnvironment, character, ref bonuses);
        }

        public static void AddPerkBonusForCharacter(PerkObject perk, CharacterObject character, bool isPrimaryBonus, ref ExplainedNumber bonuses)
        {
            if (character == null)
                return;

            global::Helpers.PerkHelper.AddPerkBonusForCharacter(
                perk,
                character.CurrentBattleEnvironment,
                character,
                isPrimaryBonus,
                ref bonuses);
        }

        public static void AddPerkBonusForCharacter(PerkObject perk, BattleEnvironment battleEnvironment, CharacterObject character, bool isPrimaryBonus, ref ExplainedNumber bonuses)
        {
            global::Helpers.PerkHelper.AddPerkBonusForCharacter(perk, battleEnvironment, character, isPrimaryBonus, ref bonuses);
        }

        public static void AddEpicPerkBonusForCharacter(PerkObject perk, CharacterObject character, SkillObject skillType, bool isPrimaryBonus, ref ExplainedNumber bonuses, int skillRequired)
        {
            if (character == null)
                return;

            global::Helpers.PerkHelper.AddEpicPerkBonusForCharacter(
                perk,
                character.CurrentBattleEnvironment,
                character,
                skillType,
                isPrimaryBonus,
                ref bonuses,
                skillRequired);
        }

        public static void AddEpicPerkBonusForCharacter(PerkObject perk, BattleEnvironment battleEnvironment, CharacterObject character, SkillObject skillType, bool isPrimaryBonus, ref ExplainedNumber bonuses, int skillRequired)
        {
            global::Helpers.PerkHelper.AddEpicPerkBonusForCharacter(
                perk,
                battleEnvironment,
                character,
                skillType,
                isPrimaryBonus,
                ref bonuses,
                skillRequired);
        }

        public static void AddPerkBonusForParty(PerkObject perk, MobileParty party, bool isPrimaryBonus, ref ExplainedNumber bonuses)
        {
            global::Helpers.PerkHelper.AddPerkBonusForParty(perk, party, isPrimaryBonus, ref bonuses);
        }
    }

    internal static class Bannerlord150PartyPerkExtensions
    {
        public static bool HasPerk(this MobileParty mobileParty, PerkObject perk, bool checkSecondaryRole)
        {
            return mobileParty != null && mobileParty.HasPerk(perk, out Hero _, checkSecondaryRole);
        }
    }

    internal static class Bannerlord150TroopSelectionExtensions
    {
        public static void OpenTroopSelection(
            this MenuContext menuContext,
            TroopRoster fullRoster,
            TroopRoster initialSelections,
            object legacyEligibleShips,
            Func<CharacterObject, bool> canChangeStatusOfTroop,
            Action<TroopRoster> onDone,
            int maxSelectableTroopCount,
            int minSelectableTroopCount)
        {
            menuContext.OpenTroopSelection(
                fullRoster,
                initialSelections,
                canChangeStatusOfTroop,
                onDone,
                maxSelectableTroopCount,
                minSelectableTroopCount);
        }
    }
}
