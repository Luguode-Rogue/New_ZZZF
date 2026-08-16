using System;
using HarmonyLib;
using New_ZZZF;

namespace New_ZZZF.GUI
{
    /// <summary>
    /// 把现有 CustomSkillScreen 的生命周期接到 HtmlUI。
    /// 原 Gauntlet Screen/VM 不删除，HTML 只是叠加在其上方。
    /// </summary>
    [HarmonyPatch]
    internal static class CustomSkillHtmlUiBridgePatch
    {
        private static CustomSkillScreen CurrentScreen;

        [HarmonyPatch(typeof(CustomSkillScreen), "OnInitialize")]
        [HarmonyPostfix]
        private static void OnInitializePostfix(CustomSkillScreen __instance)
        {
            try
            {
                CurrentScreen = __instance;
                CustomSkillHtmlUi.Instance.TryAttachFromScreen(__instance);
            }
            catch (Exception ex)
            {
                SkillDebug.Log($"[CustomSkill][HtmlUI] attach failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(CustomSkillScreen), "OnFrameTick")]
        [HarmonyPostfix]
        private static void OnFrameTickPostfix(CustomSkillScreen __instance, float dt)
        {
            if (!ReferenceEquals(CurrentScreen, __instance)) return;
            CustomSkillHtmlUi.Instance.Tick(dt);
        }

        [HarmonyPatch(typeof(CustomSkillScreen), "OnFinalize")]
        [HarmonyPrefix]
        private static void OnFinalizePrefix(CustomSkillScreen __instance)
        {
            try
            {
                if (ReferenceEquals(CurrentScreen, __instance))
                    CurrentScreen = null;
                CustomSkillHtmlUi.Instance.Detach(__instance);
            }
            catch (Exception ex)
            {
                SkillDebug.Log($"[CustomSkill][HtmlUI] detach failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
