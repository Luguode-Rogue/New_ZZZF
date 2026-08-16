using System;
using System.Drawing;
using System.Reflection;
using HarmonyLib;
using System.Windows.Forms;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Core;
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
            try { AttachFromInstance(__instance); }
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
            var web = webField?.GetValue(host);
            var form = formField?.GetValue(host) as Form;

            if (web == null || form == null)
                throw new InvalidOperationException("HtmlUiHost WebView2/form is not available.");

            var webType = web.GetType();
            var controllerField = webType.GetField(
                "_coreWebView2Controller",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (controllerField == null)
            {
                throw new InvalidOperationException(
                    "WebView2 private field '_coreWebView2Controller' was not found. Runtime type=" + webType.FullName);
            }

            var controller = controllerField.GetValue(web);
            if (controller == null)
                throw new InvalidOperationException("WebView2 _coreWebView2Controller is null.");

            var controllerType = controller.GetType();
            var backgroundProperty = controllerType.GetProperty(
                "DefaultBackgroundColor",
                BindingFlags.Instance | BindingFlags.Public);

            if (backgroundProperty == null || !backgroundProperty.CanWrite)
                throw new InvalidOperationException(
                    "CoreWebView2Controller.DefaultBackgroundColor was not found on " + controllerType.FullName);

            Action apply = () =>
            {
                var background = transparent
                    ? System.Drawing.Color.Transparent
                    : System.Drawing.Color.Black;
                backgroundProperty.SetValue(controller, background, null);

                if (transparent)
                {
                    var keyColor = System.Drawing.Color.Magenta;
                    form.BackColor = keyColor;
                    form.TransparencyKey = keyColor;
                    form.Opacity = 1.0;
                }
                else
                {
                    form.TransparencyKey = System.Drawing.Color.Empty;
                    form.BackColor = System.Drawing.Color.Black;
                    form.Opacity = 1.0;
                }
            };

            if (form.InvokeRequired)
                form.BeginInvoke(apply);
            else
                apply();
        }

        private static void OnEndMissionPostfix()
        {
            try
            {
                TacticalMapBootstrap.HtmlUi?.SetVisible(false);
                TacticalMapBootstrap.HtmlUi?.AttachController(null);
                try { ConfigureOverlayBackground(HtmlUiService.Host, transparent: false); } catch { }
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
