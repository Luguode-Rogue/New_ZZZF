using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// FullInteractive 战术地图需要可见的系统光标。
    ///
    /// 背景：战斗中引擎以相对鼠标模式运行并隐藏指针，进入战术地图交互模式后玩家
    /// "没有鼠标"，无法在大图上选点下达命令（2026-09-07 实机反馈）。
    /// 引擎先例：照片模式经 PhotoModeRequiresMouse 在 MissionScreen.OnFrameTick 中
    /// 调用 SceneLayer.InputRestrictions.SetMouseVisibility(true) 显示光标，但该路径
    /// 仅在 IsPhotoModeEnabled 时生效，且 IsPhotoModeEnabled 会连带改相机逻辑，不可借用。
    ///
    /// 实现：postfix OnFrameTick，在引擎每帧设置之后按需强制覆盖为可见——
    /// 我们后执行故稳定生效，无逐帧闪烁；标志清零后引擎原逻辑立即恢复隐藏。
    /// 点击捕获链路（TacticalMapNativeMouseInterceptor 的 GetAsyncKeyState/GetCursorPos）
    /// 与此互补：光标可见后玩家可瞄准，拦截器负责在无前台窗口状态下捕获点击。
    /// </summary>
    internal static class TacticalMapCursorPatch
    {
        private static readonly object Sync = new object();
        private static Harmony _harmony;
        private static bool _installed;

        private static bool _mouseRequested;

        /// <summary>战术地图交互模式是否需要系统光标（由 TacticalMapHtmlUi 模式切换驱动）。</summary>
        public static bool MouseRequested
        {
            get { return _mouseRequested; }
            set
            {
                if (_mouseRequested == value) return;
                _mouseRequested = value;
                TacticalMapLog.Info("TacticalMap cursor visibility requested: " + value);
            }
        }

        public static void Patch(Harmony harmony)
        {
            if (harmony == null) return;
            lock (Sync)
            {
                if (_installed) return;
                try
                {
                    var onFrameTick = AccessTools.Method(typeof(MissionScreen), "OnFrameTick");
                    if (onFrameTick == null)
                        throw new MissingMethodException(typeof(MissionScreen).FullName, "OnFrameTick");

                    _harmony = harmony;
                    _harmony.Patch(onFrameTick,
                        postfix: new HarmonyMethod(typeof(TacticalMapCursorPatch), nameof(OnFrameTickPostfix)));
                    _installed = true;
                    TacticalMapLog.Info("TacticalMap cursor patch installed.");
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Error("TacticalMap cursor patch install failed.", ex);
                }
            }
        }

        private static void OnFrameTickPostfix(MissionScreen __instance)
        {
            if (!_mouseRequested || __instance == null) return;
            try
            {
                var layer = __instance.SceneLayer;
                if (layer != null && layer.InputRestrictions != null)
                    layer.InputRestrictions.SetMouseVisibility(true);
            }
            catch
            {
                // 光标恢复失败不影响游戏输入；下帧重试。
            }
        }
    }
}
