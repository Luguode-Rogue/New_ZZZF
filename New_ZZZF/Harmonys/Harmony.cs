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
namespace New_ZZZF.Harmonys
{

    [HarmonyPatch(typeof(ItemPreviewVM), "Open")]
    public class ItemPreviewVMPatch
    {
        // 原始方法的前缀补丁
        [HarmonyPrefix]
        
        public static bool Open_Prefix(ItemPreviewVM __instance, EquipmentElement item)
        {
            string s = "";
            // 自定义逻辑：在调用原始方法之前执行


            __instance.ItemTableau.FillFrom(item, BannerCode.CreateFrom(Clan.PlayerClan.Banner).Code);
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

    // 修补UpdateMomentumRemaining方法
    [HarmonyPatch(typeof(Mission), "UpdateMomentumRemaining")]
    public class UpdateMomentumRemaining_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(
            ref float momentumRemaining,
            Blow b,
            in AttackCollisionData collisionData,
            Agent attacker,
            Agent victim,
            in MissionWeapon attackerWeapon,
            bool isCrushThrough)
        {
            // 在这里修改momentumRemaining值
            momentumRemaining = 1f;
        }
    }

    // 修补DecideWeaponCollisionReaction方法
    [HarmonyPatch(typeof(Mission), "DecideWeaponCollisionReaction")]
    public class DecideWeaponCollisionReaction_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(
            Blow registeredBlow,
            in AttackCollisionData collisionData,
            Agent attacker,
            Agent defender,
            in MissionWeapon attackerWeapon,
            bool isFatalHit,
            bool isShruggedOff,
            ref MeleeCollisionReaction colReaction)
        {
            // 修改colReaction的值
            colReaction = MeleeCollisionReaction.SlicedThrough;
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
                        __instance.SetActionChannel(0,ActionIndexCache.Create("act_mount_horse_from_left"),false, 272UL, 0, 1, -0.2f, 0.4f, 0.25f);
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
