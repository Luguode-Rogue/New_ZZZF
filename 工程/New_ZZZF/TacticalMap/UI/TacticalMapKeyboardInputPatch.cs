using System;
using System.Reflection;
using HarmonyLib;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// Keeps TacticalMap's N gesture on Bannerlord's game-thread input while the HtmlUI host
    /// is in Framework MouseCaptured mode. This patch does not touch WebView focus or keyboard policy.
    /// </summary>
    [HarmonyPatch(typeof(TacticalMapHtmlUi), "Tick")]
    internal static class TacticalMapKeyboardInputPatch
    {
        private static readonly Action<TacticalMapHtmlUi, float> UpdateToggleKey = CreateToggleDelegate();

        private static Action<TacticalMapHtmlUi, float> CreateToggleDelegate()
        {
            var method = typeof(TacticalMapHtmlUi).GetMethod("UpdateToggleKey", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new MissingMethodException("TacticalMapHtmlUi.UpdateToggleKey was not found.");
            return (Action<TacticalMapHtmlUi, float>)method.CreateDelegate(typeof(Action<TacticalMapHtmlUi, float>));
        }

        private static void Prefix(TacticalMapHtmlUi __instance, out bool __state)
        {
            __state = __instance != null && __instance.IsInteractive;
        }

        private static void Postfix(TacticalMapHtmlUi __instance, float dt, bool __state)
        {
            if (!__state || __instance == null) return;
            try
            {
                UpdateToggleKey(__instance, dt);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("Interactive keyboard passthrough tick failed.", ex);
            }
        }
    }
}
