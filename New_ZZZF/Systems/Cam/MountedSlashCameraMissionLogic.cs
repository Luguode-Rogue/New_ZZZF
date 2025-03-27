using System;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.MountAndBlade;

namespace MountedSlashCamera
{
    // 任务逻辑类，继承自MissionLogic，用于管理摄像机功能状态
    public class MountedSlashCameraMissionLogic : MissionLogic
    {
        // 任务创建后的初始化
        public override void OnAfterMissionCreated()
        {
            base.OnAfterMissionCreated();
            CameraON = false;  // 重置摄像机功能状态
        }

        // 当代理（角色）受到攻击时的回调
        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            // 检测是否是主控角色死亡
            bool isMainAgentDead = affectedAgent != null
                && affectedAgent.Health <= 0f
                && affectedAgent.IsMainAgent;

            if (isMainAgentDead)
            {
                CameraON = false;  // 死亡时强制关闭摄像机功能
            }
            base.OnAgentHit(affectedAgent, affectorAgent, affectorWeapon, blow, attackCollisionData);
        }

        // 每帧任务逻辑更新
        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            // 异常情况处理：无当前任务时关闭功能
            if (Mission.Current == null)
            {
                CameraON = false;
                return;
            }

            // 检测快捷键按下事件
            if (Input.IsKeyPressed(InputKey.RightAlt))
            {
                // 检查是否在马上
                bool isMounted = Mission.Current.MainAgent?.MountAgent != null;

                if (isMounted)
                {
                    // 切换摄像机状态
                    if (!CameraON)
                    {
                        CameraON = true;
                        InformationManager.DisplayMessage(
                            new InformationMessage("骑乘斩击摄像机: 启用"));  // 状态提示
                    }
                    else
                    {
                        CameraON = false;
                        Mission.Current.MainAgent.IsLookDirectionLocked = false;  // 解除视角锁定
                        InformationManager.DisplayMessage(
                            new InformationMessage("骑乘斩击摄像机: 禁用"));
                    }
                }
                else
                {
                    CameraON = false;  // 非骑乘状态强制关闭
                }
            }
        }

        // 任务结束时的清理
        protected override void OnEndMission()
        {
            CameraON = false;  // 重置状态
            base.OnEndMission();
        }

        // 静态属性：全局摄像机功能开关
        public static bool CameraON { get; set; } = false;
    }
}