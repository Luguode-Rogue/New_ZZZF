using HarmonyLib;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 【已废弃 / 保留空壳以兼容 Bootstrap 调用】
    ///
    /// 旧实现通过 Harmony 后置补丁 MissionScreen.UpdateCamera，用 Traverse 写入
    /// _cameraSpecialTargetPositionToAdd / _cameraSpecialTargetAddedBearing 等私有字段
    /// 来实现「镜头切到地图点」。该方案存在根本性缺陷，已整体废弃：
    ///
    ///   1. 这组字段是官方给「对话/处决」做**相对主角的小幅偏移**用的，
    ///      语义上根本不是绝对世界坐标，无法表达「飞到地图任意一点」。
    ///   2. 官方 UpdateCamera 在常规分支里每帧会把这组字段无条件清零
    ///      （MissionScreen.cs 约 1455-1459 行），postfix 写进去的值下一帧即被抹除，
    ///      表现为镜头抖动或完全不动。
    ///   3. 旧的「抵达判定」算的是 MainAgent 到目标点的距离，而镜头飞走时角色并没有移动，
    ///      导致 Active 永远无法复位，镜头再也交还不回来。
    ///
    /// 新实现见 CameraController：改为接管 MissionScreen.CustomCamera。
    /// 官方 CheckForUpdateCamera 在 CustomCamera != null 时会直接 SetCamera 并 return，
    /// 短路掉全部跟随/碰撞/边界逻辑，因此无需任何 Harmony 补丁即可完全掌控相机位姿。
    /// </summary>
    public static class TacticalCameraPatch
    {
        public static void Patch(Harmony harmony)
        {
            // 有意留空：相机控制已改由 CustomCamera 接管，不再需要打补丁。
        }
    }
}
