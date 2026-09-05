using System;
using System.Runtime.InteropServices;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Diagnostics;
using New_ZZZF.TacticalMap.UI;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// Game-tick ESC watcher for TacticalMap.
    ///
    /// This was originally a Harmony patch over SubModule.OnApplicationTick, but patch
    /// application proved unreliable in practice (it silently never ran, which disabled the ESC
    /// exit). It is now invoked directly from SubModule.OnApplicationTick and only owns ESC:
    /// the N toggle stays with SubModule's own Input.IsKeyDown edge detector, because the
    /// keyboard always remains with the game while the map owns the mouse.
    /// </summary>
    internal static class TacticalMapNativeHotkeyFallback
    {
        private const int VkEscape = 0x1B;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        private static bool _escapeWasDown;

        public static void Tick(bool customVisible)
        {
            try
            {
                if (Mission.Current == null)
                {
                    _escapeWasDown = false;
                    return;
                }
                if (customVisible) return;

                bool nativeEscapeDown = (GetAsyncKeyState(VkEscape) & 0x8000) != 0;
                bool nativeEscapeRising = nativeEscapeDown && !_escapeWasDown;
                _escapeWasDown = nativeEscapeDown;

                if (!nativeEscapeRising) return;

                TacticalMapHtmlUi map = TacticalMapHtmlUi.Instance;
                if (map.IsVisible && map.IsInteractive)
                {
                    TacticalMapLog.Info("TacticalMap ESC watcher: leaving interactive mode.");
                    map.SetInteractive(false);
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("TacticalMap ESC watcher failed.", ex);
            }
        }
    }
}
