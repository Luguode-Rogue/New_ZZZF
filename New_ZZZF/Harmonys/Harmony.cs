using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Harmonys
{


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
