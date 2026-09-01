using TaleWorlds.MountAndBlade;

namespace New_ZZZF.ArcherReposition
{
    /// <summary>
    /// 射手防发呆重定位 —— Mission 层驱动器。
    ///
    /// 本特性对工程的全部接线只有一处：
    ///   SubModule.OnMissionBehaviorInitialize 中 mission.AddMissionBehavior(new ArcherRepositionBehavior());
    ///
    /// 删除特性 = 删除本文件夹 + 删除那一行，零残留。
    /// </summary>
    public class ArcherRepositionBehavior : MissionLogic
    {
        public ArcherRepositionBehavior()
        {
            // 主线程、mission 初始化阶段安装 Harmony 补丁（幂等）。
            // 不依赖 SubModule 的 PatchAll 时机 —— 自定义战斗等任何进入战斗的路径都有效。
            ArcherRepositionPatchInstaller.EnsureInstalled();
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            ArcherRepositionLogic.TickMain(Mission);
        }

        public override void OnEndMissionInternal()
        {
            base.OnEndMissionInternal();
            ArcherRepositionStateStore.ResetAll();
        }
    }
}
