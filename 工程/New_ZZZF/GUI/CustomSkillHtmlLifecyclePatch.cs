using System;
using HarmonyLib;
using BannerlordHtmlUI;

namespace New_ZZZF.GUI
{
    /// <summary>
    /// Keeps CustomSkillHtmlUi's local lifecycle state synchronized with BannerlordHtmlUI's page lifecycle.
    /// Framework-owned closes (ESC, page manager close, consumer scope teardown, etc.) must also clear
    /// the consumer-side visible/VM/active-state state.
    /// </summary>
    [HarmonyPatch(typeof(HtmlUiPageManager), nameof(HtmlUiPageManager.CloseCurrent))]
    internal static class CustomSkillHtmlLifecyclePatch
    {
        private static bool _installed;
        private static readonly object Sync = new object();

        public static void Install()
        {
            lock (Sync)
            {
                if (_installed) return;
                _installed = true;
            }
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                var ui = CustomSkillHtmlUi.Instance;
                if (!ui.IsVisible) return;

                // Framework has already completed the page close. Close() now only releases
                // the consumer-side VM and active-state-disable request; the page close itself is a no-op.
                HtmlUiLogger.Info("CustomSkill lifecycle sync: framework page closed; releasing consumer state.");
                ui.Close();
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkill HtmlUI lifecycle synchronization failed.", ex);
            }
        }
    }
}
