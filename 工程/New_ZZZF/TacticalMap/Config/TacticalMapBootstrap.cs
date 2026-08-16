using HarmonyLib;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.UI;
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// 战术地图功能总入口。旧 Gauntlet UI 与新的 HtmlUI 视图并行，不互相替换。
    /// </summary>
    public static class TacticalMapBootstrap
    {
        private static Harmony _harmony;
        private static TacticalMapHtmlUi _htmlUi;

        public static TacticalMapHtmlUi HtmlUi => _htmlUi;

        public static void OnSubModuleLoad()
        {
            if (!FeatureGate.Enabled) { InformationManager.DisplayMessage(new InformationMessage("[TMap] 引导跳过：FeatureGate(EnableMinimap) 关闭")); return; }
            _harmony = new Harmony("TacticalMap");
            TacticalCameraPatch.Patch(_harmony);

            _htmlUi = new TacticalMapHtmlUi();
            _htmlUi.InitializeOnFrameworkReady();

            InformationManager.DisplayMessage(new InformationMessage("[TMap] 引导完成：旧版 Gauntlet + 新版 HtmlUI 并行就绪"));
        }

        public static void OnMissionStart(Mission mission)
        {
            if (!FeatureGate.Enabled) { InformationManager.DisplayMessage(new InformationMessage("[TMap] 未注入 MissionBehavior：FeatureGate(EnableMinimap) 关闭")); return; }

            // 酒馆/城镇/竞技场等场景没有地形数据，烘焙会触发引擎侧 AccessViolationException（无法被 C# 捕获），
            // 因此这里直接不注入 MissionBehavior。
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
