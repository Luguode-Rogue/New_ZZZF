using BannerlordHtmlUI;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>Thin ZZZF adapter over the Framework's public mouse-only input mode.</summary>
    internal static class HtmlUiMouseCapture
    {
        public static void Capture()
        {
            HtmlUiService.SetInputMode(HtmlUiInputMode.MouseCaptured);
        }
    }
}
