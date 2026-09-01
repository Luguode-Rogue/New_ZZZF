using System;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using New_ZZZF.GUI;
using New_ZZZF.TacticalMap.Diagnostics;
using New_ZZZF.TacticalMap.UI;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// Native hotkey fallback for TacticalMap.
    /// Runs from the Bannerlord game tick so N/ESC remain available even when
    /// the HTML overlay currently owns keyboard focus.
    /// </summary>
    [HarmonyPatch(typeof(New_ZZZF.SubModule), "OnApplicationTick")]
    internal static class TacticalMapNativeHotkeyFallback
    {
        private const int VkN = 0x4E;
        private const int VkEscape = 0x1B;

        private static bool _nWasDown;
        private static bool _escapeWasDown;
        private static FieldInfo _managedEdgeField;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [HarmonyPrefix]
        private static void Prefix(New_ZZZF.SubModule __instance)
        {
            try
            {
                bool missionActive = Mission.Current != null;
                if (!missionActive)
                {
                    _nWasDown = false;
                    _escapeWasDown = false;
                    return;
                }

                bool customVisible = false;
                try { customVisible = CustomSkillHtmlUi.Instance.IsVisible; }
                catch { }

                bool nativeNDown = (GetAsyncKeyState(VkN) & 0x8000) != 0;
                bool nativeNRising = nativeNDown && !_nWasDown;
                _nWasDown = nativeNDown;

                if (!customVisible && nativeNRising)
                {
                    TacticalMapHtmlUi map = TacticalMapHtmlUi.Instance;
                    TacticalMapLog.Info(
                        "TacticalMap native N key rising edge observed: nativeDown=" + nativeNDown +
                        " modeBefore=" + map.Mode);

                    // Keep SubModule's normal N edge detector from handling the same key again.
                    SetManagedNDown(__instance, true);
                    map.ToggleInteractive();

                    TacticalMapLog.Info("TacticalMap native N fallback toggled mode=" + map.Mode);
                }

                bool nativeEscapeDown = (GetAsyncKeyState(VkEscape) & 0x8000) != 0;
                bool nativeEscapeRising = nativeEscapeDown && !_escapeWasDown;
                _escapeWasDown = nativeEscapeDown;

                if (!customVisible && nativeEscapeRising)
                {
                    TacticalMapHtmlUi map = TacticalMapHtmlUi.Instance;
                    if (map.IsVisible && map.IsInteractive)
                    {
                        TacticalMapLog.Info("TacticalMap native ESC fallback: leaving FullInteractive.");
                        map.SetInteractive(false);
                    }
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("TacticalMap native hotkey fallback failed.", ex);
            }
        }

        private static void SetManagedNDown(New_ZZZF.SubModule instance, bool value)
        {
            if (instance == null) return;
            try
            {
                if (_managedEdgeField == null)
                    _managedEdgeField = typeof(New_ZZZF.SubModule).GetField(
                        "_tacticalMapToggleKeyWasDown",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                _managedEdgeField?.SetValue(instance, value);
            }
            catch { }
        }
    }
}
