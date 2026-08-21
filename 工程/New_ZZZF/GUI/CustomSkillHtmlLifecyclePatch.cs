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
        private static bool _syncing;
        private static readonly object Sync = new object();

        [HarmonyPostfix]
        private static void Postfix()
        {
            lock (Sync)
            {
                if (_syncing) return;
                _syncing = true;
            }

            try
            {
                var ui = CustomSkillHtmlUi.Instance;
                if (!ui.IsVisible) return;

                // Framework has already completed the page close. Close() now releases only the
                // consumer-side VM and active-state-disable request. The recursive page-close call
                // becomes a no-op because PageManager.Current is already null; _syncing prevents the
                // Postfix from re-entering this synchronization block.
                HtmlUiLogger.Info("CustomSkill lifecycle sync: framework page closed; releasing consumer state.");
                ui.Close();
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkill HtmlUI lifecycle synchronization failed.", ex);
            }
            finally
            {
                lock (Sync) _syncing = false;
            }
        }
    }
}
