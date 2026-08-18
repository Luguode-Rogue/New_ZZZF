using HarmonyLib;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.UI;
using TaleWorlds.Library;
using BannerlordHtmlUI;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// 战术地图功能总入口。HTMLUI 负责运行时界面，Core 负责地图数据与战场逻辑。
    /// </summary>
    public static class TacticalMapBootstrap
    {
        private static Harmony _harmony;
        private static TacticalMapHtmlUi _htmlUi;

        public static TacticalMapHtmlUi HtmlUi => _htmlUi;

        public static void OnSubModuleLoad()
        {
            TacticalMapHtmlUiDebug.Init();
            TacticalMapHtmlUiDebug.Log("BOOT", "TacticalMapBootstrap.OnSubModuleLoad enter");

            if (!FeatureGate.Enabled)
            {
                TacticalMapHtmlUiDebug.Log("BOOT", "FeatureGate disabled; bootstrap aborted");
                InformationManager.DisplayMessage(new InformationMessage("[TMap] 引导跳过：FeatureGate(EnableMinimap) 关闭"));
                return;
            }

            try
            {
                _harmony = new Harmony("TacticalMap");
                TacticalMapHtmlUiDebug.Log("BOOT", "Harmony created");

                TacticalCameraPatch.Patch(_harmony);
                TacticalMapHtmlUiBridgePatch.Patch(_harmony);
                TacticalMapHtmlUiInputPatch.Patch(_harmony);
                TacticalMapHtmlUiDebugPatch.Patch(_harmony);
                TacticalMapHtmlUiDebug.Log("BOOT", "Harmony patches installed");

                _htmlUi = new TacticalMapHtmlUi();
                TacticalMapHtmlUiDebug.Log("BOOT", "TacticalMapHtmlUi instance created");

                _htmlUi.InitializeOnFrameworkReady();
                TacticalMapHtmlUiDebug.Log("BOOT", "HtmlUi registration callback requested");

                InformationManager.DisplayMessage(new InformationMessage("[TMap] 引导完成：TacticalMap HTMLUI 就绪"));
            }
            catch (System.Exception ex)
            {
                TacticalMapHtmlUiDebug.Log("BOOT_ERROR", ex.ToString());
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] Bootstrap 异常: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public static void OnMissionStart(Mission mission)
        {
            TacticalMapHtmlUiDebug.Init();
            TacticalMapHtmlUiDebug.Log("MISSION_START", $"mission={(mission == null ? "null" : mission.GetType().FullName)} scene={(mission?.Scene == null ? "null" : "ok")}");

            if (!FeatureGate.Enabled)
            {
                TacticalMapHtmlUiDebug.Log("MISSION_START", "FeatureGate disabled");
                InformationManager.DisplayMessage(new InformationMessage("[TMap] 未注入 MissionBehavior：FeatureGate(EnableMinimap) 关闭"));
                return;
            }

            bool supported = MissionSceneGuard.IsTacticalMapSupported(mission);
            TacticalMapHtmlUiDebug.Log("MISSION_START", "sceneSupported=" + supported);
            if (!supported)
            {
                InformationManager.DisplayMessage(new InformationMessage("[TMap] 未注入 MissionBehavior：非战场场景（无地形）"));
                return;
            }

            mission.AddMissionBehavior(new TacticalMapMissionLogic());
            TacticalMapHtmlUiDebug.Log("MISSION_START", "TacticalMapMissionLogic added");
            InformationManager.DisplayMessage(new InformationMessage("[TMap] 已注入 TacticalMapMissionLogic"));
        }
    }
}
