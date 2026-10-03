using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace New_ZZZF.Harmonys
{
    /// <summary>
    /// 原生 ControlTick 会在 IPlayerInputEffector 之后重新写入 MovementInputVector。
    /// 后置补丁确保冲刺的强制移动轴是本帧最终提交给 Agent 的移动输入。
    /// </summary>
    [HarmonyPatch(typeof(MissionMainAgentController), "ControlTick")]
    internal static class RushMovementInputPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            Mission mission = Mission.Current;
            RushMovementMissionLogic manager = RushMovementMissionLogic.Current;
            if (mission?.MainAgent != null && manager != null)
                manager.ApplyPlayerRushMovementAfterControlTick(mission.MainAgent);
            Agent player = mission?.MainAgent;
            if (player?.GetComponent<AgentSkillComponent>()?.StateContainer.GetLongestStateDuration("DaDiJianTaBuffToEnemy") > 0f)
            {
                player.MovementInputVector = TaleWorlds.Library.Vec2.Zero;
                player.MovementFlags = 0;
                if (player.MountAgent != null) {
                    player.MountAgent.MovementInputVector = TaleWorlds.Library.Vec2.Zero;
                    player.MountAgent.MovementFlags = 0;
                }
            }
        }
    }
}
