using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Core;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// TacticalMap 运行时入口。
    /// 负责 TacticalMap Core 与战场 MissionBehavior 的运行时初始化；HTMLUI Consumer 由 SubModule 初始化。
    /// </summary>
    public static class TacticalMapBootstrap
    {
        private static Harmony _harmony;

        public static void OnSubModuleLoad()
        {
            if (!FeatureGate.Enabled)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[TMap] 引导跳过：FeatureGate(EnableMinimap) 关闭"));
                return;
            }

            _harmony = new Harmony("TacticalMap");
            TacticalCameraPatch.Patch(_harmony);

            InformationManager.DisplayMessage(new InformationMessage(
                "[TMap] 引导完成：TacticalMap Core + HTMLUI Consumer 已注册"));
        }

        public static void OnMissionStart(Mission mission)
        {
            if (!FeatureGate.Enabled)
                return;

            if (!MissionSceneGuard.IsTacticalMapSupported(mission))
                return;

            mission.AddMissionBehavior(new TacticalMapMissionLogic());
        }
    }
}
