using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;

namespace New_ZZZF.Harmonys
{
    // ================================================================
    // 临时补丁：修复 TaleWorlds.MountAndBlade.TaskForceDetachment
    //           .CalculateShouldBeDisbanded() 的 NullReferenceException
    //
    // 根因：原版代码中 _attackedAgent 在 RemoveAgent 时可能被置为 null
    //       （当 detachment 的其它成员都已离队/死亡时），之后
    //       CalculateShouldBeDisbanded 访问 _attackedAgent.Position 即崩溃。
    //
    // 此补丁仅做防御性判空：当 _attackedAgent 为 null 时直接判定该
    // detachment 应当解散，跳过原方法，避免异常。
    //
    // 官方修复后可直接删除本文件（整个文件，无需改动其它代码）。
    // ================================================================
    [HarmonyPatch(typeof(TaskForceDetachment), "CalculateShouldBeDisbanded")]
    static class TaskForceDetachment_CalculateShouldBeDisbanded_NREFix
    {
        static bool Prefix(TaskForceDetachment __instance, ref bool __result)
        {
            // _attackedAgent 是私有字段，用 Traverse 读取
            Agent attackedAgent = Traverse.Create(__instance)
                .Field<Agent>("_attackedAgent").Value;

            if (attackedAgent == null || !attackedAgent.IsActive())
            {
                // 攻击目标已不存在，安全解散 detachment，跳过原方法
                __result = true;
                return false;
            }

            // 其它情况走原版逻辑
            return true;
        }
    }
}
