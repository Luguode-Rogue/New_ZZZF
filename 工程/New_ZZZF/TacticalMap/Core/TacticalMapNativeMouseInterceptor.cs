using System;
using System.Runtime.InteropServices;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// Game-thread mouse interceptor for the interactive tactical map.
    ///
    /// Chromium only dispatches mouse input while its window is active, and the game keeps
    /// stealing the foreground back, so clicks into the HTML overlay are unreliable. This path
    /// bypasses WebView2 entirely: while FullInteractive is active it polls the physical mouse
    /// with GetAsyncKeyState/GetCursorPos on the game thread and converts clicks inside the map
    /// canvas into the same order calls the HTML path uses.
    ///
    /// The framework's input blocker keeps the game from reacting to those clicks (it hides the
    /// mouse keys from Input.IsKeyDown), so no double ordering occurs. GetAsyncKeyState is
    /// deliberately used instead of Input.IsKeyDown precisely because the blocker does not and
    /// must not touch it.
    /// </summary>
    internal static class TacticalMapNativeMouseInterceptor
    {
        private const int VkLeftButton = 0x01;
        private const int VkRightButton = 0x02;
        private const int VkMiddleButton = 0x04;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        // Map canvas rectangle in page client coordinates, reported by the page itself
        // (layout changes with mode and window size, so the page keeps it refreshed).
        private static float _rectX, _rectY, _rectW, _rectH;
        private static bool _leftWasDown, _rightWasDown, _middleWasDown;
        private static bool _rectLogged;

        public static void UpdateCanvasRect(float x, float y, float w, float h, float dpr)
        {
            var scale = dpr <= 0f ? 1f : dpr;
            // The page reports CSS pixels; GetCursorPos works in physical pixels. Convert once here.
            _rectX = x * scale; _rectY = y * scale; _rectW = w * scale; _rectH = h * scale;
            if (!_rectLogged)
            {
                _rectLogged = true;
                TacticalMapLog.Info("Canvas rect received: css=(" + x + "," + y + " " + w + "x" + h + ") dpr=" + scale +
                    " physical=(" + _rectX + "," + _rectY + " " + _rectW + "x" + _rectH + ")");
            }
        }

        /// <summary>
        /// overlayBounds: current on-screen bounds of the HTML overlay. For a borderless form the
        /// client area equals the window bounds, so page client coordinates offset directly.
        /// Pass a null controller (or empty bounds) to reset the edge state.
        /// </summary>
        public static void Tick(TacticalMapController controller, System.Drawing.Rectangle overlayBounds)
        {
            if (controller == null || _rectW <= 1f || _rectH <= 1f || overlayBounds.Width <= 0 || overlayBounds.Height <= 0)
            {
                ResetEdges();
                return;
            }

            if (!GetCursorPos(out var cursor))
            {
                ResetEdges();
                return;
            }

            var canvasLeft = overlayBounds.Left + (int)_rectX;
            var canvasTop = overlayBounds.Top + (int)_rectY;
            var canvasWidth = (int)_rectW;
            var canvasHeight = (int)_rectH;

            bool inside = cursor.X >= canvasLeft && cursor.X < canvasLeft + canvasWidth
                       && cursor.Y >= canvasTop && cursor.Y < canvasTop + canvasHeight;

            bool leftDown = (GetAsyncKeyState(VkLeftButton) & 0x8000) != 0;
            bool rightDown = (GetAsyncKeyState(VkRightButton) & 0x8000) != 0;
            bool middleDown = (GetAsyncKeyState(VkMiddleButton) & 0x8000) != 0;

            // Edge-triggered diagnostics: a click that lands outside the canvas shows the real
            // cursor position against the rect the page reported, so any scaling/offset drift
            // is visible directly in the log.
            if (leftDown && !_leftWasDown && !inside)
            {
                TacticalMapLog.Info("Native mouse click outside canvas: cursor=(" + cursor.X + "," + cursor.Y +
                    ") canvasScreen=(" + canvasLeft + "," + canvasTop + " " + canvasWidth + "x" + canvasHeight + ")");
            }

            if (inside)
            {
                float u = (cursor.X - canvasLeft) / (float)canvasWidth;
                float v = (cursor.Y - canvasTop) / (float)canvasHeight;

                if (leftDown && !_leftWasDown)
                {
                    TacticalMapLog.Info("Native mouse interceptor: move click u=" + u.ToString("0.000") + " v=" + v.ToString("0.000"));
                    controller.HandleHtmlMoveClick(u, v);
                }
                if (rightDown && !_rightWasDown)
                {
                    TacticalMapLog.Info("Native mouse interceptor: face click u=" + u.ToString("0.000") + " v=" + v.ToString("0.000"));
                    controller.HandleHtmlFaceClick(u, v);
                }
                if (middleDown && !_middleWasDown)
                {
                    TacticalMapLog.Info("Native mouse interceptor: camera click u=" + u.ToString("0.000") + " v=" + v.ToString("0.000"));
                    controller.HandleHtmlCameraClick(u, v);
                }
            }

            _leftWasDown = leftDown;
            _rightWasDown = rightDown;
            _middleWasDown = middleDown;
        }

        private static void ResetEdges()
        {
            _leftWasDown = false;
            _rightWasDown = false;
            _middleWasDown = false;
        }
    }
}
