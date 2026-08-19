using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Core;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// TacticalMap 运行时入口。
    /// 当前仅负责 TacticalMap Core 的运行时初始化；HTMLUI 已清除，后续重新设计后再接入。
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
                "[TMap] 引导完成：TacticalMap Core 已初始化，HTMLUI 当前未接入"));
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
