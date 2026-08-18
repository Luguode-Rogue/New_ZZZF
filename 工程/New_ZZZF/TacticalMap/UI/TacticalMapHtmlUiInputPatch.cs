using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HarmonyLib;
using BannerlordHtmlUI;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// TacticalMap/CustomSkill 的鼠标捕获修复：
    /// WebView 必须可接收鼠标，但不能因为 Activate() 抢走 Bannerlord 的前台焦点。
    /// 否则会触发游戏“失焦静音”，并可能因为 WebView 子窗口成为前台窗口而被 Host 隐藏。
    /// </summary>
    internal static class TacticalMapHtmlUiInputPatch
    {
        private static FieldInfo _formField;
        private static FieldInfo _inputModeField;
        private static FieldInfo _requestedVisibleField;

        public static void Patch(Harmony harmony)
        {
            if (harmony == null) return;

            _formField = AccessTools.Field(typeof(HtmlUiHost), "_form");
            _inputModeField = AccessTools.Field(typeof(HtmlUiHost), "_inputMode");
            _requestedVisibleField = AccessTools.Field(typeof(HtmlUiHost), "_requestedVisible");

            var setInputMode = AccessTools.Method(typeof(HtmlUiHost), "SetInputMode");
            if (setInputMode != null)
            {
                harmony.Patch(
                    setInputMode,
                    prefix: new HarmonyMethod(typeof(TacticalMapHtmlUiInputPatch), nameof(SetInputModePrefix)));
            }

            var follow = AccessTools.Method(typeof(HtmlUiHost), "FollowBannerlordWindow");
            if (follow != null)
            {
                harmony.Patch(
                    follow,
                    prefix: new HarmonyMethod(typeof(TacticalMapHtmlUiInputPatch), nameof(FollowBannerlordWindowPrefix)));
            }
        }

        private static bool IsTargetPage()
        {
            try
            {
                var page = HtmlUiService.Pages?.Current;
                if (page == null) return false;
                return string.Equals(page.OwnerId, "New_ZZZF.TacticalMap", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(page.OwnerId, "New_ZZZF.CustomSkill", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool SetInputModePrefix(HtmlUiHost __instance, HtmlUiInputMode mode)
        {
            if (__instance == null || !IsTargetPage() || mode != HtmlUiInputMode.Captured)
                return true;

            try
            {
                if (_inputModeField == null) _inputModeField = AccessTools.Field(typeof(HtmlUiHost), "_inputMode");
                if (_requestedVisibleField == null) _requestedVisibleField = AccessTools.Field(typeof(HtmlUiHost), "_requestedVisible");
                if (_formField == null) _formField = AccessTools.Field(typeof(HtmlUiHost), "_form");

                _inputModeField?.SetValue(__instance, mode);
                _requestedVisibleField?.SetValue(__instance, true);
                __instance.State?.Set("framework.inputMode", mode.ToString());

                var form = _formField?.GetValue(__instance) as Form;
                if (form == null || form.IsDisposed) return false;

                Action apply = () =>
                {
                    try
                    {
                        // 禁止顶层 Overlay 激活；保持 Bannerlord 作为前台窗口。
                        var setPassThrough = form.GetType().GetMethod("SetPassThrough", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        setPassThrough?.Invoke(form, new object[] { false });
                        SetNoActivate(form.Handle, true);

                        if (!form.Visible)
                            ShowWindow(form.Handle, SW_SHOWNOACTIVATE);
                        else
                            SetWindowPos(form.Handle, HWND_TOP, 0, 0, 0, 0,
                                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

                        var gameWindow = Process.GetCurrentProcess().MainWindowHandle;
                        if (gameWindow != IntPtr.Zero && GetForegroundWindow() != gameWindow)
                            SetForegroundWindow(gameWindow);
                    }
                    catch (Exception ex)
                    {
                        HtmlUiLogger.Error("TacticalMap non-activating capture setup failed.", ex);
                    }
                };

                if (form.InvokeRequired) form.BeginInvoke(apply);
                else apply();

                return false;
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("TacticalMap SetInputMode patch failed; falling back to framework behavior.", ex);
                return true;
            }
        }

        private static bool FollowBannerlordWindowPrefix(HtmlUiHost __instance)
        {
            if (__instance == null || !IsTargetPage()) return true;

            try
            {
                var form = _formField?.GetValue(__instance) as Form;
                if (form == null || form.IsDisposed || !form.IsHandleCreated) return true;

                var hwnd = Process.GetCurrentProcess().MainWindowHandle;
                if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !GetWindowRect(hwnd, out RECT rect))
                    return false;

                bool minimized = IsIconic(hwnd);
                bool gameVisible = IsWindowVisible(hwnd);
                bool gameForeground = GetForegroundWindow() == hwnd;
                bool overlayForeground = false;
                var fg = GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    var root = GetAncestor(fg, GA_ROOT);
                    overlayForeground = root == form.Handle || fg == form.Handle;
                }

                bool captured = __instance.InputMode == HtmlUiInputMode.Captured;
                bool accepted = gameForeground || (captured && overlayForeground);
                bool active = !minimized && gameVisible && accepted && __instance.IsVisible;

                int width = Math.Max(0, rect.Right - rect.Left);
                int height = Math.Max(0, rect.Bottom - rect.Top);

                SetNoActivate(form.Handle, captured);
                if (active)
                {
                    form.Bounds = new System.Drawing.Rectangle(rect.Left, rect.Top, width, height);
                    if (!form.Visible) ShowWindow(form.Handle, SW_SHOWNOACTIVATE);
                    SetWindowPos(form.Handle, HWND_TOP, rect.Left, rect.Top, width, height,
                        SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
                else if (form.Visible)
                {
                    form.Hide();
                }

                return false;
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("TacticalMap window tracking patch failed; falling back to framework behavior.", ex);
                return true;
            }
        }

        private static void SetNoActivate(IntPtr hwnd, bool enabled)
        {
            if (hwnd == IntPtr.Zero) return;
            long current = Environment.Is64BitProcess
                ? GetWindowLongPtr64(hwnd, GWL_EXSTYLE).ToInt64()
                : GetWindowLongPtr32(hwnd, GWL_EXSTYLE).ToInt64();
            long next = current | WS_EX_TOOLWINDOW;
            if (enabled) next |= WS_EX_NOACTIVATE;
            else next &= ~WS_EX_NOACTIVATE;
            var value = new IntPtr(next);
            if (Environment.Is64BitProcess)
                SetWindowLongPtr64(hwnd, GWL_EXSTYLE, value);
            else
                SetWindowLongPtr32(hwnd, GWL_EXSTYLE, value);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_NOACTIVATE = 0x08000000L;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const int SW_SHOWNOACTIVATE = 4;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint GA_ROOT = 2;
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    }
}
