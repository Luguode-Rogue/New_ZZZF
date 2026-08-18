using System;
using HarmonyLib;
using New_ZZZF.TacticalMap.Config;
using BannerlordHtmlUI;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// 仅用于排查 HtmlUI 生命周期，不改变方法行为。
    /// </summary>
    internal static class TacticalMapHtmlUiDebugPatch
    {
        private static bool _patched;
        private static Harmony _harmony;

        public static void Patch(Harmony harmony)
        {
            if (_patched || harmony == null) return;
            _harmony = harmony;

            PatchMethod(typeof(TacticalMapHtmlUi), "InitializeOnFrameworkReady", nameof(AfterInitializeRequest));
            PatchMethod(typeof(TacticalMapHtmlUi), "Register", nameof(AfterRegister));
            PatchMethod(typeof(TacticalMapHtmlUi), "SetVisible", nameof(AfterSetVisible));
            PatchMethod(typeof(TacticalMapHtmlUi), "SetUiState", nameof(AfterSetUiState));
            PatchMethod(typeof(TacticalMapHtmlUi), "ApplyInputMode", nameof(AfterApplyInputMode));
            PatchMethod(typeof(TacticalMapHtmlUiBridgePatch), "AttachFromInstance", nameof(AfterAttachFromInstance));
            PatchMethod(typeof(HtmlUiPageManager), "Open", nameof(AfterPageOpen));
            PatchMethod(typeof(HtmlUiHost), "Navigate", nameof(AfterNavigate));
            PatchMethod(typeof(HtmlUiHost), "OnNavigationCompleted", nameof(AfterNavigationCompleted));

            _patched = true;
            TacticalMapHtmlUiDebug.Log("PATCH", "TacticalMap HtmlUI diagnostic patches installed");
        }

        private static void PatchMethod(Type type, string method, string postfix)
        {
            try
            {
                var target = AccessTools.Method(type, method);
                if (target == null)
                {
                    TacticalMapHtmlUiDebug.Log("PATCH", "missing method " + type.FullName + "." + method);
                    return;
                }

                var callback = AccessTools.Method(typeof(TacticalMapHtmlUiDebugPatch), postfix);
                if (callback == null)
                {
                    TacticalMapHtmlUiDebug.Log("PATCH", "missing diagnostic callback " + postfix);
                    return;
                }

                _harmony.Patch(target, postfix: new HarmonyMethod(callback));
                TacticalMapHtmlUiDebug.Log("PATCH", "patched " + type.Name + "." + method);
            }
            catch (Exception ex)
            {
                TacticalMapHtmlUiDebug.Log("PATCH_ERROR", type.Name + "." + method + " -> " + ex);
            }
        }

        private static void AfterInitializeRequest()
        {
            TacticalMapHtmlUiDebug.Log("UI_INIT", "InitializeOnFrameworkReady returned");
        }

        private static void AfterRegister(TacticalMapHtmlUi __instance)
        {
            TacticalMapHtmlUiDebug.Log("UI_REGISTER", "Register returned; registered=" + __instance.IsRegistered + ", visible=" + __instance.IsVisible + ", state=" + __instance.State);
        }

        private static void AfterSetVisible(TacticalMapHtmlUi __instance, bool visible)
        {
            TacticalMapHtmlUiDebug.Log("UI_VISIBLE", "SetVisible(" + visible + ") returned; visible=" + __instance.IsVisible + ", state=" + __instance.State + ", registered=" + __instance.IsRegistered);
        }

        private static void AfterSetUiState(TacticalMapHtmlUi __instance, TacticalMapHtmlUi.UiState state)
        {
            TacticalMapHtmlUiDebug.Log("UI_STATE", "SetUiState(" + state + ") returned; actual=" + __instance.State + ", visible=" + __instance.IsVisible + ", fullscreen=" + __instance.IsFullscreen + ", interactive=" + __instance.IsInteractive);
        }

        private static void AfterApplyInputMode(TacticalMapHtmlUi __instance)
        {
            TacticalMapHtmlUiDebug.Log("UI_INPUT", "ApplyInputMode returned; interactive=" + __instance.IsInteractive);
        }

        private static void AfterAttachFromInstance(object instance)
        {
            var ui = TacticalMapBootstrap.HtmlUi;
            TacticalMapHtmlUiDebug.Log("BRIDGE_ATTACH", "AttachFromInstance returned; ui=" + (ui == null ? "null" : "exists") + ", uiVisible=" + (ui != null && ui.IsVisible) + ", uiState=" + (ui == null ? "null" : ui.State.ToString()));
        }

        private static void AfterPageOpen(HtmlUiPageManager __instance, string id, bool __result)
        {
            try
            {
                var current = __instance.Current;
                TacticalMapHtmlUiDebug.Log("PAGE_OPEN", "Open('" + id + "') result=" + __result + ", current=" + (__instance.CurrentId ?? "<null>") + ", owner=" + (current?.OwnerId ?? "<null>") + ", path=" + (current?.RelativePath ?? "<null>"));
            }
            catch (Exception ex)
            {
                TacticalMapHtmlUiDebug.Log("PAGE_OPEN_ERROR", ex.ToString());
            }
        }

        private static void AfterNavigate(HtmlUiPage page)
        {
            try
            {
                TacticalMapHtmlUiDebug.Log("NAVIGATE", "Navigate('" + (page?.Id ?? "<null>") + "') requested path=" + (page?.ContentRootId ?? "<null>") + ":/" + (page?.RelativePath ?? "<null>") + ", webReady=" + HtmlUiService.Host?.IsWebViewReady + ", visible=" + HtmlUiService.Host?.IsVisible + ", input=" + HtmlUiService.Host?.InputMode);
            }
            catch (Exception ex)
            {
                TacticalMapHtmlUiDebug.Log("NAVIGATE_ERROR", ex.ToString());
            }
        }

        private static void AfterNavigationCompleted(HtmlUiHost __instance)
        {
            try
            {
                var current = HtmlUiService.Pages?.Current;
                TacticalMapHtmlUiDebug.Log("NAV_COMPLETED", "NavigationCompleted; hostVisible=" + __instance.IsVisible + ", input=" + __instance.InputMode + ", page=" + (current?.Id ?? "<null>") + ", path=" + (current?.RelativePath ?? "<null>"));
            }
            catch (Exception ex)
            {
                TacticalMapHtmlUiDebug.Log("NAV_COMPLETED_ERROR", ex.ToString());
            }
        }
    }
}
