using TaleWorlds.MountAndBlade;
using New_ZZZF.GUI;

namespace New_ZZZF
{
    /// <summary>
    /// 战场状态 HUD 的 MissionView 适配层。
    /// HTML 页面、输入和窗口生命周期全部由 MissionAgentStatusHtmlUi / Framework 负责。
    /// </summary>
    public sealed class NewZZZF_MissionAgentStatusView : MissionView
    {
        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            MissionAgentStatusHtmlUi.Instance.Tick(dt);
        }

        public override void OnRemoveBehavior()
        {
            MissionAgentStatusHtmlUi.Instance.StopForMission();
            base.OnRemoveBehavior();
        }
    }
}
