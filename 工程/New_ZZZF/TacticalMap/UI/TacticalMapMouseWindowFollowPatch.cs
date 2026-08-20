using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BannerlordHtmlUI;
using HarmonyLib;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// Keeps the BannerlordHtmlUI overlay alive while TacticalMap owns mouse input.
    /// The framework's normal window tracker requires Bannerlord to remain foreground,
    /// but a real mouse click on the overlay necessarily changes the foreground HWND.
    /// In MouseCaptured mode we therefore track geometry without hiding the overlay.
    /// </summary>
    [HarmonyPatch]
    internal static class TacticalMapMouseWindowFollowPatch
    {
        private static readonly FieldInfo InputModeField = typeof(HtmlUiHost).GetField("_inputMode", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo RequestedVisibleField = typeof(HtmlUiHost).GetField("_requestedVisible", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FormField = typeof(HtmlUiHost).GetField("_form", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo FollowMethod = AccessTools.Method(typeof(HtmlUiHost), "FollowBannerlordWindow");

        static MethodBase TargetMethod() => FollowMethod;

        static bool Prefix(HtmlUiHost __instance)
        {
            try
            {
                if (__instance == null || InputModeField == null || RequestedVisibleField == null || FormField == null)
                    return true;

                var mode = InputModeField.GetValue(__instance);
                if (!(mode is HtmlUiInputMode inputMode) || inputMode != HtmlUiInputMode.MouseCaptured)
                    return true;

                var requestedVisible = (bool?)RequestedVisibleField.GetValue(__instance) ?? false;
                var form = FormField.GetValue(__instance) as Form;
                if (!requestedVisible || form == null || form.IsDisposed)
                    return false;

                var hwnd = ProcessMainWindowHandle();
                if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !GetWindowRect(hwnd, out var rect)
                    || IsIconic(hwnd) || !IsWindowVisible(hwnd))
                {
                    return false;
                }

                var bounds = new Rectangle(rect.Left, rect.Top,
                    Math.Max(0, rect.Right - rect.Left),
                    Math.Max(0, rect.Bottom - rect.Top));

                if (form.Bounds != bounds)
                    form.Bounds = bounds;

                if (!form.Visible)
                    ShowWindow(form.Handle, SW_SHOWNOACTIVATE);

                SetWindowPos(form.Handle, HWND_TOPMOST,
                    bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                    SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_SHOWWINDOW);

                return false;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Debug("Mouse-captured window follow patch failed: " + ex.GetBaseException().Message);
                return false;
            }
        }

        private static IntPtr ProcessMainWindowHandle()
        {
            try { return System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; }
            catch { return IntPtr.Zero; }
        }

        private const int SW_SHOWNOACTIVATE = 4;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
