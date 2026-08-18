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
            if (!FeatureGate.Enabled)
            {
                InformationManager.DisplayMessage(new InformationMessage("[TMap] 引导跳过：FeatureGate(EnableMinimap) 关闭"));
                return;
            }

            _harmony = new Harmony("TacticalMap");
            TacticalCameraPatch.Patch(_harmony);
            TacticalMapHtmlUiBridgePatch.Patch(_harmony);
            TacticalMapHtmlUiInputPatch.Patch(_harmony);

            _htmlUi = new TacticalMapHtmlUi();

            HtmlUiService.OnReady(() =>
            {
                try
                {
                    HtmlUiOverlayTransparency.Enable(HtmlUiService.Host);
                    InformationManager.DisplayMessage(new InformationMessage("[TMap][HtmlUI] 已启用透明 Overlay"));
                }
                catch (System.Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"[TMap][HtmlUI] 透明 Overlay 启用失败: {ex.GetType().Name}: {ex.Message}"));
                }
            });

            _htmlUi.InitializeOnFrameworkReady();
            HtmlUiService.OnReady(() => TacticalMapHtmlUiBridgePatch.OnHtmlUiFrameworkReady());

            InformationManager.DisplayMessage(new InformationMessage("[TMap] 引导完成：TacticalMap HTMLUI 就绪"));
        }

        public static void OnMissionStart(Mission mission)
        {
            if (!FeatureGate.Enabled)
            {
                InformationManager.DisplayMessage(new InformationMessage("[TMap] 未注入 MissionBehavior：FeatureGate(EnableMinimap) 关闭"));
                return;
            }

            if (!MissionSceneGuard.IsTacticalMapSupported(mission))
            {
                InformationManager.DisplayMessage(new InformationMessage("[TMap] 未注入 MissionBehavior：非战场场景（无地形）"));
                return;
            }

            mission.AddMissionBehavior(new TacticalMapMissionLogic());
            InformationManager.DisplayMessage(new InformationMessage("[TMap] 已注入 TacticalMapMissionLogic"));
        }
    }
}
