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
        }

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

                // Core Controller 的 IsVisible 仅表示战场数据更新是否启用。
                // HTML 页面的显示/隐藏/全屏/操作状态完全由 TacticalMapHtmlUi 自己管理。
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

        private static void OnEndMissionPostfix()
        {
            try
            {
                TacticalMapBootstrap.HtmlUi?.SetVisible(false);
                TacticalMapBootstrap.HtmlUi?.AttachController(null);
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
