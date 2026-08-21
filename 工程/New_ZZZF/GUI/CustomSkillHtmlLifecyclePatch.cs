using System;
using HarmonyLib;
using BannerlordHtmlUI;

namespace New_ZZZF.GUI
{
    /// <summary>
    /// Keeps CustomSkillHtmlUi's local lifecycle state synchronized with BannerlordHtmlUI's page lifecycle.
    /// Framework-owned closes (ESC, page manager close, consumer scope teardown, etc.) must always clear
    /// the consumer-side _visible / VM / active-state-disable state as well.
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

                // The page manager has already completed the Framework-side close at this point.
                // Calling Close() here is safe: Pages.Close(pageId) sees no current page and becomes a no-op,
                // while the consumer-side VM/active-state state is released exactly once.
                if (string.Equals(ui.CurrentPageId, "New_ZZZF.CustomSkill.customskill.html", StringComparison.OrdinalIgnoreCase))
                {
                    ui.SyncFrameworkClosed();
                    return;
                }

                // If another page transition closed the skill page, the local flag still must be cleared.
                ui.SyncFrameworkClosed();
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkill HtmlUI lifecycle synchronization failed.", ex);
            }
        }
    }
}
