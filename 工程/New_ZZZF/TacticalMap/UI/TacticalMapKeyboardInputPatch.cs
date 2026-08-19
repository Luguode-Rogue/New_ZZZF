using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.InputSystem;
using BannerlordHtmlUI;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// TacticalMap uses Framework mouse capture with keyboard passthrough.
    /// N therefore continues to be sampled from Bannerlord's game-thread input even while the page is interactive.
    /// </summary>
    internal static class TacticalMapKeyboardInputPatch
    {
        private const string HarmonyId = "New_ZZZF.TacticalMap.KeyboardInput";
        private static Harmony _harmony;
        private static Action<TacticalMapHtmlUi, float> _updateToggleKey;
        private static bool _installed;

        public static void Install()
        {
            if (_installed) return;

            var method = typeof(TacticalMapHtmlUi).GetMethod("UpdateToggleKey", BindingFlags.Instance | BindingFlags.NonPublic);
            var tick = typeof(TacticalMapHtmlUi).GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public);
            if (method == null || tick == null)
                throw new MissingMethodException("TacticalMap keyboard methods were not found.");

            _updateToggleKey = (Action<TacticalMapHtmlUi, float>)method.CreateDelegate(typeof(Action<TacticalMapHtmlUi, float>));
            _harmony = new Harmony(HarmonyId);
            _harmony.Patch(
                tick,
                prefix: new HarmonyMethod(typeof(TacticalMapKeyboardInputPatch), nameof(BeforeTick)),
                postfix: new HarmonyMethod(typeof(TacticalMapKeyboardInputPatch), nameof(AfterTick)));
            _installed = true;
            TacticalMapLog.Info("TacticalMap keyboard passthrough patch installed.");
        }

        public static void Uninstall()
        {
            if (!_installed) return;
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            _updateToggleKey = null;
            _installed = false;
        }

        private static void BeforeTick(TacticalMapHtmlUi __instance, out bool __state)
        {
            __state = __instance != null && __instance.IsInteractive;
        }

        private static void AfterTick(TacticalMapHtmlUi __instance, bool __state, float dt)
        {
            if (!__state || __instance == null || _updateToggleKey == null) return;
            try
            {
                // The original Tick intentionally skips C# N polling while Captured.
                // Keyboard passthrough means Bannerlord still owns keyboard focus, so continue the same game-thread state machine here.
                _updateToggleKey(__instance, dt);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("Interactive keyboard passthrough tick failed.", ex);
            }
        }
    }
}
