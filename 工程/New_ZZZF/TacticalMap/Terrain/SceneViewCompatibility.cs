using TaleWorlds.Engine;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// Bannerlord 当前目标版本的 SceneView 兼容层。
    ///
    /// REV12 从旧的 Tableau/SceneView 实验代码继承了 SetDoNotRenderThisFrame(false)
    /// 调用，但当前引用的 TaleWorlds.Engine.SceneView 并不公开该方法。
    ///
    /// REV12 已通过 SetRenderOnDemand(false) + SetEnable(true) 让 SceneView 连续渲染，
    /// 因此该旧调用在当前实现中不承担必要状态切换。保留同名扩展作为 no-op，
    /// 避免为了一个版本差异改动主拍摄状态机。
    /// </summary>
    internal static class SceneViewCompatibility
    {
        public static void SetDoNotRenderThisFrame(this SceneView view, bool doNotRender)
        {
            // Intentionally no-op on the current Bannerlord SceneView API.
            // Continuous rendering is controlled by SetRenderOnDemand(false).
        }
    }
}
