using System;
using System.Drawing;
using System.Reflection;
using HarmonyLib;
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

            var navigate = AccessTools.Method(typeof(HtmlUiHost), "Navigate");
            if (navigate != null)
            {
                harmony.Patch(
                    navigate,
                    prefix: new HarmonyMethod(typeof(TacticalMapHtmlUiBridgePatch), nameof(HtmlUiNavigatePrefix)));
            }
        }

        /// <summary>
        /// HtmlUI 框架已经 Ready 后，若 MissionLogic 已经创建并默认开启地图，立即补一次打开。
        /// 解决“战斗先开始、WebView 后 Ready”时第一下 N 需要等待的问题。
        /// </summary>
        public static void OnHtmlUiFrameworkReady()
        {
            try
            {
                if (_controller != null && _controller.IsVisible)
                    TacticalMapBootstrap.HtmlUi?.SetVisible(true);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMap][HtmlUI] Framework Ready 同步失败: {ex.GetType().Name}: {ex.Message}"));
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

                // Core controller 的 IsVisible 表示“战场数据更新是否启用”，不是 HTML 页面的显隐状态。
                // HTML 隐藏/全屏/操作状态完全由 TacticalMapHtmlUi 自己管理，避免长按隐藏后被 Bridge 下一帧强制重新打开。
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

            // 优先使用 WebView2 WinForms 控件公开的 CoreWebView2Controller，
            // 不再依赖私有字段 _coreWebView2Controller。旧实现依赖该字段，
            // 在不同 WebView2 控件版本下可能失效，从而导致透明设置根本没有应用。
            object controller = null;
            var publicControllerProperty = web.GetType().GetProperty(
                "CoreWebView2Controller",
                BindingFlags.Instance | BindingFlags.Public);
            if (publicControllerProperty != null)
                controller = publicControllerProperty.GetValue(web, null);

            if (controller == null)
            {
                var privateField = web.GetType().GetField(
                    "_coreWebView2Controller",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (privateField != null)
                    controller = privateField.GetValue(web);
            }

            if (controller == null)
                throw new InvalidOperationException(
                    "WebView2 CoreWebView2Controller could not be resolved. Runtime type=" + web.GetType().FullName);

            var backgroundProperty = controller.GetType().GetProperty(
                "DefaultBackgroundColor",
                BindingFlags.Instance | BindingFlags.Public);
            if (backgroundProperty == null || !backgroundProperty.CanWrite)
                throw new InvalidOperationException(
                    "CoreWebView2Controller.DefaultBackgroundColor was not found on " + controller.GetType().FullName);

            Action apply = () =>
            {
                backgroundProperty.SetValue(
                    controller,
                    transparent ? System.Drawing.Color.Transparent : System.Drawing.Color.Black,
                    null);

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

            if (form.InvokeRequired) form.BeginInvoke(apply);
            else apply();
        }

        private static void OnEndMissionPostfix()
        {
            try
            {
                TacticalMapBootstrap.HtmlUi?.SetVisible(false);
                TacticalMapBootstrap.HtmlUi?.AttachController(null);
                try { ConfigureOverlayBackground(HtmlUiService.Host, false); } catch { }
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
