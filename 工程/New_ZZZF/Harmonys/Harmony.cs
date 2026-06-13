using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TaleWorlds.Library;
using System;
using TaleWorlds.Engine;
using MathF = TaleWorlds.Library.MathF;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using System.Reflection;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem;
using New_ZZZF.Skills;
using SandBox.GameComponents;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
namespace New_ZZZF.Harmonys
{
    [HarmonyPatch(typeof(RangedSiegeWeapon), "GetBallisticErrorAppliedDirection")]
    static class GetBallisticErrorAppliedDirection_Patch
    {
        static bool Prefix(ref Vec3 __result, RangedSiegeWeapon __instance,ref float BallisticErrorAmount)
        {            
            // 提前获取_lastShooterAgent（关键修改点）
            Agent lastShooterAgent = Traverse.Create(__instance)
                .Field<Agent>("_lastShooterAgent").Value;
            int duiwugongcheng = 0;//队伍的工程师的攻城等级
            // 确认agent和其起源信息有效
            if (lastShooterAgent?.Origin?.BattleCombatant is PartyBase partyBase && partyBase.MapEvent is MapEvent mapEvent)
            {
                // 获取移动部队
                MobileParty mobileParty = partyBase.IsMobile ? partyBase.MobileParty : null;

                // 检查是否满足围攻战且为攻击方的条件
                if (mobileParty != null && mapEvent.AttackerSide == partyBase.MapEventSide && mapEvent.EventType == MapEvent.BattleTypes.Siege)
                {

                    if (mobileParty.EffectiveEngineer != null)
                    {
                        duiwugongcheng= mobileParty.EffectiveEngineer.GetSkillValue(DefaultSkills.Engineering);
                    }
                }
            }

            // 获取工程技能值
            int engineeringSkill = lastShooterAgent.Character.GetSkillValue(DefaultSkills.Engineering);
            engineeringSkill += duiwugongcheng /2;
            engineeringSkill = 500;
            BallisticErrorAmount = GetBaseErrorModified(__instance, engineeringSkill);
            return true; 
        }
        private static float GetBaseErrorModified(RangedSiegeWeapon weapon, int skill)
        {
            return weapon switch
            {
                FireBallista _ => CalculateModifiedError(0.5f,skill),
                Ballista _ => CalculateModifiedError(0.5f, skill),
                FireMangonel _ => CalculateModifiedError(1.5f, skill),
                Mangonel _ => CalculateModifiedError(2.5f, skill),
                Trebuchet _ => CalculateModifiedError(1.0f, skill),
                _ => CalculateModifiedError(1.0f, skill) // 默认值
            };
        }
        private static float CalculateModifiedError(float baseError, int skill)
        {
            float retFloat =1;
            if (skill <= 100)
            {
                retFloat= baseError - (skill - 100) * 0.005f;
            }
            else if (skill <=200) 
            {
                if (baseError == 0.5f)//攻城弩
                {
                    retFloat = MathF.Clamp(baseError - (skill - 100) * 0.005f, 0, 3);//

                }
                else if (baseError == 1.5f)//火焰投石车
                {

                    retFloat = MathF.Clamp(baseError - (skill - 100) * 0.001f, 0, 3);
                }
                else if (baseError == 2.5f)//散弹投石车
                {

                    retFloat = MathF.Clamp(baseError - (skill - 100) * 0.015f, 1, 3);
                }
                else if (baseError == 1f)//配重投石车和其他
                {

                    retFloat = MathF.Clamp(baseError - (skill - 100) * 0.005f, 0, 3);
                }
            }
            else if (skill <=250)
            {
                if (baseError == 0.5f)//攻城弩
                {
                    retFloat = MathF.Clamp(baseError - (skill - 100) * 0.005f, 0, 3);//

                }
                else if (baseError == 1.5f)//火焰投石车
                {

                    retFloat = MathF.Clamp(baseError - (skill - 100) * 0.001f, 0, 3);
                }
                else if (baseError == 2.5f)//散弹投石车
                {

                    retFloat = MathF.Clamp(baseError - (skill - 100) * 0.015f, 1, 3);
                }
                else if (baseError == 1f)//配重投石车和其他
                {

                    retFloat = MathF.Clamp(baseError - (skill - 100) * 0.005f, 0, 3);
                }
            }
            else if (skill <=300) 
            {
                if (baseError == 2.5)
                {
                    retFloat = 0.5f;
                }
                else
                {
                    retFloat = 0;
                }
            }
            return retFloat;
        }
    }

    
    [HarmonyPatch(typeof(WeaponComponentData), nameof(WeaponComponentData.GetRelevantSkillFromWeaponClass))]
    public class WeaponSkillPatch
    {
        static bool Prefix(WeaponClass weaponClass, ref SkillObject __result)
        {
            switch (weaponClass)
            {
                // 剑精通体系
                case WeaponClass.OneHandedSword:  // 单手剑
                case WeaponClass.TwoHandedSword:  // 双手剑
                case WeaponClass.ThrowingKnife:   // 投掷剑（原飞刀）
                    __result = DefaultSkills.OneHanded; //预设技能库.剑精通;
                    return false; // 拦截原方法

                // 斧精通体系
                case WeaponClass.OneHandedAxe:    // 单手斧
                case WeaponClass.TwoHandedAxe:    // 双手斧
                case WeaponClass.ThrowingAxe:    // 投掷斧
                    __result = DefaultSkills.TwoHanded; //预设技能库.斧精通;
                    return false;

                // 锤精通体系
                case WeaponClass.Mace:            // 单手锤
                case WeaponClass.TwoHandedMace:   // 双手锤
                case WeaponClass.Dagger:          // 投掷锤（原匕首）
                    __result = DefaultSkills.Polearm; //预设技能库.锤精通;
                    return false;

                // 矛精通体系
                case WeaponClass.Javelin:         // 投掷矛
                case WeaponClass.OneHandedPolearm:      // 单手短矛
                case WeaponClass.TwoHandedPolearm:       // 双手长矛
                case WeaponClass.LowGripPolearm:       // 低握长柄
                    __result = DefaultSkills.Throwing; //预设技能库.矛精通;
                    return false;

                case WeaponClass.Pistol:         // 手铳
                case WeaponClass.Musket:         // 双手手铳
                    __result = DefaultSkills.Crossbow;
                    return false;
                // 其他未修改的武器类型继续走原逻辑
                default:
                    return true;
            }
        }
    }

    [HarmonyPatch(typeof(ItemPreviewVM), "Open")]
    public class ItemPreviewVMPatch
    {
        // 原始方法的前缀补丁
        [HarmonyPrefix]
        
        public static bool Open_Prefix(ItemPreviewVM __instance, EquipmentElement item)
        {
            string s = "";
            // 自定义逻辑：在调用原始方法之前执行


            __instance.ItemTableau.FillFrom(item, null /* BannerCode removed in new API */);
            __instance.ItemName = item.Item.Name.ToString();
            __instance.IsSelected = true;

            if (SkillFactory._skillRegistry.TryGetValue(item.Item.StringId, out var skillBase))
            {
                if (skillBase.Description != null)
                {
                    s = item.Item.StringId;
                    __instance.ItemName = skillBase.Description.ToString();
                }
            }

            // 如果返回true，则继续执行原始方法；如果返回false，则跳过原始方法
            return false;
        }

    }

    // 修补UpdateMomentumRemaining方法 (新版已移至 MissionCombatMechanicsHelper 静态类)
    [HarmonyPatch(typeof(MissionCombatMechanicsHelper), "UpdateMomentumRemaining")]
    public class UpdateMomentumRemaining_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(
            ref float momentumRemaining,
            in Blow b,
            in AttackCollisionData collisionData,
            Agent attacker,
            Agent victim,
            in MissionWeapon attackerWeapon,
            bool isCrushThrough)
        {
            // 在这里修改momentumRemaining值
            ////等待加限制momentumRemaining = 1f;
        }
    }

    // 修补DecideWeaponCollisionReaction方法 (新版已移至 MissionCombatMechanicsHelper 静态类)
    [HarmonyPatch(typeof(MissionCombatMechanicsHelper), "DecideWeaponCollisionReaction")]
    public class DecideWeaponCollisionReaction_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(
            in Blow registeredBlow,
            in AttackCollisionData collisionData,
            Agent attacker,
            Agent defender,
            in MissionWeapon attackerWeapon,
            bool isFatalHit,
            bool isShruggedOff,
            float momentumRemaining,
            out MeleeCollisionReaction colReaction)
        {
            // 修改colReaction的值
            colReaction = MeleeCollisionReaction.ContinueChecking;
            ////等待加限制 colReaction = MeleeCollisionReaction.SlicedThrough;
        }
    }
    [HarmonyPatch(typeof(Agent), "Mount")]
    public class AgentPatches
    {
        [HarmonyPrefix]
        //__instance等价于this,后面的参数是修改的方法本来传入的参数
        //在这里，__instance是骑手agent，mountAgent是坐骑agent
        public static bool Prefix_Mount(Agent __instance, Agent mountAgent)
        {

            bool flag = mountAgent.GetCurrentActionType(0) == Agent.ActionCodeType.Rear;
            SkillSystemBehavior.ActiveComponents.TryGetValue(__instance.Index, out var v);
            if (__instance.MountAgent == null && mountAgent.RiderAgent == null)
            {
                if (__instance.CheckSkillForMounting(mountAgent) && (!flag || v._globalCooldownTimer <= 0f))// && __instance.GetCurrentActionValue(0) == ActionIndexValueCache.act_none)
                {
                    __instance.EventControlFlags |= Agent.EventControlFlag.Mount;
                    __instance.SetInteractionAgent(mountAgent);


                    if (v._globalCooldownTimer <= 0f)
                    {
                        var traverse = Traverse.Create(__instance);
                        traverse.Property("MountAgent").SetValue(mountAgent);
                        __instance.SetActionChannel(0,ActionIndexCache.Create("act_mount_horse_from_left"),false, (AnimFlags)272UL, 0, 1, -0.2f, 0.4f, 0.25f);
                        //v._globalCooldownTimer += 5;
                    }



                }
            }
            else if (__instance.MountAgent == mountAgent && !flag || v._globalCooldownTimer <= 0f)
            {
                __instance.EventControlFlags |= Agent.EventControlFlag.Dismount;

                if (v._globalCooldownTimer <= 0f)
                {
                    var traverse = Traverse.Create(__instance);
                    traverse.Property("MountAgent").SetValue(null);
                    __instance.SetActionChannel(0, ActionIndexCache.Create("act_horse_fall_roll"));
                    __instance.SetCurrentActionProgress(0, 0.3f);
                    __instance.SetCurrentActionSpeed(0, 2f);
                    //v._globalCooldownTimer +=5;
                }
            }
            return false;
            // 在这里添加你想要在Mount方法执行前执行的代码  
            // 例如，你可以记录日志，或者添加一些额外的检查  



            // 在这里添加你想要在Mount方法执行后执行的代码  
            // 例如，你可以根据originalResult的值执行一些操作  

            // 返回true表示继续执行原始方法，如果需要的话  
            return false;
        }
    }
}
