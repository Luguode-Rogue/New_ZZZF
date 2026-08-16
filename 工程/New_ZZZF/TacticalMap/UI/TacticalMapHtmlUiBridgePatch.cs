using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.Config;

namespace New_ZZZF.TacticalMap.UI
{
    internal static class TacticalMapHtmlUiBridgePatch
    {
        private static readonly FieldInfo ControllerField = typeof(TacticalMapMissionLogic)
            .GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic);

        private static TacticalMapController _controller;

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

        private static void OnAfterMissionCreatedPostfix(TacticalMapMissionLogic __instance)
        {
            try
            {
                _controller = ControllerField.GetValue(__instance) as TacticalMapController;
                TacticalMapBootstrap.HtmlUi?.AttachController(_controller);
                if (_controller != null && _controller.IsVisible)
                    TacticalMapBootstrap.HtmlUi?.SetVisible(true);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TMap][HtmlUI] Mission attach 失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        private static void OnMissionTickPostfix()
        {
            try
            {
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
                _controller = null;
            }
        }
    }
}
