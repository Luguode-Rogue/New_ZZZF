using HarmonyLib;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Core;
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// 小地图功能的总入口（与 ZZZF 主 SubModule 解耦）。
    /// 在 SubModule 的两个生命周期点被调用：
    ///   - OnSubModuleLoad：注册 Harmony 相机补丁（仅本功能，独立 Harmony id，避免双重 patch）
    ///   - OnMissionBehaviorInitialize：注入 MissionBehavior
    /// 这样整个 TacticalMap 文件夹可直接搬入独立 mod（仅需保留这两行调用）。
    /// </summary>
    public static class TacticalMapBootstrap
    {
        private static Harmony _harmony;

        public static void OnSubModuleLoad()
        {
            if (!FeatureGate.Enabled) { InformationManager.DisplayMessage(new InformationMessage("[TMap] 引导跳过：FeatureGate(EnableMinimap) 关闭")); return; }
            _harmony = new Harmony("TacticalMap");
            TacticalCameraPatch.Patch(_harmony);
            InformationManager.DisplayMessage(new InformationMessage("[TMap] 引导完成：已注册相机补丁"));
        }

        public static void OnMissionStart(Mission mission)
        {
            if (!FeatureGate.Enabled) { InformationManager.DisplayMessage(new InformationMessage("[TMap] 未注入 MissionBehavior：FeatureGate(EnableMinimap) 关闭")); return; }
            mission.AddMissionBehavior(new TacticalMapMissionLogic());
            InformationManager.DisplayMessage(new InformationMessage("[TMap] 已注入 TacticalMapMissionLogic"));
        }
    }
}
