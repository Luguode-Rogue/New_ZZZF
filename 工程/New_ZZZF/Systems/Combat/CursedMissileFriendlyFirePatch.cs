using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    // 原版远程碰撞在进入伤害模型前就取消AI友伤。
    // 仅在此回调内替换友军保护查询，不修改队伍、控制器或全局友伤配置。
    [HarmonyPatch(typeof(Mission), "MissileHitCallback")]
    internal static class CursedMissileFriendlyFirePatch
    {
        private static bool IsProtectedFriend(Agent attacker, Agent victim) =>
            attacker.IsFriendOf(victim) && !XieEZuZhou.IsCursed(attacker);

        private static bool IsDamageableEnemy(Agent attacker, Agent victim) =>
            attacker.IsEnemyOf(victim) || (attacker != victim && attacker.IsFriendOf(victim) && XieEZuZhou.IsCursed(attacker));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.Method(typeof(Agent), nameof(Agent.IsFriendOf), new[] { typeof(Agent) });
            MethodInfo replacement = AccessTools.Method(typeof(CursedMissileFriendlyFirePatch), nameof(IsProtectedFriend));
            MethodInfo enemy = AccessTools.Method(typeof(Agent), nameof(Agent.IsEnemyOf), new[] { typeof(Agent) });
            MethodInfo enemyReplacement = AccessTools.Method(typeof(CursedMissileFriendlyFirePatch), nameof(IsDamageableEnemy));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(original)) {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }
                if (instruction.Calls(enemy)) {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = enemyReplacement;
                }
                yield return instruction;
            }
        }
    }
}