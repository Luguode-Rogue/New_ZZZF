using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using New_ZZZF.GUI;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Diagnostics;
using New_ZZZF.TacticalMap.UI;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// N 键输入兜底。
    ///
    /// New_ZZZF 的 SubModule 已经使用 Input.IsKeyPressed 处理 TacticalMap N 键，
    /// 但当前运行环境中 N 的 Pressed 状态没有进入该入口。这里在 SubModule 的
    /// OnApplicationTick 之后检查真实 KeyDown 上升沿：
    /// - 如果 IsKeyPressed 已经成立，说明原入口负责切换，这里不重复处理。
    /// - 如果 IsKeyPressed 没成立但 KeyDown 出现新的上升沿，则这里补一次切换。
    ///
    /// 这样不会建立第二套 UI 状态机，只是补齐同一个游戏热键的输入检测。
    /// </summary>
    [HarmonyPatch(typeof(New_ZZZF.SubModule), "OnApplicationTick")]
    internal static class TacticalMapNKeyFallback
    {
        private static bool _wasDown;

        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                bool missionActive = Mission.Current != null;
                InputKey key = TacticalSettings.Instance.ToggleKey;
                bool isDown = missionActive && Input.IsKeyDown(key);
                bool risingEdge = isDown && !_wasDown;
                bool pressed = missionActive && Input.IsKeyPressed(key);
                _wasDown = isDown;

                if (!risingEdge)
                    return;

                TacticalMapLog.Info(
                    "TacticalMap N fallback input observed: key=" + key +
                    " pressed=" + pressed +
                    " pageVisible=" + TacticalMapHtmlUi.Instance.IsVisible +
                    " mode=" + TacticalMapHtmlUi.Instance.Mode +
                    " customSkillVisible=" + CustomSkillHtmlUi.Instance.IsVisible);

                // 如果原 SubModule 入口已经看到 IsKeyPressed，它负责唯一一次切换。
                if (pressed || !TacticalMapHtmlUi.Instance.IsVisible || CustomSkillHtmlUi.Instance.IsVisible)
                    return;

                TacticalMapHtmlUi.Instance.ToggleInteractive();
                TacticalMapLog.Info(
                    "TacticalMap N fallback toggled mode=" + TacticalMapHtmlUi.Instance.Mode);
            }
            catch (System.Exception ex)
            {
                TacticalMapLog.Error("TacticalMap N fallback input handling failed.", ex);
            }
        }
    }
}
