using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Core;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// TacticalMap runtime bootstrap.
    /// Legacy HtmlUI integration has been removed; the new UI will be rebuilt from a clean baseline.
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
                "[TMap] 引导完成：旧 HtmlUI 已移除，等待新 UI 集成"));
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
