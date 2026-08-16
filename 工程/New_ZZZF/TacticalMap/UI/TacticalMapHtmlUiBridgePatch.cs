using System;
using System.Drawing;
using System.Reflection;
using HarmonyLib;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Windows.Forms;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.Config;
using BannerlordHtmlUI;

namespace New_ZZZF.TacticalMap.UI
{
    internal static class TacticalMapHtmlUiBridgePatch
    {
        private static readonly FieldInfo ControllerField = typeof(TacticalMapMissionLogic)
            .GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic);

        private static TacticalMapController _controller;
        private static TacticalMapMissionLogic _logicInstance;

        public static void Patch(Harmony harmony)
        {
            if (harmony == null || ControllerField == null) return;

            harmony.Patch(
                AccessTools.Method(typeof(TacticalMapMissionLogic), "OnAfterMissionCreated"),
                postfix: new HarmonyMethod(typeof(TacticalMapHtmlUiBridgePatch), nameof(OnAfterMissionCreatedPostfix)));

            harmony.Patch(
                AccessTools.Method(typeof(TacticalMapMissionLogic), "OnMissionTick"),
                postfix: new HarmonyMethod(typeof(TacticalMapHtmlUiBridgePatch), nameof(OnMissionTickPostfix)));

            harmony.Patch(
                AccessTools.Method(typeof(TacticalMapMissionLogic), "OnEndMission"),
                postfix: new HarmonyMethod(typeof(TacticalMapHtmlUiBridgePatch), nameof(OnEndMissionPostfix)));

            // HtmlUiHost uses the same WebView2 overlay host for all pages. Configure the
            // host immediately before navigation so TacticalMap can render over the game.
            var navigate = AccessTools.Method(typeof(HtmlUiHost), "Navigate");
            if (navigate != null)
            {
                harmony.Patch(
                    navigate,
                    prefix: new HarmonyMethod(typeof(TacticalMapHtmlUiBridgePatch), nameof(HtmlUiNavigatePrefix)));
            }
        }

        private static void OnAfterMissionCreatedPostfix(TacticalMapMissionLogic __instance)
        {
            try
            {
                AttachFromInstance(__instance);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TMap][HtmlUI] Mission attach 失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        private static void OnMissionTickPostfix(TacticalMapMissionLogic __instance)
        {
            try
            {
                if (!ReferenceEquals(_logicInstance, __instance) || _controller == null)
                    AttachFromInstance(__instance);

                var ui = TacticalMapBootstrap.HtmlUi;
                if (ui == null || _controller == null) return;

                if (ui.IsVisible != _controller.IsVisible)
                    ui.SetVisible(_controller.IsVisible);

                if (_controller.IsVisible)
                    ui.Tick();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TMap][HtmlUI] Tick 失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        private static void AttachFromInstance(TacticalMapMissionLogic instance)
        {
            if (instance == null || ControllerField == null) return;

            var controller = ControllerField.GetValue(instance) as TacticalMapController;
            if (controller == null) return;

            bool changed = !ReferenceEquals(_logicInstance, instance) || !ReferenceEquals(_controller, controller);
            _logicInstance = instance;
            _controller = controller;

            if (changed)
            {
                TacticalMapBootstrap.HtmlUi?.AttachController(controller);
                if (controller.IsVisible)
                    TacticalMapBootstrap.HtmlUi?.SetVisible(true);
            }
        }

        private static void HtmlUiNavigatePrefix(HtmlUiPage page)
        {
            if (page == null) return;

            try
            {
                bool transparent = string.Equals(
                    page.OwnerId,
                    "New_ZZZF.TacticalMap",
                    StringComparison.OrdinalIgnoreCase);

                ConfigureOverlayBackground(HtmlUiService.Host, transparent);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMap][HtmlUI] Overlay background 配置失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        private static void ConfigureOverlayBackground(HtmlUiHost host, bool transparent)
        {
            if (host == null) return;

            var hostType = typeof(HtmlUiHost);
            var webField = hostType.GetField("_web", BindingFlags.Instance | BindingFlags.NonPublic);
            var formField = hostType.GetField("_form", BindingFlags.Instance | BindingFlags.NonPublic);
            var web = webField?.GetValue(host) as WebView2;
            var form = formField?.GetValue(host) as Form;

            if (web == null || form == null)
                throw new InvalidOperationException("HtmlUiHost WebView2/form is not available.");

            Action apply = () =>
            {
                if (transparent)
                {
                    const int magenta = 0x00FF00FF;
                    var keyColor = Color.FromArgb(magenta);
                    form.BackColor = keyColor;
                    form.TransparencyKey = keyColor;
                    form.Opacity = 1.0;

                    var controllerField = FindControllerField(web.GetType());
                    var controller = controllerField?.GetValue(web) as CoreWebView2Controller;
                    if (controller == null)
                        throw new InvalidOperationException("WebView2 internal CoreWebView2Controller was not found.");

                    controller.DefaultBackgroundColor = Color.Transparent;
                }
                else
                {
                    form.TransparencyKey = Color.Empty;
                    form.BackColor = Color.Black;
                    form.Opacity = 1.0;

                    var controllerField = FindControllerField(web.GetType());
                    var controller = controllerField?.GetValue(web) as CoreWebView2Controller;
                    if (controller != null)
                        controller.DefaultBackgroundColor = Color.Black;
                }
            };

            if (form.InvokeRequired)
                form.BeginInvoke(apply);
            else
                apply();
        }

        private static FieldInfo FindControllerField(Type webType)
        {
            foreach (var field in webType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (typeof(CoreWebView2Controller).IsAssignableFrom(field.FieldType))
                    return field;

                if (field.Name.IndexOf("corewebview2controller", StringComparison.OrdinalIgnoreCase) >= 0)
                    return field;
            }

            return null;
        }

        private static void OnEndMissionPostfix()
        {
            try
            {
                TacticalMapBootstrap.HtmlUi?.SetVisible(false);
                TacticalMapBootstrap.HtmlUi?.AttachController(null);

                try
                {
                    ConfigureOverlayBackground(HtmlUiService.Host, transparent: false);
                }
                catch { }
            }
            catch { }
            finally
            {
                _logicInstance = null;
                _controller = null;
            }
        }
    }
}
