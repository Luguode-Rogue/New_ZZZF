using HarmonyLib;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.UI;
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// TacticalMap HTMLUI runtime entry.
    /// </summary>
    public static class TacticalMapBootstrap
    {
        private static Harmony _harmony;
        private static TacticalMapHtmlUi _htmlUi;

        public static TacticalMapHtmlUi HtmlUi => _htmlUi;

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
            TacticalMapHtmlUiBridgePatch.Patch(_harmony);
            TacticalMapHtmlUiInputPatch.Patch(_harmony);

            _htmlUi = new TacticalMapHtmlUi();
            _htmlUi.InitializeOnFrameworkReady();

            InformationManager.DisplayMessage(new InformationMessage(
                "[TMap] 引导完成：新版 HtmlUI 已注册"));
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
