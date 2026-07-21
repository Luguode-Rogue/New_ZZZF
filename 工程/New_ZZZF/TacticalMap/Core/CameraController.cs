using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 镜头联动控制器（供 Harmony 后置补丁读取）。
    /// 开启"镜头联动"模式后，点击小地图会把镜头平滑飞向目标点。
    /// </summary>
    public sealed class CameraController
    {
        public static CameraController Instance { get; set; }

        public bool Active { get; set; }
        public Vec2 TargetWorldPos { get; private set; }

        public void Enable(Vec2 worldPos)
        {
            Active = true;
            TargetWorldPos = worldPos;
        }

        public void Disable()
        {
            Active = false;
        }

        public bool Toggle()
        {
            Active = !Active;
            return Active;
        }
    }
}
