using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using HarmonyLib;
using New_ZZZF.TacticalMap.Config;
using BannerlordHtmlUI;

namespace New_ZZZF.TacticalMap.UI
{
    internal static class TacticalMapHtmlUiDebugPatch
    {
        private static bool _patched;
        private static Harmony _harmony;
        private static FieldInfo _formField;
        private static FieldInfo _webField;

        public static void Patch(Harmony harmony)
        {
            if (_patched || harmony == null) return;
            _harmony = harmony;
            _formField = AccessTools.Field(typeof(HtmlUiHost), "_form");
            _webField = AccessTools.Field(typeof(HtmlUiHost), "_web");

            PatchMethod(typeof(TacticalMapHtmlUi), "InitializeOnFrameworkReady", nameof(AfterInitializeRequest));
            PatchMethod(typeof(TacticalMapHtmlUi), "Register", nameof(AfterRegister));
            PatchMethod(typeof(TacticalMapHtmlUi), "SetVisible", nameof(AfterSetVisible));
            PatchMethod(typeof(TacticalMapHtmlUi), "SetUiState", nameof(AfterSetUiState));
            PatchMethod(typeof(TacticalMapHtmlUi), "ApplyInputMode", nameof(AfterApplyInputMode));
            PatchMethod(typeof(TacticalMapHtmlUiBridgePatch), "AttachFromInstance", nameof(AfterAttachFromInstance));
            PatchMethod(typeof(HtmlUiPageManager), "Open", nameof(AfterPageOpen));
            PatchMethod(typeof(HtmlUiHost), "Navigate", nameof(AfterNavigate));
            PatchMethod(typeof(HtmlUiHost), "OnNavigationCompleted", nameof(AfterNavigationCompleted));
            PatchMethod(typeof(HtmlUiHost), "SetInputMode", nameof(AfterHostInputMode));
            PatchMethod(typeof(HtmlUiHost), "FollowBannerlordWindow", nameof(AfterFollowWindow));

            _patched = true;
            TacticalMapHtmlUiDebug.Log("PATCH", "TacticalMap HtmlUI diagnostic patches installed");
        }

        private static void PatchMethod(Type type, string method, string postfix)
        {
            try
            {
                var target = AccessTools.Method(type, method);
                var callback = AccessTools.Method(typeof(TacticalMapHtmlUiDebugPatch), postfix);
                if (target == null) { TacticalMapHtmlUiDebug.Log("PATCH", "missing method " + type.FullName + "." + method); return; }
                if (callback == null) { TacticalMapHtmlUiDebug.Log("PATCH", "missing diagnostic callback " + postfix); return; }
                _harmony.Patch(target, postfix: new HarmonyMethod(callback));
                TacticalMapHtmlUiDebug.Log("PATCH", "patched " + type.Name + "." + method);
            }
            catch (Exception ex) { TacticalMapHtmlUiDebug.Log("PATCH_ERROR", type.Name + "." + method + " -> " + ex); }
        }

        private static void AfterInitializeRequest() => TacticalMapHtmlUiDebug.Log("UI_INIT", "InitializeOnFrameworkReady returned");

        private static void AfterRegister(TacticalMapHtmlUi __instance) => TacticalMapHtmlUiDebug.Log("UI_REGISTER", "registered=" + __instance.IsRegistered + ", visible=" + __instance.IsVisible + ", state=" + __instance.State);

        private static void AfterSetVisible(TacticalMapHtmlUi __instance, bool visible) => TacticalMapHtmlUiDebug.Log("UI_VISIBLE", "SetVisible(" + visible + ") returned; visible=" + __instance.IsVisible + ", state=" + __instance.State + ", registered=" + __instance.IsRegistered);

        private static void AfterSetUiState(TacticalMapHtmlUi __instance, TacticalMapHtmlUi.UiState state) => TacticalMapHtmlUiDebug.Log("UI_STATE", "SetUiState(" + state + ") returned; actual=" + __instance.State + ", visible=" + __instance.IsVisible + ", fullscreen=" + __instance.IsFullscreen + ", interactive=" + __instance.IsInteractive);

        private static void AfterApplyInputMode(TacticalMapHtmlUi __instance) => TacticalMapHtmlUiDebug.Log("UI_INPUT", "ApplyInputMode returned; interactive=" + __instance.IsInteractive);

        private static void AfterAttachFromInstance(object instance)
        {
            var ui = TacticalMapBootstrap.HtmlUi;
            TacticalMapHtmlUiDebug.Log("BRIDGE_ATTACH", "ui=" + (ui == null ? "null" : "exists") + ", uiVisible=" + (ui != null && ui.IsVisible) + ", uiState=" + (ui == null ? "null" : ui.State.ToString()));
        }

        private static void AfterPageOpen(HtmlUiPageManager __instance, string id, bool __result)
        {
            try
            {
                var current = __instance.Current;
                TacticalMapHtmlUiDebug.Log("PAGE_OPEN", "Open('" + id + "') result=" + __result + ", current=" + (__instance.CurrentId ?? "<null>") + ", owner=" + (current?.OwnerId ?? "<null>") + ", path=" + (current?.RelativePath ?? "<null>"));
            }
            catch (Exception ex) { TacticalMapHtmlUiDebug.Log("PAGE_OPEN_ERROR", ex.ToString()); }
        }

        private static void AfterNavigate(HtmlUiPage page)
        {
            try
            {
                var host = HtmlUiService.Host;
                TacticalMapHtmlUiDebug.Log("NAVIGATE", "page=" + (page?.Id ?? "<null>") + ", path=" + (page?.ContentRootId ?? "<null>") + ":/" + (page?.RelativePath ?? "<null>") + ", webReady=" + host?.IsWebViewReady + ", visible=" + host?.IsVisible + ", input=" + host?.InputMode);
            }
            catch (Exception ex) { TacticalMapHtmlUiDebug.Log("NAVIGATE_ERROR", ex.ToString()); }
        }

        private static void AfterNavigationCompleted(HtmlUiHost __instance)
        {
            try
            {
                var current = HtmlUiService.Pages?.Current;
                TacticalMapHtmlUiDebug.Log("NAV_COMPLETED", "hostVisible=" + __instance.IsVisible + ", input=" + __instance.InputMode + ", page=" + (current?.Id ?? "<null>") + ", path=" + (current?.RelativePath ?? "<null>"));
                LogWindowState(__instance, "NAV_WINDOW");
            }
            catch (Exception ex) { TacticalMapHtmlUiDebug.Log("NAV_COMPLETED_ERROR", ex.ToString()); }
        }

        private static void AfterHostInputMode(HtmlUiHost __instance, HtmlUiInputMode mode)
        {
            LogWindowState(__instance, "INPUT_WINDOW");
        }

        private static void AfterFollowWindow(HtmlUiHost __instance)
        {
            LogWindowState(__instance, "FOLLOW_WINDOW");
        }

        private static void LogWindowState(HtmlUiHost host, string stage)
        {
            try
            {
                var form = _formField?.GetValue(host) as Form;
                var webObject = _webField?.GetValue(host);
                var fg = GetForegroundWindow();
                var game = Process.GetCurrentProcess().MainWindowHandle;
                string source = "<null>";
                string core = "unknown";
                try
                {
                    if (webObject != null)
                    {
                        var sourceProperty = webObject.GetType().GetProperty("Source", BindingFlags.Instance | BindingFlags.Public);
                        source = sourceProperty?.GetValue(webObject, null)?.ToString() ?? "<null>";
                        var coreProperty = webObject.GetType().GetProperty("CoreWebView2", BindingFlags.Instance | BindingFlags.Public);
                        core = coreProperty?.GetValue(webObject, null) == null ? "null" : "ready";
                    }
                }
                catch { }

                string bounds = form == null ? "null" : form.Bounds.ToString();
                string formHwnd = form == null ? "null" : form.Handle.ToString();
                string visible = form == null ? "null" : form.Visible.ToString();
                string opacity = form == null ? "null" : form.Opacity.ToString("F2");
                string topMost = form == null ? "null" : form.TopMost.ToString();
                bool formFg = form != null && fg == form.Handle;
                TacticalMapHtmlUiDebug.Log(stage,
                    "hostVisible=" + host.IsVisible +
                    ", input=" + host.InputMode +
                    ", formVisible=" + visible +
                    ", formFg=" + formFg +
                    ", topMost=" + topMost +
                    ", opacity=" + opacity +
                    ", bounds=" + bounds +
                    ", formHwnd=" + formHwnd +
                    ", gameHwnd=" + game +
                    ", fg=" + fg +
                    ", webCore=" + core +
                    ", source=" + source);
            }
            catch (Exception ex) { TacticalMapHtmlUiDebug.Log(stage + "_ERROR", ex.ToString()); }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
