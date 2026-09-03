using Helpers;
using SandBox.GameComponents;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using MathF = TaleWorlds.Library.MathF;

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
    }

    internal static class ZZZFBlockBreakRules
    {
        public static bool Decide(
            Agent attackerAgent,
            Agent defenderAgent,
            float totalAttackEnergy,
            Agent.UsageDirection attackDirection,
            StrikeType strikeType,
            WeaponComponentData defendItem,
            bool isPassiveUsage)
        {
            if (attackerAgent == null || defenderAgent == null
                || !attackerAgent.IsActive() || !defenderAgent.IsActive())
                return false;

            EquipmentIndex attackerOffHand = attackerAgent.GetOffhandWieldedItemIndex();
            EquipmentIndex attackerMainHand = attackerAgent.GetPrimaryWieldedItemIndex();

            WeaponComponentData attackerWeapon =
                attackerMainHand != EquipmentIndex.None
                    ? attackerAgent.Equipment[attackerMainHand].CurrentUsageItem
                    : null;

            if (attackerWeapon == null)
                return false;

            EquipmentIndex defenderIndex = defenderAgent.GetPrimaryWieldedItemIndex();
            WeaponComponentData defenderWeapon =
                defenderIndex != EquipmentIndex.None
                    ? defenderAgent.Equipment[defenderIndex].CurrentUsageItem
                    : null;

            if (defenderWeapon == null)
                return true;

            if (SkillSystemBehavior.ActiveComponents != null &&
                SkillSystemBehavior.ActiveComponents.TryGetValue(attackerAgent.Index, out var attackerComponent) &&
                attackerComponent.HasSkill("Power"))
            {
                return true;
            }

            if (defendItem != null &&
                !defendItem.IsShield &&
                strikeType == StrikeType.Thrust)
            {
                return true;
            }

            int proficiencyDifference =
                attackerAgent.Character.GetSkillValue(attackerWeapon.RelevantSkill) -
                defenderAgent.Character.GetSkillValue(defenderWeapon.RelevantSkill);

            int attackerMovementSkill = GetMovementSkill(attackerAgent);
            int defenderMovementSkill = GetMovementSkill(defenderAgent);

            float threshold = 58f;

            if (attackerWeapon.RelevantSkill == DefaultSkills.TwoHanded ||
                (attackerOffHand == EquipmentIndex.None &&
                 attackerWeapon.RelevantSkill == DefaultSkills.Polearm))
            {
                totalAttackEnergy *= 1.2f;

                if (proficiencyDifference > 0)
                {
                    totalAttackEnergy *=
                        1f + proficiencyDifference / 500f;
                }
            }

            if (defendItem != null &&
                !defendItem.IsShield &&
                defenderAgent.Mount == null &&
                attackerAgent.Mount == null)
            {
                threshold -=
                    (attackerMovementSkill - defenderMovementSkill) *
                    0.05f;
            }

            threshold -= proficiencyDifference * 0.05f;

            if (isPassiveUsage)
                threshold /= 2f;

            if (defendItem != null && defendItem.IsShield)
                threshold *= 1.2f;

            TryDisarm(
                attackerAgent,
                defenderAgent,
                attackerWeapon,
                proficiencyDifference,
                attackerMovementSkill,
                defenderMovementSkill);

            return totalAttackEnergy > threshold;
        }

        private static int GetMovementSkill(Agent agent)
        {
            return agent.Mount != null
                ? agent.Character.GetSkillValue(DefaultSkills.Riding)
                : agent.Character.GetSkillValue(DefaultSkills.Athletics);
        }

        private static void TryDisarm(
            Agent attackerAgent,
            Agent defenderAgent,
            WeaponComponentData attackerWeapon,
            int proficiencyDifference,
            int attackerMovementSkill,
            int defenderMovementSkill)
        {
            if (attackerWeapon.WeaponClass != WeaponClass.OneHandedAxe &&
                attackerWeapon.WeaponClass != WeaponClass.TwoHandedAxe)
            {
                return;
            }

            float disarmChance =
                0.2f +
                proficiencyDifference / 500f *
                (1f +
                 (attackerMovementSkill - defenderMovementSkill) / 1000f);

            if (disarmChance <= MBRandom.RandomFloat)
                return;

            EquipmentIndex wieldedIndex =
                defenderAgent.GetOffhandWieldedItemIndex();

            if (wieldedIndex == EquipmentIndex.None)
                wieldedIndex = defenderAgent.GetPrimaryWieldedItemIndex();

            if (wieldedIndex == EquipmentIndex.None)
                return;

            // AV 根因修复：DropItem 会触碰原生 Agent/装备状态，绝不能在伤害/格挡判定调用栈内
            // 直接执行（DeferredDisarmPatch 的 Harmony 补丁可能因 PatchAll 失败而不在场）。
            // 改为标记延迟，由 SkillSystemBehavior.OnMissionTick 后的安全阶段统一执行。
            DeferredDisarmExecutor.Mark(defenderAgent, wieldedIndex);
        }
    }

    public class StrikeMagnitudeScript
    {
        public static Random random = new Random();

        public static float WOW_Script_AgentStatCalculateModel(Agent agent, float native)
        {
            SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var result);
            if (result != null)
            {
                if (result.StateContainer.HasState("ZhanYiBuff"))
                    native *= 1 + result._currentStamina / 100 * 2;
                if (result.StateContainer.HasState("JueXingBuff"))
                    native *= 1 + result._currentStamina / 100 / 2;
                if (result.StateContainer.HasState("TianQiBuff"))
                    native *= 2;
                if (result.StateContainer.HasState("ZhanHaoBuff"))
                    native *= 1.2f;
                if (result.StateContainer.HasState("WeiYaBuff"))
                    native *= 0.75f;
                if (result.StateContainer.HasState("YingXiongZhuFuBuff"))
                    native *= 2f;
                if (result.StateContainer.HasState("KongNueCiFuBuff"))
                    native *= 3f;
                if (result.StateContainer.HasState("NaGouCiFuBuff"))
                    native *= 1.5f;
                if (result.StateContainer.HasState("XuRuoZuZhouBuffToEnemy"))
                    native *= 0.5f;
                if (result.StateContainer.HasState("BKBBuff"))
                    native *= 2f;
                if (result.StateContainer.HasState("FengBaoZhiLiBuff"))
                {
                    Script.AgentGetCurrentWeapon(agent, out var missionWeapon);
                    if (Script.IsRangeWeapon(missionWeapon.Item))
                        native *= 3f;
                }
            }

            return native;
        }
    }

    public class WOW_SandboxStrikeMagnitudeModel : SandboxStrikeMagnitudeModel
    {
        public override float CalculateStrikeMagnitudeForMissile(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            in MissionWeapon weapon,
            float missileSpeed)
        {
            if (SkillSystemBehavior.WoW_WeaponMissile.ContainsKey(collisionData.AffectorWeaponSlotOrMissileIndex) &&
                (weapon.Item.PrimaryWeapon.WeaponClass == WeaponClass.Arrow ||
                 weapon.Item.PrimaryWeapon.WeaponClass == WeaponClass.Bolt))
            {
                SkillSystemBehavior.WoW_WeaponMissile.TryGetValue(
                    collisionData.AffectorWeaponSlotOrMissileIndex,
                    out int weaponDamage);

                float baseDam = base.CalculateStrikeMagnitudeForMissile(
                    attackInformation,
                    collisionData,
                    weapon,
                    missileSpeed);
                float mtd = collisionData.MissileTotalDamage;
                if (baseDam == 0) baseDam = 1;
                if (mtd == 0) mtd = 1;
                if (weaponDamage == 0) weaponDamage = (int)mtd;

                return baseDam / mtd * (weaponDamage + collisionData.MissileTotalDamage);
            }

            if (SkillSystemBehavior.WoW_WeaponMissile.ContainsKey(collisionData.AffectorWeaponSlotOrMissileIndex) &&
                weapon.CurrentUsageItem.IsConsumable &&
                weapon.CurrentUsageItem.IsRangedWeapon)
            {
                SkillSystemBehavior.WoW_WeaponMissile.TryGetValue(
                    collisionData.AffectorWeaponSlotOrMissileIndex,
                    out int weaponDamage);

                float baseDam = base.CalculateStrikeMagnitudeForMissile(
                    attackInformation,
                    collisionData,
                    weapon,
                    missileSpeed);
                float mtd = collisionData.MissileTotalDamage;
                if (baseDam == 0) baseDam = 1;
                if (mtd == 0) mtd = 1;
                if (weaponDamage == 0) weaponDamage = (int)mtd;

                return baseDam / mtd * weaponDamage;
            }

            float result = base.CalculateStrikeMagnitudeForMissile(
                attackInformation,
                collisionData,
                weapon,
                missileSpeed);

            string? affixInstId = AffixMissionBehavior.GetAgentWeaponInstanceId(
                attackInformation.AttackerAgent,
                (EquipmentIndex)collisionData.AffectorWeaponSlotOrMissileIndex);

            result *= AffixCampaignBehavior.GetAffixDamageMultiplier(
                affixInstId,
                weapon.Item,
                "MissileDamage");

            return result;
        }

        public override float CalculateStrikeMagnitudeForSwing(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            in MissionWeapon weapon,
            float swingSpeed,
            float impactPointAsPercent,
            float extraLinearSpeed)
        {
            BasicCharacterObject attackerAgentCharacter = attackInformation.AttackerAgentCharacter;
            BasicCharacterObject attackerCaptainCharacter = attackInformation.AttackerCaptainCharacter;
            bool doesAttackerHaveMountAgent = attackInformation.DoesAttackerHaveMountAgent;
            MissionWeapon missionWeapon = weapon;
            WeaponComponentData currentUsageItem = missionWeapon.CurrentUsageItem;
            CharacterObject characterObject = attackerAgentCharacter as CharacterObject;
            ExplainedNumber explainedNumber = new ExplainedNumber(extraLinearSpeed, false, null);

            if (characterObject != null && extraLinearSpeed > 0f)
            {
                SkillObject relevantSkill = currentUsageItem.RelevantSkill;
                CharacterObject captainCharacter = attackerCaptainCharacter as CharacterObject;

                if (doesAttackerHaveMountAgent)
                {
                    PerkHelper.AddPerkBonusFromCaptain(
                        DefaultPerks.Riding.NomadicTraditions,
                        captainCharacter,
                        ref explainedNumber);
                }
                else
                {
                    if (relevantSkill == DefaultSkills.TwoHanded)
                    {
                        PerkHelper.AddPerkBonusForCharacter(
                            DefaultPerks.TwoHanded.RecklessCharge,
                            characterObject,
                            true,
                            ref explainedNumber);
                    }

                    PerkHelper.AddPerkBonusForCharacter(
                        DefaultPerks.Roguery.DashAndSlash,
                        characterObject,
                        true,
                        ref explainedNumber);

                    PerkHelper.AddPerkBonusForCharacter(
                        DefaultPerks.Athletics.SurgingBlow,
                        characterObject,
                        true,
                        ref explainedNumber);

                    PerkHelper.AddPerkBonusFromCaptain(
                        DefaultPerks.Athletics.SurgingBlow,
                        captainCharacter,
                        ref explainedNumber);
                }

                if (relevantSkill == DefaultSkills.Polearm)
                {
                    PerkHelper.AddPerkBonusFromCaptain(
                        DefaultPerks.Polearm.Lancer,
                        captainCharacter,
                        ref explainedNumber);

                    if (doesAttackerHaveMountAgent)
                    {
                        PerkHelper.AddPerkBonusForCharacter(
                            DefaultPerks.Polearm.Lancer,
                            characterObject,
                            true,
                            ref explainedNumber);

                        PerkHelper.AddPerkBonusFromCaptain(
                            DefaultPerks.Polearm.UnstoppableForce,
                            captainCharacter,
                            ref explainedNumber);
                    }
                }
            }

            ItemObject item = weapon.Item;
            float num = CombatStatCalculator.CalculateStrikeMagnitudeForSwing(
                swingSpeed,
                currentUsageItem.SweetSpotReach,
                item.Weight,
                currentUsageItem.GetRealWeaponLength(),
                currentUsageItem.TotalInertia,
                currentUsageItem.CenterOfMass,
                explainedNumber.ResultNumber);

            if (item.IsCraftedByPlayer)
            {
                ExplainedNumber explainedNumber2 = new ExplainedNumber(num, false, null);
                PerkHelper.AddPerkBonusForCharacter(
                    DefaultPerks.Crafting.SharpenedEdge,
                    characterObject,
                    true,
                    ref explainedNumber2);
                num = explainedNumber2.ResultNumber;
            }

            string? affixInstId = AffixMissionBehavior.GetAgentWeaponInstanceId(
                attackInformation.AttackerAgent,
                (EquipmentIndex)collisionData.AffectorWeaponSlotOrMissileIndex);

            num *= AffixCampaignBehavior.GetAffixDamageMultiplier(
                affixInstId,
                item,
                "SwingDamage");

            return num;
        }

        public override float CalculateStrikeMagnitudeForThrust(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            in MissionWeapon weapon,
            float thrustWeaponSpeed,
            float extraLinearSpeed,
            bool isThrown = false)
        {
            BasicCharacterObject attackerAgentCharacter = attackInformation.AttackerAgentCharacter;
            BasicCharacterObject attackerCaptainCharacter = attackInformation.AttackerCaptainCharacter;
            bool doesAttackerHaveMountAgent = attackInformation.DoesAttackerHaveMountAgent;
            MissionWeapon missionWeapon = weapon;
            ItemObject item = missionWeapon.Item;
            float weight = item.Weight;
            WeaponComponentData currentUsageItem = missionWeapon.CurrentUsageItem;
            CharacterObject characterObject = attackerAgentCharacter as CharacterObject;
            ExplainedNumber explainedNumber = new ExplainedNumber(extraLinearSpeed, false, null);

            if (characterObject != null && extraLinearSpeed > 0f)
            {
                SkillObject relevantSkill = currentUsageItem.RelevantSkill;
                CharacterObject captainCharacter = attackerCaptainCharacter as CharacterObject;

                if (doesAttackerHaveMountAgent)
                {
                    PerkHelper.AddPerkBonusFromCaptain(
                        DefaultPerks.Riding.NomadicTraditions,
                        captainCharacter,
                        ref explainedNumber);
                }
                else
                {
                    if (relevantSkill == DefaultSkills.TwoHanded)
                    {
                        PerkHelper.AddPerkBonusForCharacter(
                            DefaultPerks.TwoHanded.RecklessCharge,
                            characterObject,
                            true,
                            ref explainedNumber);
                    }

                    PerkHelper.AddPerkBonusForCharacter(
                        DefaultPerks.Roguery.DashAndSlash,
                        characterObject,
                        true,
                        ref explainedNumber);

                    PerkHelper.AddPerkBonusForCharacter(
                        DefaultPerks.Athletics.SurgingBlow,
                        characterObject,
                        true,
                        ref explainedNumber);

                    PerkHelper.AddPerkBonusFromCaptain(
                        DefaultPerks.Athletics.SurgingBlow,
                        captainCharacter,
                        ref explainedNumber);
                }

                if (relevantSkill == DefaultSkills.Polearm)
                {
                    PerkHelper.AddPerkBonusFromCaptain(
                        DefaultPerks.Polearm.Lancer,
                        captainCharacter,
                        ref explainedNumber);

                    if (doesAttackerHaveMountAgent)
                    {
                        PerkHelper.AddPerkBonusForCharacter(
                            DefaultPerks.Polearm.Lancer,
                            characterObject,
                            true,
                            ref explainedNumber);

                        PerkHelper.AddPerkBonusFromCaptain(
                            DefaultPerks.Polearm.UnstoppableForce,
                            captainCharacter,
                            ref explainedNumber);
                    }
                }
            }

            float num = CombatStatCalculator.CalculateStrikeMagnitudeForThrust(
                thrustWeaponSpeed,
                weight,
                explainedNumber.ResultNumber,
                isThrown);

            // Disabled: do not force thrust magnitude up to the weapon's modified thrust damage.
            // num = MathF.Max(
            //     num,
            //     (float)weapon.GetModifiedThrustDamageForCurrentUsage());

            if (item.IsCraftedByPlayer)
            {
                ExplainedNumber explainedNumber2 = new ExplainedNumber(num, false, null);
                PerkHelper.AddPerkBonusForCharacter(
                    DefaultPerks.Crafting.SharpenedTip,
                    characterObject,
                    true,
                    ref explainedNumber2);
                num = explainedNumber2.ResultNumber;
            }

            string? affixInstId = AffixMissionBehavior.GetAgentWeaponInstanceId(
                attackInformation.AttackerAgent,
                (EquipmentIndex)collisionData.AffectorWeaponSlotOrMissileIndex);

            num *= AffixCampaignBehavior.GetAffixDamageMultiplier(
                affixInstId,
                item,
                "ThrustDamage");

            return num;
        }
    }

    public class WOW_DefaultStrikeMagnitudeModel : DefaultStrikeMagnitudeModel
    {
        public override float CalculateStrikeMagnitudeForMissile(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            in MissionWeapon weapon,
            float missileSpeed)
        {
            if (SkillSystemBehavior.WoW_WeaponMissile.ContainsKey(collisionData.AffectorWeaponSlotOrMissileIndex) &&
                (weapon.Item.PrimaryWeapon.WeaponClass == WeaponClass.Arrow ||
                 weapon.Item.PrimaryWeapon.WeaponClass == WeaponClass.Bolt))
            {
                SkillSystemBehavior.WoW_WeaponMissile.TryGetValue(
                    collisionData.AffectorWeaponSlotOrMissileIndex,
                    out int weaponDamage);

                float baseDam = base.CalculateStrikeMagnitudeForMissile(
                    attackInformation,
                    collisionData,
                    weapon,
                    missileSpeed);
                float mtd = collisionData.MissileTotalDamage;
                if (baseDam == 0) baseDam = 1;
                if (mtd == 0) mtd = 1;

                return baseDam / mtd * (weaponDamage + collisionData.MissileTotalDamage);
            }

            if (SkillSystemBehavior.WoW_WeaponMissile.ContainsKey(collisionData.AffectorWeaponSlotOrMissileIndex) &&
                weapon.CurrentUsageItem.IsConsumable &&
                weapon.CurrentUsageItem.IsRangedWeapon)
            {
                SkillSystemBehavior.WoW_WeaponMissile.TryGetValue(
                    collisionData.AffectorWeaponSlotOrMissileIndex,
                    out int weaponDamage);

                float baseDam = base.CalculateStrikeMagnitudeForMissile(
                    attackInformation,
                    collisionData,
                    weapon,
                    missileSpeed);
                float mtd = collisionData.MissileTotalDamage;
                if (baseDam == 0) baseDam = 1;
                if (mtd == 0) mtd = 1;
                if (weaponDamage == 0) weaponDamage = (int)mtd;

                return baseDam / mtd * weaponDamage;
            }

            float result = base.CalculateStrikeMagnitudeForMissile(
                attackInformation,
                collisionData,
                weapon,
                missileSpeed);

            string? affixInstId = AffixMissionBehavior.GetAgentWeaponInstanceId(
                attackInformation.AttackerAgent,
                (EquipmentIndex)collisionData.AffectorWeaponSlotOrMissileIndex);

            result *= AffixCampaignBehavior.GetAffixDamageMultiplier(
                affixInstId,
                weapon.Item,
                "MissileDamage");

            return result;
        }

        public override float CalculateStrikeMagnitudeForSwing(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            in MissionWeapon weapon,
            float swingSpeed,
            float impactPointAsPercent,
            float extraLinearSpeed)
        {
            MissionWeapon missionWeapon = weapon;
            WeaponComponentData currentUsageItem = missionWeapon.CurrentUsageItem;

            float num = CombatStatCalculator.CalculateStrikeMagnitudeForSwing(
                swingSpeed,
                currentUsageItem.SweetSpotReach,
                missionWeapon.Item.Weight,
                currentUsageItem.GetRealWeaponLength(),
                currentUsageItem.TotalInertia,
                currentUsageItem.CenterOfMass,
                extraLinearSpeed);

            string? affixInstId = AffixMissionBehavior.GetAgentWeaponInstanceId(
                attackInformation.AttackerAgent,
                (EquipmentIndex)collisionData.AffectorWeaponSlotOrMissileIndex);

            num *= AffixCampaignBehavior.GetAffixDamageMultiplier(
                affixInstId,
                missionWeapon.Item,
                "SwingDamage");

            return num;
        }

        public override float CalculateStrikeMagnitudeForThrust(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            in MissionWeapon weapon,
            float thrustWeaponSpeed,
            float extraLinearSpeed,
            bool isThrown = false)
        {
            MissionWeapon missionWeapon = weapon;
            float num = CombatStatCalculator.CalculateStrikeMagnitudeForThrust(
                thrustWeaponSpeed,
                missionWeapon.Item.Weight,
                extraLinearSpeed,
                isThrown);

            // Disabled: do not force thrust magnitude up to the weapon's modified thrust damage.
            // num = MathF.Max(
            //     num,
            //     (float)weapon.GetModifiedThrustDamageForCurrentUsage());

            string? affixInstId = AffixMissionBehavior.GetAgentWeaponInstanceId(
                attackInformation.AttackerAgent,
                (EquipmentIndex)collisionData.AffectorWeaponSlotOrMissileIndex);

            num *= AffixCampaignBehavior.GetAffixDamageMultiplier(
                affixInstId,
                missionWeapon.Item,
                "ThrustDamage");

            return num;
        }
    }

    public class WOW_CustomBattleAgentStatCalculateModel : CustomBattleAgentStatCalculateModel
    {
        public override float GetWeaponDamageMultiplier(
            Agent agent,
            WeaponComponentData weapon)
        {
            float native = base.GetWeaponDamageMultiplier(agent, weapon);
            native = StrikeMagnitudeScript.WOW_Script_AgentStatCalculateModel(agent, native);
            return native;
        }
    }

    public class WOW_SandboxAgentApplyDamageModel : SandboxAgentApplyDamageModel
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

            float armor = attackInformation.ArmorAmountFloat;

            AttackInformation noArmor = attackInformation;
            noArmor.ArmorAmountFloat = 0f;

            float damageWithoutNativeArmor =
                base.ApplyDamageReductions(
                    in noArmor,
                    in collisionData,
                    baseDamage);

            float adjustedDamage =
                DamageCalculationRules.ApplyCampaignFinalRules(
                    in attackInformation,
                    damageWithoutNativeArmor);

            AttackInformation armorContext = attackInformation;
            armorContext.ArmorAmountFloat = armor;

            return DamageCalculationRules.ApplyRefactoredArmor(
                in armorContext,
                adjustedDamage);
        }
    }

    public class WOW_CustomAgentApplyDamageModel : CustomAgentApplyDamageModel
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

            return base.ApplyDamageReductions(
                in attackInformation,
                in collisionData,
                baseDamage);
        }
    }
}