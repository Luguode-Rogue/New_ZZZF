using System.Reflection;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 通过 Harmony 后置补丁接管战场相机（与项目已有的 MountedSlashCamera 同一机制，但独立注册、互不干扰）。
    /// 仅在小地图"镜头联动"模式激活时写入相机偏移/朝向私有字段。
    /// </summary>
    public static class TacticalCameraPatch
    {
        public static void Patch(Harmony harmony)
        {
            var method = typeof(MissionScreen).GetMethod(
                "UpdateCamera",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(float) },
                null);
            if (method == null) return;

            harmony.Patch(
                method,
                postfix: new HarmonyMethod(typeof(TacticalCameraPatch).GetMethod(
                    nameof(UpdateCameraPostfix),
                    BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static void UpdateCameraPostfix(MissionScreen __instance)
        {
            var cam = CameraController.Instance;
            if (cam == null || !cam.Active) return;
            var mission = Mission.Current;
            if (mission == null || mission.MainAgent == null) return;

            var main = mission.MainAgent;
            Vec3 agentPos = main.Position;
            Vec3 delta = new Vec3(
                cam.TargetWorldPos.X - agentPos.X,
                cam.TargetWorldPos.Y - agentPos.Y,
                0f);

            // 抵达目标附近后交还镜头控制（避免一直被偏移锁住）
            float distSq = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;
            if (distSq < 9f)
            {
                cam.Active = false;
                return;
            }

            // 朝目标点的方位角
            float bearing = MathF.Atan2(delta.y, delta.x);

            var t = Traverse.Create(__instance);
            t.Field("_cameraSpecialTargetPositionToAdd").SetValue(new Vec3(delta.x, delta.y, 6f));
            t.Field("_cameraSpecialTargetAddedBearing").SetValue(bearing);
            t.Field("_cameraSpecialCurrentAddedBearing").SetValue(bearing);
            t.Field("_cameraSpecialTargetAddedElevation").SetValue(0f);
            t.Field("_cameraSpecialCurrentAddedElevation").SetValue(0f);
        }
    }
}
