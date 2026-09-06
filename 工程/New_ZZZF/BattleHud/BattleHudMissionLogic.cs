using System;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.BattleHud
{
    /// <summary>
    /// 随 Mission 生命周期驱动的战斗 HUD MissionLogic：
    /// 开战 Show Surface、每帧 Tick（内部 10Hz 节流）、终局 Hide。
    /// </summary>
    public sealed class BattleHudMissionLogic : MissionLogic
    {
        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            try
            {
                BattleHudHtmlUi.Instance.OnMissionStarted();
                BattleHudHtmlUi.Instance.Tick(dt);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[BattleHud] Mission tick failed.", ex);
            }
        }

        protected override void OnEndMission()
        {
            try { BattleHudHtmlUi.Instance.OnMissionEnded(); }
            catch (Exception ex) { TacticalMapLog.Error("[BattleHud] Mission end cleanup failed.", ex); }
            base.OnEndMission();
        }
    }
}
