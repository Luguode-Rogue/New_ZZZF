using Helpers;
using SandBox.GameComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{

    public class StrikeMagnitudeScript
    {
        public static float WOW_Script_AgentStatCalculateModel(Agent agent, float native)
        {
            return 1f;
            ////AgentStatCalculateModel输入agent后，根据agent的状态，附加额外的增伤数值
            //if (WoW_MissionSetting.WoW_Agents.TryGetValue(agent.Index, out var WOW_agent))
            //{
            //    if (WOW_agent.passiveSkill == "nuyikuangji")
            //    {
            //        float time = WOW_agent.passiveSkillDur;
            //        native = native + time * 0.5f;
            //        time += 5;
            //        WOW_agent.passiveSkillDur = time;
            //        InformationManager.DisplayMessage(new InformationMessage("nuyikuangji"));

            //    }
            //    if (WOW_agent.primarySkill == "mengliyiji" && WOW_agent.primarySkillDur > 0)
            //    {
            //        BasicCharacterObject characterObject = ((agent != null) ? agent.Character : null);//agent转troop
            //                                                                                          //对士兵，伤害增加10*（阶数-1）%，6阶兵增伤50%
            //        if (characterObject.IsSoldier)
            //        {
            //            native = native + 0.1f * (float)characterObject.GetBattleTier();
            //        }
            //        //对英雄为50+武器对应skill专精*skill对应属性
            //        if (characterObject.IsHero)
            //        {
            //            Hero hero = (characterObject as CharacterObject)?.HeroObject;
            //            // 获取Agent主手中武器的Index索引
            //            EquipmentIndex mainHandIndex = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
            //            if (mainHandIndex == EquipmentIndex.None || hero == null)
            //            {
            //                return native;
            //            }

            //            // EquipmentIndex转MissionWeapon转ItemObject
            //            MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
            //            ItemObject weapon = mainHandEquipmentElement.Item;
            //            SkillObject skillObject = ((weapon != null) ? weapon.RelevantSkill : null);

            //            if (characterObject != null && skillObject != null)
            //            {
            //                //foreach (CharacterAttribute characterAttribute in Attributes.All)
            //                //{ }
            //                //int zhuanjing=characterObject.GetSkillValue(skillObject);//日了狗这个是获取对应skill熟练度，不是专精

            //                //读取agent对应troop的skill专精等级以及skill对应的属性数值
            //                int shuxing = hero.GetAttributeValue(skillObject.CharacterAttribute);
            //                int zhuanjing = hero.HeroDeveloper.GetFocus(skillObject);
            //                native += 0.5f + shuxing * zhuanjing / 100f;

            //            }
            //        }
            //        WOW_agent.primarySkillDur = 0;

            //    }
            //}
            //return native;
        }
    }
    public class WOW_SandboxStrikeMagnitudeModel : SandboxStrikeMagnitudeModel
    {
        public override float CalculateStrikeMagnitudeForMissile(in AttackInformation attackInformation, in AttackCollisionData collisionData, in MissionWeapon weapon, float missileSpeed)
        {
            if (SkillSystemBehavior.WoW_WeaponMissile.ContainsKey(collisionData.AffectorWeaponSlotOrMissileIndex) && (weapon.Item.PrimaryWeapon.WeaponClass == WeaponClass.Arrow || weapon.Item.PrimaryWeapon.WeaponClass == WeaponClass.Bolt))// WoW_MissionSetting.WoW_WeaponMissile字典里有acd中的missile。index，则说明这个箭矢是我代码生成的，所以return出去的伤害数值=mtd+字典里武器的伤害
            {
                int weaponDamage;
                SkillSystemBehavior.WoW_WeaponMissile.TryGetValue(collisionData.AffectorWeaponSlotOrMissileIndex, out weaponDamage);
                float baseDam = base.CalculateStrikeMagnitudeForMissile(attackInformation, collisionData, weapon, missileSpeed);
                float mtd = collisionData.MissileTotalDamage;
                if (baseDam == 0) baseDam = 1;
                if (mtd == 0) mtd = 1;
                if (weaponDamage == 0) weaponDamage = (int)mtd;
                return baseDam / mtd * (weaponDamage + collisionData.MissileTotalDamage);
            }
            else if (SkillSystemBehavior.WoW_WeaponMissile.ContainsKey(collisionData.AffectorWeaponSlotOrMissileIndex) && weapon.CurrentUsageItem.IsConsumable && weapon.CurrentUsageItem.IsRangedWeapon)//对于自己生成的投掷类武器因为mtd值正确，所以不用weaponDamage+mtd。为了兼容非远程武器生成的投矛，所以用这个公式
            {
                int weaponDamage;
                SkillSystemBehavior.WoW_WeaponMissile.TryGetValue(collisionData.AffectorWeaponSlotOrMissileIndex, out weaponDamage);
                float baseDam = base.CalculateStrikeMagnitudeForMissile(attackInformation, collisionData, weapon, missileSpeed);
                float mtd = collisionData.MissileTotalDamage;
                if (baseDam == 0) baseDam = 1;
                if (mtd == 0) mtd = 1;
                if (weaponDamage == 0) weaponDamage = (int)mtd;
                return baseDam / mtd * (weaponDamage);

            }

            return base.CalculateStrikeMagnitudeForMissile(attackInformation, collisionData, weapon, missileSpeed);//如果字典里没有，说明是游戏源代码生成 的，所以直接输出原来acd里的mtd 

        }
        public override float CalculateStrikeMagnitudeForSwing(in AttackInformation attackInformation, in AttackCollisionData collisionData, in MissionWeapon weapon, float swingSpeed, float impactPointAsPercent, float extraLinearSpeed)
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
                    PerkHelper.AddPerkBonusFromCaptain(DefaultPerks.Riding.NomadicTraditions, captainCharacter, ref explainedNumber);
                }
                else
                {
                    if (relevantSkill == DefaultSkills.TwoHanded)
                    {
                        PerkHelper.AddPerkBonusForCharacter(DefaultPerks.TwoHanded.RecklessCharge, characterObject, true, ref explainedNumber);
                    }
                    PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Roguery.DashAndSlash, characterObject, true, ref explainedNumber);
                    PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Athletics.SurgingBlow, characterObject, true, ref explainedNumber);
                    PerkHelper.AddPerkBonusFromCaptain(DefaultPerks.Athletics.SurgingBlow, captainCharacter, ref explainedNumber);
                }
                if (relevantSkill == DefaultSkills.Polearm)
                {
                    PerkHelper.AddPerkBonusFromCaptain(DefaultPerks.Polearm.Lancer, captainCharacter, ref explainedNumber);
                    if (doesAttackerHaveMountAgent)
                    {
                        PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Polearm.Lancer, characterObject, true, ref explainedNumber);
                        PerkHelper.AddPerkBonusFromCaptain(DefaultPerks.Polearm.UnstoppableForce, captainCharacter, ref explainedNumber);
                    }
                }
            }
            missionWeapon = weapon;
            ItemObject item = missionWeapon.Item;
            float num = CombatStatCalculator.CalculateStrikeMagnitudeForSwing(swingSpeed, currentUsageItem.SweetSpotReach, item.Weight, currentUsageItem.GetRealWeaponLength(), currentUsageItem.Inertia, currentUsageItem.CenterOfMass, explainedNumber.ResultNumber);
            if (item.IsCraftedByPlayer)
            {
                ExplainedNumber explainedNumber2 = new ExplainedNumber(num, false, null);
                PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Crafting.SharpenedEdge, characterObject, true, ref explainedNumber2);
                num = explainedNumber2.ResultNumber;
            }
            return num;
        }
        public override float CalculateStrikeMagnitudeForThrust(in AttackInformation attackInformation, in AttackCollisionData collisionData, in MissionWeapon weapon, float thrustWeaponSpeed, float extraLinearSpeed, bool isThrown = false)
        {
            BasicCharacterObject attackerAgentCharacter = attackInformation.AttackerAgentCharacter;
            BasicCharacterObject attackerCaptainCharacter = attackInformation.AttackerCaptainCharacter;
            bool doesAttackerHaveMountAgent = attackInformation.DoesAttackerHaveMountAgent;
            MissionWeapon missionWeapon = weapon;
            ItemObject item = missionWeapon.Item;
            float weight = item.Weight;
            missionWeapon = weapon;
            WeaponComponentData currentUsageItem = missionWeapon.CurrentUsageItem;
            CharacterObject characterObject = attackerAgentCharacter as CharacterObject;
            ExplainedNumber explainedNumber = new ExplainedNumber(extraLinearSpeed, false, null);
            if (characterObject != null && extraLinearSpeed > 0f)
            {
                SkillObject relevantSkill = currentUsageItem.RelevantSkill;
                CharacterObject captainCharacter = attackerCaptainCharacter as CharacterObject;
                if (doesAttackerHaveMountAgent)
                {
                    PerkHelper.AddPerkBonusFromCaptain(DefaultPerks.Riding.NomadicTraditions, captainCharacter, ref explainedNumber);
                }
                else
                {
                    if (relevantSkill == DefaultSkills.TwoHanded)
                    {
                        PerkHelper.AddPerkBonusForCharacter(DefaultPerks.TwoHanded.RecklessCharge, characterObject, true, ref explainedNumber);
                    }
                    PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Roguery.DashAndSlash, characterObject, true, ref explainedNumber);
                    PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Athletics.SurgingBlow, characterObject, true, ref explainedNumber);
                    PerkHelper.AddPerkBonusFromCaptain(DefaultPerks.Athletics.SurgingBlow, captainCharacter, ref explainedNumber);
                }
                if (relevantSkill == DefaultSkills.Polearm)
                {
                    PerkHelper.AddPerkBonusFromCaptain(DefaultPerks.Polearm.Lancer, captainCharacter, ref explainedNumber);
                    if (doesAttackerHaveMountAgent)
                    {
                        PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Polearm.Lancer, characterObject, true, ref explainedNumber);
                        PerkHelper.AddPerkBonusFromCaptain(DefaultPerks.Polearm.UnstoppableForce, captainCharacter, ref explainedNumber);
                    }
                }
            }
            float num = CombatStatCalculator.CalculateStrikeMagnitudeForThrust(thrustWeaponSpeed, weight, explainedNumber.ResultNumber, isThrown);

            num = TaleWorlds.Library.MathF.Max(num, (float)weapon.GetModifiedThrustDamageForCurrentUsage());
            if (item.IsCraftedByPlayer)
            {
                ExplainedNumber explainedNumber2 = new ExplainedNumber(num, false, null);
                PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Crafting.SharpenedTip, characterObject, true, ref explainedNumber2);
                num = explainedNumber2.ResultNumber;
            }
            return num;
        }

    }
    public class WOW_DefaultStrikeMagnitudeModel : DefaultStrikeMagnitudeModel
    {
        public override float CalculateStrikeMagnitudeForMissile(in AttackInformation attackInformation, in AttackCollisionData collisionData, in MissionWeapon weapon, float missileSpeed)
        {
            Script Script = new Script();
            if (SkillSystemBehavior.WoW_WeaponMissile.ContainsKey(collisionData.AffectorWeaponSlotOrMissileIndex) && (weapon.Item.PrimaryWeapon.WeaponClass == WeaponClass.Arrow || weapon.Item.PrimaryWeapon.WeaponClass == WeaponClass.Bolt))// SkillSystemBehavior.WoW_WeaponMissile字典里有acd中的missile。index，则说明这个箭矢是我代码生成的，所以return出去的伤害数值=mtd+字典里武器的伤害
            {
                int weaponDamage;
                SkillSystemBehavior.WoW_WeaponMissile.TryGetValue(collisionData.AffectorWeaponSlotOrMissileIndex, out weaponDamage);
                float baseDam = base.CalculateStrikeMagnitudeForMissile(attackInformation, collisionData, weapon, missileSpeed);
                float mtd = collisionData.MissileTotalDamage;
                if (baseDam == 0) baseDam = 1;
                if (mtd == 0) mtd = 1;
                return baseDam / mtd * (weaponDamage + collisionData.MissileTotalDamage);
            }
            else if (SkillSystemBehavior.WoW_WeaponMissile.ContainsKey(collisionData.AffectorWeaponSlotOrMissileIndex) && weapon.CurrentUsageItem.IsConsumable && weapon.CurrentUsageItem.IsRangedWeapon)//对于自己生成的投掷类武器因为mtd值正确，所以不用weaponDamage+mtd。为了兼容非远程武器生成的投矛，所以用这个公式
            {
                int weaponDamage;
                SkillSystemBehavior.WoW_WeaponMissile.TryGetValue(collisionData.AffectorWeaponSlotOrMissileIndex, out weaponDamage);
                float baseDam = base.CalculateStrikeMagnitudeForMissile(attackInformation, collisionData, weapon, missileSpeed);
                float mtd = collisionData.MissileTotalDamage;
                if (baseDam == 0) baseDam = 1;
                if (mtd == 0) mtd = 1;
                if (weaponDamage == 0) weaponDamage = (int)mtd;
                return baseDam / mtd * (weaponDamage);

            }

            return base.CalculateStrikeMagnitudeForMissile(attackInformation, collisionData, weapon, missileSpeed);//如果字典里没有，说明是游戏源代码生成 的，所以直接输出原来acd里的mtd 

        }
        public override float CalculateStrikeMagnitudeForSwing(in AttackInformation attackInformation, in AttackCollisionData collisionData, in MissionWeapon weapon, float swingSpeed, float impactPointAsPercent, float extraLinearSpeed)
        {
            //移出武器打击位置对伤害的影响
            MissionWeapon missionWeapon = weapon;
            WeaponComponentData currentUsageItem = missionWeapon.CurrentUsageItem;
            missionWeapon = weapon;
            //将输入进来的impactPointAsPercent武器打击点,直接替换成currentUsageItem.SweetSpotReach物品属性里的最佳打击位置,这样就算是用护手敲人也等于用剑刃打人
            return CombatStatCalculator.CalculateStrikeMagnitudeForSwing(swingSpeed, currentUsageItem.SweetSpotReach, missionWeapon.Item.Weight, currentUsageItem.GetRealWeaponLength(), currentUsageItem.Inertia, currentUsageItem.CenterOfMass, extraLinearSpeed);
        }
        public override float CalculateStrikeMagnitudeForThrust(in AttackInformation attackInformation, in AttackCollisionData collisionData, in MissionWeapon weapon, float thrustWeaponSpeed, float extraLinearSpeed, bool isThrown = false)
        {
            MissionWeapon missionWeapon = weapon;
            float num = CombatStatCalculator.CalculateStrikeMagnitudeForThrust(thrustWeaponSpeed, missionWeapon.Item.Weight, extraLinearSpeed, isThrown);

            num = TaleWorlds.Library.MathF.Max(num, (float)weapon.GetModifiedThrustDamageForCurrentUsage());
            return num;
        }

    }

    public class WOW_CustomBattleAgentStatCalculateModel : CustomBattleAgentStatCalculateModel
    {
        public override float GetWeaponDamageMultiplier(Agent agent, WeaponComponentData weapon)
        {
            float native = base.GetWeaponDamageMultiplier(agent, weapon);
            native = StrikeMagnitudeScript.WOW_Script_AgentStatCalculateModel(agent, native);
            return native;
            //接下来的代码会乘等这个函数的输出值，所以这个函数的数值1==100%
            //float weaponDamageMultiplier = MissionGameModels.Current.AgentStatCalculateModel.GetWeaponDamageMultiplier(attackInformation.AttackerAgent, currentUsageItem2);
            //baseMagnitude *= weaponDamageMultiplier;
        }
    }
    public class WOW_SandboxAgentStatCalculateModel : SandboxAgentStatCalculateModel
    {
        public override float GetWeaponDamageMultiplier(Agent agent, WeaponComponentData weapon)
        {
            float native = base.GetWeaponDamageMultiplier(agent, weapon);
            native = StrikeMagnitudeScript.WOW_Script_AgentStatCalculateModel(agent, native);
            return native;
            //接下来的代码会乘等这个函数的输出值，所以这个函数的数值1==100%
            //float weaponDamageMultiplier = MissionGameModels.Current.AgentStatCalculateModel.GetWeaponDamageMultiplier(attackInformation.AttackerAgent, currentUsageItem2);
            //baseMagnitude *= weaponDamageMultiplier;
        }
    }
    public class WOW_SandboxAgentApplyDamageModel : SandboxAgentApplyDamageModel
    {
        //public static float CalculateStrikeMagnitudeForSwing(float swingSpeed, float impactPointAsPercent, float weaponWeight, float weaponLength, float weaponInertia, float weaponCoM, float extraLinearSpeed)
        //{
        //    // 计算击中点距离武器重心的距离
        //    float impactPointDistance = weaponLength * impactPointAsPercent - weaponCoM;

        //    // 计算武器在击中瞬间的初始线速度
        //    float initialVelocity = swingSpeed * (0.5f + weaponCoM) + extraLinearSpeed;

        //    // 计算击中之前的动能，包括线动能和角动能
        //    float kineticEnergyBeforeImpact = 0.5f * weaponWeight * initialVelocity * initialVelocity +
        //                                       0.5f * weaponInertia * swingSpeed * swingSpeed;

        //    // 计算击中后的最终线速度
        //    float finalLinearVelocity = initialVelocity - (initialVelocity + swingSpeed * impactPointDistance) / (1f / weaponWeight + impactPointDistance * impactPointDistance / weaponInertia) / weaponWeight;

        //    // 计算击中后的最终角速度
        //    float finalAngularVelocity = swingSpeed - (initialVelocity + swingSpeed * impactPointDistance) * impactPointDistance / weaponInertia;

        //    // 计算击中后的动能，包括线动能和角动能
        //    float kineticEnergyAfterImpact = 0.5f * weaponWeight * finalLinearVelocity * finalLinearVelocity +
        //                                     0.5f * weaponInertia * finalAngularVelocity * finalAngularVelocity;

        //    // 返回冲击力的大小，基于动能的改变量，并乘以一个系数0.067，再加上一个常数0.5的一半
        //    return 0.067f * (kineticEnergyBeforeImpact - kineticEnergyAfterImpact + 0.5f);
        //}
        public override bool DecideCrushedThrough(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy, Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsage)
        {
            Random random = new Random();
            /*获取攻击方武器数据*/
            EquipmentIndex attackerAgentwieldedOffHandItemIndex = attackerAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand);
            EquipmentIndex attackerAgentwieldedMainHandItemIndex = attackerAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
            WeaponComponentData attackerAgentweaponComponentData = ((attackerAgentwieldedMainHandItemIndex != EquipmentIndex.None) ? attackerAgent.Equipment[attackerAgentwieldedMainHandItemIndex].CurrentUsageItem : null);
            if (attackerAgentweaponComponentData == null)
            {
                return false;//如果没有攻击方武器，直接无法突破格挡
            }
            //获取防御方武器数据
            EquipmentIndex defenderAgentwieldedItemIndex = attackerAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
            WeaponComponentData defenderAgentweaponComponentData = ((defenderAgentwieldedItemIndex != EquipmentIndex.None) ? attackerAgent.Equipment[defenderAgentwieldedItemIndex].CurrentUsageItem : null);
            if (defenderAgentweaponComponentData == null)
            {
                return true;//如果没有武器，默认突破格挡
            }
            //一些额外的直接突破格挡的设定
            if (SkillSystemBehavior.ActiveComponents != null && SkillSystemBehavior.ActiveComponents.TryGetValue(attackerAgent.Index, out var WoW_agent))
            {
                if (WoW_agent.HasSkill("Power"))
                {
                    return true;
                }
            }
            if (defendItem != null && !defendItem.IsShield && strikeType == StrikeType.Thrust)//武器防御突刺时
            {
                return true;//一定被突破格挡
            }

            //获取攻守双方的武器熟练度（对应武器的技能等级）
            int attacckAgentWeaponProficiency = attackerAgent.Character.GetSkillValue(attackerAgentweaponComponentData.RelevantSkill);
            int defenderAgentWeaponProficiency = defenderAgent.Character.GetSkillValue(defenderAgentweaponComponentData.RelevantSkill);
            int ProficiencyCrush = attacckAgentWeaponProficiency - defenderAgentWeaponProficiency;//熟练度差值，可以为负数，负数表示攻击方熟练度低于防御方
                                                                                                  //获取双方跑动或骑术的技能等级，根据是否有马来判断
            int attacckAgentDefSkillValue = 0;
            int defenderAgentDefSkillValue = 0;
            if (defenderAgent.Mount != null)
                defenderAgentDefSkillValue = defenderAgent.Character.GetSkillValue(DefaultSkills.Riding);
            else
                defenderAgentDefSkillValue = defenderAgent.Character.GetSkillValue(DefaultSkills.Athletics);
            if (attackerAgent.Mount != null)
                attacckAgentDefSkillValue = attackerAgent.Character.GetSkillValue(DefaultSkills.Riding);
            else
                attacckAgentDefSkillValue = attackerAgent.Character.GetSkillValue(DefaultSkills.Athletics);

            //突破格挡需要超过的能量数
            float num = 58f;

            //攻击方使用双手武器或者双手使用长杆，更容易突破格挡
            if (attackerAgentweaponComponentData.RelevantSkill == DefaultSkills.TwoHanded | (attackerAgentwieldedOffHandItemIndex == null && attackerAgentweaponComponentData.RelevantSkill == DefaultSkills.Polearm))
            {
                totalAttackEnergy *= 1.2f;//只要用双手武器，就强化20%输入能量
                if (ProficiencyCrush > 0)
                    totalAttackEnergy = totalAttackEnergy * (1 + ProficiencyCrush / 500f);//如果熟练度高于对方的武器熟练度，则额外加乘，每多100点熟练度，强化20%
            }
            //双方都处于步战时，如果防御方使用武器防御，则会根据步战技能差值，调整破防难度
            if (defendItem != null && !defendItem.IsShield && defenderAgent.Mount == null && attackerAgent.Mount == null)
            {
                num -= ((attacckAgentDefSkillValue - defenderAgentDefSkillValue) * 0.05f);
            }
            //处于任意状态，根据双方武器熟练差值，调整破防难度
            num -= (ProficiencyCrush * 0.05f);
            if (isPassiveUsage)//如果是骑枪冲刺或者长矛反骑
            {
                num /= 2;//更容易突破格挡
            }
            if (defendItem != null && defendItem.IsShield)//如果防御方使用盾牌防御，更难突破格挡
            {
                num *= 1.2f;
            }

            //如果使用斧类武器攻击，根据双方武器和步战熟练度差值，有概率缴械对方的防御物品
            //需要额外增加设定,基础概率增加一点,然后可以根据对方格挡的方向,额外降低一下缴械概率
            //注意一下缴械的时候检测一下是不是空手了
            if (attackerAgentweaponComponentData.WeaponClass == WeaponClass.OneHandedAxe || attackerAgentweaponComponentData.WeaponClass == WeaponClass.TwoHandedAxe)
            {

                //缴械概率=熟练度差值/500*（1+步战差值/1000）.300熟练打100熟练，破防时缴械概率为0.2+0.4*1.2=0.68
                float disarm = 0.2f + ProficiencyCrush / 500 * (1 + (attacckAgentDefSkillValue - defenderAgentDefSkillValue) / 1000);
                float r = random.NextFloat();
                if (disarm > r)
                {
                    EquipmentIndex wieldedItemIndex = defenderAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand);
                    if (wieldedItemIndex == EquipmentIndex.None)
                    {
                        wieldedItemIndex = defenderAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
                    }
                    if (wieldedItemIndex == EquipmentIndex.None)
                        defenderAgent.RemoveEquippedWeapon(wieldedItemIndex);
                }


            }
            return totalAttackEnergy > num;
        }

        public override float CalculateDamage(in AttackInformation attackInformation, in AttackCollisionData collisionData, in MissionWeapon weapon, float baseDamage)
        {
            //做一下护甲的固定数值减伤，最终的伤害，减少护甲15%防御值的伤害。60甲-12的最终伤害，避免重甲被劫匪石头砸死
            int def = (int)(attackInformation.ArmorAmountFloat * 0.1f);
            return base.CalculateDamage(attackInformation, collisionData, weapon, baseDamage) - def;
        }
    }
    public class WOW_CustomAgentApplyDamageModel : CustomAgentApplyDamageModel
    {
        public override bool DecideCrushedThrough(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy, Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsage)
        {
            Random random = new Random();
            /*获取攻击方武器数据*/
            EquipmentIndex attackerAgentwieldedOffHandItemIndex = attackerAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand);
            EquipmentIndex attackerAgentwieldedMainHandItemIndex = attackerAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
            WeaponComponentData attackerAgentweaponComponentData = ((attackerAgentwieldedMainHandItemIndex != EquipmentIndex.None) ? attackerAgent.Equipment[attackerAgentwieldedMainHandItemIndex].CurrentUsageItem : null);
            if (attackerAgentweaponComponentData == null)
            {
                return false;//如果没有攻击方武器，直接无法突破格挡
            }
            //获取防御方武器数据
            EquipmentIndex defenderAgentwieldedItemIndex = attackerAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
            WeaponComponentData defenderAgentweaponComponentData = ((defenderAgentwieldedItemIndex != EquipmentIndex.None) ? attackerAgent.Equipment[defenderAgentwieldedItemIndex].CurrentUsageItem : null);
            if (defenderAgentweaponComponentData == null)
            {
                return true;//如果没有武器，默认突破格挡
            }
            //一些额外的直接突破格挡的设定
            if (SkillSystemBehavior.ActiveComponents != null && SkillSystemBehavior.ActiveComponents.TryGetValue(attackerAgent.Index, out var WoW_agent))
            {
                if (WoW_agent.HasSkill("Power"))
                {
                    return true;
                }
            }
            if (defendItem != null && !defendItem.IsShield && strikeType == StrikeType.Thrust)//武器防御突刺时
            {
                return true;//一定被突破格挡
            }

            //获取攻守双方的武器熟练度（对应武器的技能等级）
            int attacckAgentWeaponProficiency = attackerAgent.Character.GetSkillValue(attackerAgentweaponComponentData.RelevantSkill);
            int defenderAgentWeaponProficiency = defenderAgent.Character.GetSkillValue(defenderAgentweaponComponentData.RelevantSkill);
            int ProficiencyCrush = attacckAgentWeaponProficiency - defenderAgentWeaponProficiency;//熟练度差值，可以为负数，负数表示攻击方熟练度低于防御方
                                                                                                  //获取双方跑动或骑术的技能等级，根据是否有马来判断
            int attacckAgentDefSkillValue = 0;
            int defenderAgentDefSkillValue = 0;
            if (defenderAgent.Mount != null)
                defenderAgentDefSkillValue = defenderAgent.Character.GetSkillValue(DefaultSkills.Riding);
            else
                defenderAgentDefSkillValue = defenderAgent.Character.GetSkillValue(DefaultSkills.Athletics);
            if (attackerAgent.Mount != null)
                attacckAgentDefSkillValue = attackerAgent.Character.GetSkillValue(DefaultSkills.Riding);
            else
                attacckAgentDefSkillValue = attackerAgent.Character.GetSkillValue(DefaultSkills.Athletics);

            //突破格挡需要超过的能量数
            float num = 58f;

            //攻击方使用双手武器或者双手使用长杆，更容易突破格挡
            if (attackerAgentweaponComponentData.RelevantSkill == DefaultSkills.TwoHanded | (attackerAgentwieldedOffHandItemIndex == null && attackerAgentweaponComponentData.RelevantSkill == DefaultSkills.Polearm))
            {
                totalAttackEnergy *= 1.2f;//只要用双手武器，就强化20%输入能量
                if (ProficiencyCrush > 0)
                    totalAttackEnergy = totalAttackEnergy * (1 + ProficiencyCrush / 500f);//如果熟练度高于对方的武器熟练度，则额外加乘，每多100点熟练度，强化20%
            }
            //双方都处于步战时，如果防御方使用武器防御，则会根据步战技能差值，调整破防难度
            if (defendItem != null && !defendItem.IsShield && defenderAgent.Mount == null && attackerAgent.Mount == null)
            {
                num -= ((attacckAgentDefSkillValue - defenderAgentDefSkillValue) * 0.05f);
            }
            //处于任意状态，根据双方武器熟练差值，调整破防难度
            num -= (ProficiencyCrush * 0.05f);
            if (isPassiveUsage)//如果是骑枪冲刺或者长矛反骑
            {
                num /= 2;//更容易突破格挡
            }
            if (defendItem != null && defendItem.IsShield)//如果防御方使用盾牌防御，更难突破格挡
            {
                num *= 1.2f;
            }

            //如果使用斧类武器攻击，根据双方武器和步战熟练度差值，有概率缴械对方的防御物品
            //需要额外增加设定,基础概率增加一点,然后可以根据对方格挡的方向,额外降低一下缴械概率
            //注意一下缴械的时候检测一下是不是空手了
            if (attackerAgentweaponComponentData.WeaponClass == WeaponClass.OneHandedAxe || attackerAgentweaponComponentData.WeaponClass == WeaponClass.TwoHandedAxe)
            {

                //缴械概率=熟练度差值/500*（1+步战差值/1000）.300熟练打100熟练，破防时缴械概率为0.2+0.4*1.2=0.68
                float disarm = 0.2f + ProficiencyCrush / 500 * (1 + (attacckAgentDefSkillValue - defenderAgentDefSkillValue) / 1000);
                float r = random.NextFloat();
                if (disarm > r)
                {
                    EquipmentIndex wieldedItemIndex = defenderAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand);
                    if (wieldedItemIndex == EquipmentIndex.None)
                    {
                        wieldedItemIndex = defenderAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
                    }
                    if (wieldedItemIndex == EquipmentIndex.None)
                        defenderAgent.RemoveEquippedWeapon(wieldedItemIndex);
                }


            }
            return totalAttackEnergy > num;
        }
        public override float CalculateDamage(in AttackInformation attackInformation, in AttackCollisionData collisionData, in MissionWeapon weapon, float baseDamage)
        {
            //做一下护甲的固定数值减伤，最终的伤害，减少护甲15%防御值的伤害。60甲-12的最终伤害，避免重甲被劫匪石头砸死
            int def = (int)(attackInformation.ArmorAmountFloat * 0.1f);
            return base.CalculateDamage(attackInformation, collisionData, weapon, baseDamage) - def;
        }

    }



}

