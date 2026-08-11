using TaleWorlds.ScreenSystem;

namespace New_ZZZF.ActionExplorer
{
    public static class ActionExplorerLauncher
    {
        private static ActionExplorerScreen _screen;

        public static bool IsOpen
        {
            get { return _screen != null; }
        }

        public static void TryOpen()
        {
            if (_screen != null)
                return;

            _screen = new ActionExplorerScreen();
            ScreenManager.PushScreen(_screen);
        }

        public static void Toggle()
        {
            if (_screen != null)
            {
                _screen.CloseScreen();
                return;
            }

            TryOpen();
        }

        public static void NotifyClosed()
        {
            _screen = null;
        }
    }
}
