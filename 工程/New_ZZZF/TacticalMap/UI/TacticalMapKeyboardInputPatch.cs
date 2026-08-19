using System;
using System.Reflection;
using BannerlordHtmlUI;
using HarmonyLib;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// TacticalMap uses Framework mouse capture with keyboard passthrough.
    /// N therefore continues to be sampled from Bannerlord's game-thread input even while the page is interactive.
    /// Harmony discovers this patch through the existing New_ZZZF PatchAll call.
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

        [HarmonyPatch(typeof(HtmlUiPageManager), "Open")]
        private static class PageKeyboardPolicy
        {
            private static void Postfix(string id)
            {
                try
                {
                    var page = HtmlUiService.Pages.Current;
                    if (page == null) return;
                    if (!string.Equals(page.OwnerId, TacticalMapHtmlUi.OwnerId, StringComparison.OrdinalIgnoreCase)) return;
                    page.KeyboardInputMode = HtmlUiKeyboardInputMode.Passthrough;
                    TacticalMapLog.Info("TacticalMap page keyboard policy=Passthrough. PageId=" + page.Id);
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Warn("Failed to apply TacticalMap page keyboard policy: " + ex.Message);
                }
            }
        }
    }
}
