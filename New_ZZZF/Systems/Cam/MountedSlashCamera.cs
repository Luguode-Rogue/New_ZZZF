using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

namespace MountedSlashCamera
{
    // 标记此类为Harmony补丁，目标类为MissionScreen
    [HarmonyPatch(typeof(MissionScreen))]
    internal static class MountedSlashCamera
    {
        // Harmony后置补丁方法，目标方法为MissionScreen.UpdateCamera
        [HarmonyPostfix]
        [HarmonyPatch("UpdateCamera")]
        private static void UpdateCameraMOD(
            ref MissionScreen __instance,          // 原始MissionScreen实例
            ref float ____cameraSpecialTargetAddedBearing,     // 目标水平旋转角（反射访问私有字段）
            ref float ____cameraSpecialTargetAddedElevation,   // 目标垂直旋转角
            ref float ____cameraSpecialCurrentAddedBearing,   // 当前水平旋转角
            ref float ____cameraSpecialCurrentAddedElevation, // 当前垂直旋转角
            ref float ____cameraBearingDelta,      // 水平旋转变化量（玩家输入）
            ref float ____cameraElevationDelta,    // 垂直旋转变化量（玩家输入）
            ref Vec3 ____cameraSpecialCurrentPositionToAdd,   // 当前摄像机位置偏移
            ref Vec3 ____cameraSpecialTargetPositionToAdd)    // 目标摄像机位置偏移
        {
            // 获取玩家主控角色
            Agent mainAgent = __instance.Mission.MainAgent;

            // 条件检查：功能启用 + 骑马 + 第三人称 + 未查看角色面板
            bool isActive = MountedSlashCameraMissionLogic.CameraON
                && mainAgent.MountAgent != null
                && !__instance.Mission.CameraIsFirstPerson
                && !__instance.IsViewingCharacter();

            if (isActive)
            {
                // 检测是否处于架枪冲刺状态（返回3表示正在架枪）
                int couchState = GetCouchLanceState();
                bool isCouchLance = couchState == 3;

                // 获取当前攻击方向（左/右）
                Agent.UsageDirection attackDir = mainAgent.GetAttackDirection();

                // 获取角色头部骨骼索引
                sbyte headBoneIndex = mainAgent.Monster.HeadLookDirectionBoneIndex;

                // 计算头部骨骼的世界坐标框架
                MatrixFrame boneFrame = mainAgent.AgentVisuals.GetSkeleton()
                    .GetBoneEntitialFrame(headBoneIndex, true);

                // 应用第一人称摄像机偏移到骨骼框架
                boneFrame.origin = boneFrame.TransformToParent(
                    mainAgent.Monster.FirstPersonCameraOffsetWrtHead
                );

                // 架枪冲刺分支
                if (isCouchLance)
                {
                    // 应用长枪冲刺专用偏移
                    boneFrame.origin.x += couchCameraOffsetRight;  // 水平右偏移
                    boneFrame.origin.y += couchCameraOffsetForward;// 前向偏移

                    // 转换到世界坐标系
                    MatrixFrame worldFrame = mainAgent.AgentVisuals.GetFrame()
                        .TransformToParent(boneFrame);

                    // 计算相对于角色的位置差，设置高度偏移
                    ____cameraSpecialTargetPositionToAdd = new Vec3(
                        worldFrame.origin.x - mainAgent.Position.x,
                        worldFrame.origin.y - mainAgent.Position.y,
                        couchCameraOffsetHeight  // 架枪时提升高度
                    );
                }
                else  // 普通攻击分支
                {
                    // 获取当前攻击阶段（准备/释放）
                    Agent.ActionStage actionStage = mainAgent.GetCurrentActionStage(1);
                    bool isAttacking = actionStage == Agent.ActionStage.AttackReady
                                     || actionStage == Agent.ActionStage.AttackRelease;

                    if (isAttacking)
                    {
                        // 锁定角色头部朝向
                        //mainAgent.IsLookDirectionLocked = true;

                        // 右侧攻击处理
                        if (attackDir == Agent.UsageDirection.AttackRight
                            && !_AttackLEFT)
                        {
                            _AttackRIGHT = true;  // 标记右侧攻击状态
                            boneFrame.origin.x += cameraOffsetRight;  // 右偏移
                            boneFrame.origin.y += cameraOffsetForward;// 前偏移

                            // 计算世界坐标
                            MatrixFrame worldFrame = mainAgent.AgentVisuals.GetFrame()
                                .TransformToParent(boneFrame);

                            // 更新摄像机旋转参数（平滑过渡）
                            ____cameraSpecialTargetAddedBearing = MBMath.WrapAngle(____cameraSpecialTargetAddedBearing + ____cameraBearingDelta);
                            ____cameraSpecialTargetAddedElevation = MBMath.WrapAngle(____cameraSpecialTargetAddedElevation + ____cameraElevationDelta);
                            ____cameraSpecialCurrentAddedBearing = MBMath.WrapAngle(____cameraSpecialCurrentAddedBearing + ____cameraBearingDelta);
                            ____cameraSpecialCurrentAddedElevation = MBMath.WrapAngle(____cameraSpecialCurrentAddedElevation + ____cameraElevationDelta);

                            // 设置目标位置（降低高度）
                            ____cameraSpecialTargetPositionToAdd = new Vec3(
                                worldFrame.origin.x - mainAgent.Position.x,
                                worldFrame.origin.y - mainAgent.Position.y,
                                -cameraOffsetHeight);  // 负值表示降低高度

                        }
                        // 左侧攻击处理（逻辑镜像）
                        else if (attackDir == Agent.UsageDirection.AttackLeft
                               && !_AttackRIGHT)
                        {
                            _AttackLEFT = true;
                            boneFrame.origin.x -= cameraOffsetLeft;  // 左偏移
                            boneFrame.origin.y += cameraOffsetForward;
                            // ...（类似右侧处理逻辑）

                            // 计算世界坐标
                            MatrixFrame worldFrame = mainAgent.AgentVisuals.GetFrame()
                                .TransformToParent(boneFrame);

                            // 更新摄像机旋转参数（平滑过渡）
                            ____cameraSpecialTargetAddedBearing = MBMath.WrapAngle(____cameraSpecialTargetAddedBearing + ____cameraBearingDelta);
                            ____cameraSpecialTargetAddedElevation = MBMath.WrapAngle(____cameraSpecialTargetAddedElevation + ____cameraElevationDelta);
                            ____cameraSpecialCurrentAddedBearing = MBMath.WrapAngle(____cameraSpecialCurrentAddedBearing + ____cameraBearingDelta);
                            ____cameraSpecialCurrentAddedElevation = MBMath.WrapAngle(____cameraSpecialCurrentAddedElevation + ____cameraElevationDelta);

                            // 设置目标位置（降低高度）
                            ____cameraSpecialTargetPositionToAdd = new Vec3(
                                worldFrame.origin.x - mainAgent.Position.x,
                                worldFrame.origin.y - mainAgent.Position.y,
                                -cameraOffsetHeight);  // 负值表示降低高度

                        }
                    }
                    else  // 非攻击状态
                    {
                        // 重置攻击标记和解锁视角
                        _AttackRIGHT = false;
                        _AttackLEFT = false;
                        mainAgent.IsLookDirectionLocked = false;
                        ____cameraSpecialTargetPositionToAdd = Vec3.Zero;
                    }
                }
            }
            else  // 功能未激活时复位摄像机
            {
                ____cameraSpecialTargetPositionToAdd = Vec3.Zero;
            }
        }

        // 生成的辅助方法：检测武器是否可架枪
        [CompilerGenerated]
        internal static bool IsWeaponCouchable(MissionWeapon weapon)
        {
            if (weapon.IsEmpty) return false;
            foreach (var weaponData in weapon.Item.Weapons)
            {
                if (weaponData.WeaponDescriptionId?.IndexOf("couch", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        // 生成的辅助方法：检测是否处于被动武器使用状态
        [CompilerGenerated]
        internal static bool IsPassiveUsageActive(MissionWeapon weapon)
        {
            return !weapon.IsEmpty
                && MBItem.GetItemIsPassiveUsage(weapon.CurrentUsageItem.ItemUsage);
        }

        // 生成的辅助方法：获取架枪状态（0=未架枪，3=架枪中）
        [CompilerGenerated]
        internal static int GetCouchLanceState()
        {
            if (Agent.Main == null) return 0;
            MissionWeapon weapon = Agent.Main.WieldedWeapon;
            if (Agent.Main.HasMount && IsWeaponCouchable(weapon))
            {
                return IsPassiveUsageActive(weapon) ? 3 : 0;
            }
            return 0;
        }

        //// 从XML加载的配置参数
        //private static float cameraOffsetRight = Main.GetValueForXMLParameter(CameraOffsetRight);
        //private static float cameraOffsetLeft = Main.GetValueForXMLParameter(CameraOffsetLeft);
        //private static float cameraOffsetForward = Main.GetValueForXMLParameter(CameraOffsetForward);
        //private static float cameraOffsetHeight = Main.GetValueForXMLParameter(CameraOffsetHeight);
        //private static float couchCameraOffsetRight = Main.GetValueForXMLParameter(CouchCameraOffsetRight);
        //private static float couchCameraOffsetForward = Main.GetValueForXMLParameter(CouchCameraOffsetForward);
        //private static float couchCameraOffsetHeight = Main.GetValueForXMLParameter(CouchCameraOffsetHeight);

        private static float cameraOffsetRight = 1.0f;        // 右侧攻击水平偏移
        private static float cameraOffsetLeft = 1.0f;         // 左侧攻击水平偏移
        private static float cameraOffsetForward = -0.75f;    // 攻击时前向偏移（负值可能表示向后）
        private static float cameraOffsetHeight = 1.0f;       // 普通攻击高度偏移

        private static float couchCameraOffsetRight = 0.75f;   // 架枪冲刺水平偏移
        private static float couchCameraOffsetForward = 0.35f; // 架枪冲刺前向偏移
        private static float couchCameraOffsetHeight = 0.25f;  // 架枪冲刺高度提升
        // 攻击方向状态标记
        private static bool _AttackRIGHT = false;
        private static bool _AttackLEFT = false;
    }
}