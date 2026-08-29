using System;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using New_ZZZF.GUI;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// Native N-key fallback for TacticalMap.
    /// </summary>
    [HarmonyPatch(typeof(New_ZZZF.SubModule), "OnApplicationTick")]
    internal static class TacticalMapNativeNKeyFallback
    {
        private const int VkN = 0x4E;
        private static bool _wasDown;
        private static FieldInfo _managedEdgeField;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [HarmonyPrefix]
        private static void Prefix(New_ZZZF.SubModule __instance)
        {
            try
            {
                bool missionActive = Mission.Current != null;
                bool customVisible = CustomSkillHtmlUi.Instance.IsVisible;
                bool nativeDown = missionActive && (GetAsyncKeyState(VkN) & 0x8000) != 0;
                bool nativeRising = nativeDown && !_wasDown;
                bool bannerlordDown = false;
                try { bannerlordDown = missionActive && TaleWorlds.InputSystem.Input.IsKeyDown(InputKey.N); }
                catch { }

                _wasDown = nativeDown;
                if (!nativeRising || !missionActive || customVisible)
                    return;

                TacticalMapLog.Info(
                    "TacticalMap native N key rising edge observed: nativeDown=" + nativeDown +
                    " bannerlordDown=" + bannerlordDown +
                    " customVisible=" + customVisible +
                    " modeBefore=" + TacticalMapHtmlUi.Instance.Mode);

                SetManagedNDown(__instance, true);
                TacticalMapHtmlUi.Instance.ToggleInteractive();
                TacticalMapLog.Info("TacticalMap native N fallback toggled mode=" + TacticalMapHtmlUi.Instance.Mode);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("TacticalMap native N fallback failed.", ex);
            }
        }

        private static void SetManagedNDown(New_ZZZF.SubModule instance, bool value)
        {
            if (instance == null) return;
            try
            {
                if (_managedEdgeField == null)
                    _managedEdgeField = typeof(New_ZZZF.SubModule).GetField("_tacticalMapToggleKeyWasDown", BindingFlags.Instance | BindingFlags.NonPublic);
                _managedEdgeField?.SetValue(instance, value);
            }
            catch { }
        }
    }
}