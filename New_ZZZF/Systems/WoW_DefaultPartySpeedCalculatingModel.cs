using System;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace New_ZZZF  
{
    // Token: 0x02000122 RID: 290
    public class WOW_DefaultPartySpeedCalculatingModel : DefaultPartySpeedCalculatingModel
    {
        // Token: 0x1700061C RID: 1564
        // (get) Token: 0x060016BE RID: 5822 RVA: 0x0006EBD2 File Offset: 0x0006CDD2
        public override float BaseSpeed
        {
            get
            {
                return 7.5f;
            }
        }

        // Token: 0x1700061D RID: 1565
        // (get) Token: 0x060016BF RID: 5823 RVA: 0x0006EBD9 File Offset: 0x0006CDD9
        public override float MinimumSpeed
        {
            get
            {
                return 3f;
            }
        }

        // Token: 0x060016C0 RID: 5824 RVA: 0x0006EBE0 File Offset: 0x0006CDE0
        public override ExplainedNumber CalculateBaseSpeed(MobileParty mobileParty, bool includeDescriptions = false, int additionalTroopOnFootCount = 0, int additionalTroopOnHorseCount = 0)
        {
            PartyBase party = mobileParty.Party;
            int num = 0;
            float num2 = 0f;
            int num3 = 0;
            int num4 = mobileParty.MemberRoster.TotalManCount + additionalTroopOnFootCount + additionalTroopOnHorseCount;
            AddCargoStats(mobileParty, ref num, ref num2, ref num3);
            float num5 = mobileParty.ItemRoster.TotalWeight;
            int num6 = (int)Campaign.Current.Models.InventoryCapacityModel.CalculateInventoryCapacity(mobileParty, false, additionalTroopOnFootCount, additionalTroopOnHorseCount, 0, false).ResultNumber;
            int num7 = party.NumberOfMenWithHorse + additionalTroopOnHorseCount;
            int num8 = party.NumberOfMenWithoutHorse + additionalTroopOnFootCount;
            int num9 = party.MemberRoster.TotalWounded;
            int num10 = party.PrisonRoster.TotalManCount;
            float morale = mobileParty.Morale;
            if (mobileParty.AttachedParties.Count != 0)
            {
                foreach (MobileParty mobileParty2 in mobileParty.AttachedParties)
                {
                    AddCargoStats(mobileParty2, ref num, ref num2, ref num3);
                    num4 += mobileParty2.MemberRoster.TotalManCount;
                    num5 += mobileParty2.ItemRoster.TotalWeight;
                    num6 += mobileParty2.InventoryCapacity;
                    num7 += mobileParty2.Party.NumberOfMenWithHorse;
                    num8 += mobileParty2.Party.NumberOfMenWithoutHorse;
                    num9 += mobileParty2.MemberRoster.TotalWounded;
                    num10 += mobileParty2.PrisonRoster.TotalManCount;
                }
            }
            float baseNumber = 0;
            //添加设定,如果有部队指挥官或者斥候,则移速基础为7.5,如果没有,移速基础为5//做简单点,直接按有没有英雄单位进行判定
            foreach (TroopRosterElement item in mobileParty.MemberRoster.GetTroopRoster())
            {
                if (!item.Character.IsHero)
                {
                    baseNumber = 5;
                }
                else
                {
                    baseNumber = this.CalculateBaseSpeedForParty(num4);//修改这个函数,移除掉部队人数对移速的影响
                    break;
                }
            }
            ExplainedNumber result = new ExplainedNumber(baseNumber, includeDescriptions, null);
            bool flag = Campaign.Current.Models.MapWeatherModel.GetWeatherEffectOnTerrainForPosition(mobileParty.Position2D) == MapWeatherModel.WeatherEventEffectOnTerrain.Wet;
            this.GetFootmenPerkBonus(mobileParty, num4, num8, ref result);
            float cavalryRatioModifier = this.GetCavalryRatioModifier(num4, num7);
            int num11 = TaleWorlds.Library.MathF.Min(num8, num);
            float mountedFootmenRatioModifier = this.GetMountedFootmenRatioModifier(num4, num11);
            result.AddFactor(cavalryRatioModifier, _textCavalry);
            result.AddFactor(mountedFootmenRatioModifier, _textMountedFootmen);
            if (flag)
            {
                float num12 = cavalryRatioModifier * 0.3f;
                float num13 = mountedFootmenRatioModifier * 0.3f;
                result.AddFactor(-num12, _textCavalryWeatherPenalty);
                result.AddFactor(-num13, _textMountedFootmenWeatherPenalty);
            }
            if (mountedFootmenRatioModifier > 0f && mobileParty.LeaderHero != null && mobileParty.LeaderHero.GetPerkValue(DefaultPerks.Riding.NomadicTraditions))
            {
                result.AddFactor(mountedFootmenRatioModifier * DefaultPerks.Riding.NomadicTraditions.PrimaryBonus, DefaultPerks.Riding.NomadicTraditions.Name);
            }
            float num14 = TaleWorlds.Library.MathF.Min(num5, (float)num6);
            if (num14 > 0f)
            {
                float cargoEffect = this.GetCargoEffect(num14, num6);
                result.AddFactor(cargoEffect, _textCargo);
            }
            if (num2 > (float)num6)
            {
                float overBurdenedEffect = this.GetOverBurdenedEffect(num2 - (float)num6, num6);
                result.AddFactor(overBurdenedEffect, _textOverburdened);
                if (mobileParty.HasPerk(DefaultPerks.Athletics.Energetic, false))
                {
                    result.AddFactor(overBurdenedEffect * DefaultPerks.Athletics.Energetic.PrimaryBonus, DefaultPerks.Athletics.Energetic.Name);
                }
                if (mobileParty.HasPerk(DefaultPerks.Scouting.Unburdened, false))
                {
                    result.AddFactor(overBurdenedEffect * DefaultPerks.Scouting.Unburdened.PrimaryBonus, DefaultPerks.Scouting.Unburdened.Name);
                }
            }
            if (mobileParty.HasPerk(DefaultPerks.Riding.SweepingWind, true))
            {
                result.AddFactor(DefaultPerks.Riding.SweepingWind.SecondaryBonus, DefaultPerks.Riding.SweepingWind.Name);
            }
            if (mobileParty.Party.NumberOfAllMembers > mobileParty.Party.PartySizeLimit)
            {
                float overPartySizeEffect = this.GetOverPartySizeEffect(mobileParty);
                result.AddFactor(overPartySizeEffect, _textOverPartySize);
            }
            num3 += TaleWorlds.Library.MathF.Max(0, num - num11);
            if (!mobileParty.IsVillager)
            {
                float herdingModifier = this.GetHerdingModifier(num4, num3);
                result.AddFactor(herdingModifier, _textHerd);
                if (mobileParty.HasPerk(DefaultPerks.Riding.Shepherd, false))//牧羊人perk的效果翻倍,直接删除牲畜减移速
                {
                    result.AddFactor(herdingModifier * DefaultPerks.Riding.Shepherd.PrimaryBonus * 2, DefaultPerks.Riding.Shepherd.Name);
                }
            }
            float woundedModifier = this.GetWoundedModifier(num4, num9, mobileParty);
            result.AddFactor(woundedModifier, _textWounded);
            if (!mobileParty.IsCaravan)
            {
                if (mobileParty.Party.NumberOfPrisoners > mobileParty.Party.PrisonerSizeLimit)
                {
                    float overPrisonerSizeEffect = this.GetOverPrisonerSizeEffect(mobileParty);
                    result.AddFactor(overPrisonerSizeEffect, _textOverPrisonerSize);
                }
                float sizeModifierPrisoner = GetSizeModifierPrisoner(num4, num10);
                result.AddFactor(1f / sizeModifierPrisoner - 1f, _textPrisoners);
            }
            if (morale > 70f)
            {
                result.AddFactor(0.05f * ((morale - 70f) / 30f), _textHighMorale);
            }
            if (morale < 30f)
            {
                result.AddFactor(-0.1f * (1f - mobileParty.Morale / 30f), _textLowMorale);
            }
            if (mobileParty == MobileParty.MainParty)
            {
                float playerMapMovementSpeedBonusMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerMapMovementSpeedBonusMultiplier();
                if (playerMapMovementSpeedBonusMultiplier > 0f)
                {
                    result.AddFactor(playerMapMovementSpeedBonusMultiplier, GameTexts.FindText("str_game_difficulty", null));
                }
            }
            if (mobileParty.IsCaravan)
            {
                result.AddFactor(0.1f, _textCaravan);
            }
            if (mobileParty.IsDisorganized)
            {
                result.AddFactor(-0.4f, _textDisorganized);
            }
            result.LimitMin(this.MinimumSpeed);
            return result;
        }

        // Token: 0x060016C1 RID: 5825 RVA: 0x0006F0E8 File Offset: 0x0006D2E8
        private static void AddCargoStats(MobileParty mobileParty, ref int numberOfAvailableMounts, ref float totalWeightCarried, ref int herdSize)
        {
            ItemRoster itemRoster = mobileParty.ItemRoster;
            int numberOfPackAnimals = itemRoster.NumberOfPackAnimals;
            int numberOfLivestockAnimals = itemRoster.NumberOfLivestockAnimals;
            herdSize += numberOfPackAnimals + numberOfLivestockAnimals;
            numberOfAvailableMounts += itemRoster.NumberOfMounts;
            totalWeightCarried += itemRoster.TotalWeight;
        }

        // Token: 0x060016C2 RID: 5826 RVA: 0x0006F128 File Offset: 0x0006D328
        private float CalculateBaseSpeedForParty(int menCount)
        {
            return this.BaseSpeed;
        }

        // Token: 0x060016C3 RID: 5827 RVA: 0x0006F14C File Offset: 0x0006D34C
        public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
        {
            if (mobileParty.IsCustomParty && !((CustomPartyComponent)mobileParty.PartyComponent).BaseSpeed.ApproximatelyEqualsTo(0f, 1E-05f))
            {
                finalSpeed = new ExplainedNumber(((CustomPartyComponent)mobileParty.PartyComponent).BaseSpeed, false, null);
            }
            TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
            Hero effectiveScout = mobileParty.EffectiveScout;
            if (faceTerrainType == TerrainType.Forest)
            {
                float num = 0f;
                if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.ForestKin))
                {
                    for (int i = 0; i < mobileParty.MemberRoster.Count; i++)
                    {
                        if (!mobileParty.MemberRoster.GetCharacterAtIndex(i).IsMounted)
                        {
                            num += (float)mobileParty.MemberRoster.GetElementNumber(i);
                        }
                    }
                }
                float value = ((num / (float)mobileParty.MemberRoster.TotalManCount >= 0.75f) ? (-0.3f * -DefaultPerks.Scouting.ForestKin.PrimaryBonus) : (-0.3f));
                finalSpeed.AddFactor(value, _movingInForest);
                if (PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.BattanianForestSpeedFeat))
                {
                    float value2 = DefaultCulturalFeats.BattanianForestSpeedFeat.EffectBonus * 0.3f;
                    finalSpeed.AddFactor(value2, _culture);
                }
            }
            else if (faceTerrainType == TerrainType.Water || faceTerrainType == TerrainType.River || faceTerrainType == TerrainType.Bridge || faceTerrainType == TerrainType.Fording)
            {
                finalSpeed.AddFactor(-0.3f, _fordEffect);
            }
            else if (faceTerrainType == TerrainType.Desert || faceTerrainType == TerrainType.Dune)
            {
                if (!PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.AseraiDesertFeat))
                {
                    finalSpeed.AddFactor(-0.1f, _desert);
                }
                if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.DesertBorn))
                {
                    finalSpeed.AddFactor(DefaultPerks.Scouting.DesertBorn.PrimaryBonus, DefaultPerks.Scouting.DesertBorn.Name);
                }
            }
            else if ((faceTerrainType == TerrainType.Plain || faceTerrainType == TerrainType.Steppe) && effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.Pathfinder))
            {
                finalSpeed.AddFactor(DefaultPerks.Scouting.Pathfinder.PrimaryBonus, DefaultPerks.Scouting.Pathfinder.Name);
            }
            MapWeatherModel.WeatherEvent weatherEventInPosition = Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(mobileParty.Position2D);
            if (weatherEventInPosition == MapWeatherModel.WeatherEvent.Snowy || weatherEventInPosition == MapWeatherModel.WeatherEvent.Blizzard)
            {
                finalSpeed.AddFactor(-0.1f, _snow);
            }
            if (Campaign.Current.IsNight)
            {
                finalSpeed.AddFactor(-0.25f, _night);
                if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.NightRunner))
                {
                    finalSpeed.AddFactor(DefaultPerks.Scouting.NightRunner.PrimaryBonus, DefaultPerks.Scouting.NightRunner.Name);
                }
            }
            else if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.DayTraveler))
            {
                finalSpeed.AddFactor(DefaultPerks.Scouting.DayTraveler.PrimaryBonus, DefaultPerks.Scouting.DayTraveler.Name);
            }
            if (effectiveScout != null)
            {
                PerkHelper.AddEpicPerkBonusForCharacter(DefaultPerks.Scouting.UncannyInsight, effectiveScout.CharacterObject, DefaultSkills.Scouting, true, ref finalSpeed, Campaign.Current.Models.CharacterDevelopmentModel.MinSkillRequiredForEpicPerkBonus);
                if (effectiveScout.GetPerkValue(DefaultPerks.Scouting.ForcedMarch) && mobileParty.Morale > 75f)
                {
                    finalSpeed.AddFactor(DefaultPerks.Scouting.ForcedMarch.PrimaryBonus, DefaultPerks.Scouting.ForcedMarch.Name);
                }
                if (mobileParty.DefaultBehavior == AiBehavior.EngageParty)
                {
                    MobileParty targetParty = mobileParty.TargetParty;
                    if (targetParty != null && targetParty.MapFaction.IsAtWarWith(mobileParty.MapFaction) && effectiveScout.GetPerkValue(DefaultPerks.Scouting.Tracker))
                    {
                        finalSpeed.AddFactor(DefaultPerks.Scouting.Tracker.SecondaryBonus, DefaultPerks.Scouting.Tracker.Name);
                    }
                }
            }
            Army army = mobileParty.Army;
            if (((army != null) ? army.LeaderParty : null) != null && mobileParty.Army.LeaderParty != mobileParty && mobileParty.AttachedTo != mobileParty.Army.LeaderParty && mobileParty.Army.LeaderParty.HasPerk(DefaultPerks.Tactics.CallToArms, false))
            {
                finalSpeed.AddFactor(DefaultPerks.Tactics.CallToArms.PrimaryBonus, DefaultPerks.Tactics.CallToArms.Name);
            }
            finalSpeed.LimitMin(this.MinimumSpeed);
            return finalSpeed;
        }

        // Token: 0x060016C4 RID: 5828 RVA: 0x0006F517 File Offset: 0x0006D717
        private float GetCargoEffect(float weightCarried, int partyCapacity)
        {
            return -0.02f * weightCarried / (float)partyCapacity;
        }

        // Token: 0x060016C5 RID: 5829 RVA: 0x0006F524 File Offset: 0x0006D724
        private float GetOverBurdenedEffect(float totalWeightCarried, int partyCapacity)
        {
            return -0.4f * (totalWeightCarried / (float)partyCapacity);
        }

        // Token: 0x060016C6 RID: 5830 RVA: 0x0006F534 File Offset: 0x0006D734
        private float GetOverPartySizeEffect(MobileParty mobileParty)
        {
            int partySizeLimit = mobileParty.Party.PartySizeLimit;
            int numberOfAllMembers = mobileParty.Party.NumberOfAllMembers;
            return 1f / ((float)numberOfAllMembers / (float)partySizeLimit) - 1f;
        }

        // Token: 0x060016C7 RID: 5831 RVA: 0x0006F56C File Offset: 0x0006D76C
        private float GetOverPrisonerSizeEffect(MobileParty mobileParty)
        {
            int prisonerSizeLimit = mobileParty.Party.PrisonerSizeLimit;
            int numberOfPrisoners = mobileParty.Party.NumberOfPrisoners;
            return 1f / ((float)numberOfPrisoners / (float)prisonerSizeLimit) - 1f;
        }

        // Token: 0x060016C8 RID: 5832 RVA: 0x0006F5A2 File Offset: 0x0006D7A2
        private float GetHerdingModifier(int totalMenCount, int herdSize)
        {
            herdSize -= totalMenCount;
            if (herdSize <= 0)
            {
                return 0f;
            }
            if (totalMenCount == 0)
            {
                return -0.8f;
            }
            return TaleWorlds.Library.MathF.Max(-0.8f, -0.3f * ((float)herdSize / (float)totalMenCount));
        }

        // Token: 0x060016C9 RID: 5833 RVA: 0x0006F5D4 File Offset: 0x0006D7D4
        private float GetWoundedModifier(int totalMenCount, int numWounded, MobileParty party)
        {
            if (numWounded <= totalMenCount / 4)
            {
                return 0f;
            }
            if (totalMenCount == 0)
            {
                return -0.5f;
            }
            float baseNumber = TaleWorlds.Library.MathF.Max(-0.8f, -0.05f * (float)numWounded / (float)totalMenCount);
            ExplainedNumber explainedNumber = new ExplainedNumber(baseNumber, false, null);
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Medicine.Sledges, party, true, ref explainedNumber);
            return explainedNumber.ResultNumber;
        }

        // Token: 0x060016CA RID: 5834 RVA: 0x0006F62B File Offset: 0x0006D82B
        private float GetCavalryRatioModifier(int totalMenCount, int totalCavalryCount)
        {
            if (totalMenCount == 0 || totalCavalryCount == 0)
            {
                return 0f;
            }
            return 0.4f * (float)totalCavalryCount / (float)totalMenCount;
        }

        // Token: 0x060016CB RID: 5835 RVA: 0x0006F644 File Offset: 0x0006D844
        private float GetMountedFootmenRatioModifier(int totalMenCount, int totalMountedFootmenCount)
        {
            if (totalMenCount == 0 || totalMountedFootmenCount == 0)
            {
                return 0f;
            }
            return 0.2f * (float)totalMountedFootmenCount / (float)totalMenCount;
        }

        // Token: 0x060016CC RID: 5836 RVA: 0x0006F660 File Offset: 0x0006D860
        private void GetFootmenPerkBonus(MobileParty party, int totalMenCount, int totalFootmenCount, ref ExplainedNumber result)
        {
            if (totalMenCount == 0)
            {
                return;
            }
            float num = (float)totalFootmenCount / (float)totalMenCount;
            if (party.HasPerk(DefaultPerks.Athletics.Strong, true) && !num.ApproximatelyEqualsTo(0f, 1E-05f))
            {
                result.AddFactor(num * DefaultPerks.Athletics.Strong.SecondaryBonus, DefaultPerks.Athletics.Strong.Name);
            }
        }

        // Token: 0x060016CD RID: 5837 RVA: 0x0006F6B4 File Offset: 0x0006D8B4
        private static float GetSizeModifierWounded(int totalMenCount, int totalWoundedMenCount)
        {
            return TaleWorlds.Library.MathF.Pow((10f + (float)totalMenCount) / (10f + (float)totalMenCount - (float)totalWoundedMenCount), 0.33f);
        }

        // Token: 0x060016CE RID: 5838 RVA: 0x0006F6D4 File Offset: 0x0006D8D4
        private static float GetSizeModifierPrisoner(int totalMenCount, int totalPrisonerCount)
        {
            return TaleWorlds.Library.MathF.Pow((10f + (float)totalMenCount + (float)totalPrisonerCount) / (10f + (float)totalMenCount), 0.33f);
        }

        // Token: 0x040007E8 RID: 2024
        private static readonly TextObject _textCargo = new TextObject("{=fSGY71wd}Cargo within capacity", null);

        // Token: 0x040007E9 RID: 2025
        private static readonly TextObject _textOverburdened = new TextObject("{=xgO3cCgR}Overburdened", null);

        // Token: 0x040007EA RID: 2026
        private static readonly TextObject _textOverPartySize = new TextObject("{=bO5gL3FI}Men within party size", null);

        // Token: 0x040007EB RID: 2027
        private static readonly TextObject _textOverPrisonerSize = new TextObject("{=Ix8YjLPD}Men within prisoner size", null);

        // Token: 0x040007EC RID: 2028
        private static readonly TextObject _textCavalry = new TextObject("{=YVGtcLHF}Cavalry", null);

        // Token: 0x040007ED RID: 2029
        private static readonly TextObject _textCavalryWeatherPenalty = new TextObject("{=Cb0k9KM8}Cavalry weather penalty", null);

        // Token: 0x040007EE RID: 2030
        private static readonly TextObject _textKhuzaitCavalryBonus = new TextObject("{=yi07dBks}Khuzait cavalry bonus", null);

        // Token: 0x040007EF RID: 2031
        private static readonly TextObject _textMountedFootmen = new TextObject("{=5bSWSaPl}Footmen on horses", null);

        // Token: 0x040007F0 RID: 2032
        private static readonly TextObject _textMountedFootmenWeatherPenalty = new TextObject("{=JAKoFNgt}Footmen on horses weather penalty", null);

        // Token: 0x040007F1 RID: 2033
        private static readonly TextObject _textWounded = new TextObject("{=aLsVKIRy}Wounded members", null);

        // Token: 0x040007F2 RID: 2034
        private static readonly TextObject _textPrisoners = new TextObject("{=N6QTvjMf}Prisoners", null);

        // Token: 0x040007F3 RID: 2035
        private static readonly TextObject _textHerd = new TextObject("{=NhAMSaWU}Herding", null);

        // Token: 0x040007F4 RID: 2036
        private static readonly TextObject _textHighMorale = new TextObject("{=aDQcIGfH}High morale", null);

        // Token: 0x040007F5 RID: 2037
        private static readonly TextObject _textLowMorale = new TextObject("{=ydspCDIy}Low morale", null);

        // Token: 0x040007F6 RID: 2038
        private static readonly TextObject _textCaravan = new TextObject("{=vvabqi2w}Caravan", null);

        // Token: 0x040007F7 RID: 2039
        private static readonly TextObject _textDisorganized = new TextObject("{=JuwBb2Yg}Disorganized", null);

        // Token: 0x040007F8 RID: 2040
        private static readonly TextObject _movingInForest = new TextObject("{=rTFaZCdY}Forest", null);

        // Token: 0x040007F9 RID: 2041
        private static readonly TextObject _fordEffect = new TextObject("{=NT5fwUuJ}Fording", null);

        // Token: 0x040007FA RID: 2042
        private static readonly TextObject _night = new TextObject("{=fAxjyMt5}Night", null);

        // Token: 0x040007FB RID: 2043
        private static readonly TextObject _snow = new TextObject("{=vLjgcdgB}Snow", null);

        // Token: 0x040007FC RID: 2044
        private static readonly TextObject _desert = new TextObject("{=ecUwABe2}Desert", null);

        // Token: 0x040007FD RID: 2045
        private static readonly TextObject _sturgiaSnowBonus = new TextObject("{=0VfEGekD}Sturgia snow bonus", null);

        // Token: 0x040007FE RID: 2046
        private static readonly TextObject _culture = GameTexts.FindText("str_culture", null);

        // Token: 0x040007FF RID: 2047
        private const float MovingAtForestEffect = -0.3f;

        // Token: 0x04000800 RID: 2048
        private const float MovingAtWaterEffect = -0.3f;

        // Token: 0x04000801 RID: 2049
        private const float MovingAtNightEffect = -0.25f;

        // Token: 0x04000802 RID: 2050
        private const float MovingOnSnowEffect = -0.1f;

        // Token: 0x04000803 RID: 2051
        private const float MovingInDesertEffect = -0.1f;

        // Token: 0x04000804 RID: 2052
        private const float CavalryEffect = 0.4f;

        // Token: 0x04000805 RID: 2053
        private const float MountedFootMenEffect = 0.2f;

        // Token: 0x04000806 RID: 2054
        private const float HerdEffect = -0.4f;

        // Token: 0x04000807 RID: 2055
        private const float WoundedEffect = -0.05f;

        // Token: 0x04000808 RID: 2056
        private const float CargoEffect = -0.02f;

        // Token: 0x04000809 RID: 2057
        private const float OverburdenedEffect = -0.4f;

        // Token: 0x0400080A RID: 2058
        private const float HighMoraleThresold = 70f;

        // Token: 0x0400080B RID: 2059
        private const float LowMoraleThresold = 30f;

        // Token: 0x0400080C RID: 2060
        private const float HighMoraleEffect = 0.05f;

        // Token: 0x0400080D RID: 2061
        private const float LowMoraleEffect = -0.1f;

        // Token: 0x0400080E RID: 2062
        private const float DisorganizedEffect = -0.4f;
    }
}

