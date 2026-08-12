using System;
using TaleWorlds.Core;

namespace New_ZZZF.ActionExplorer
{
    /// <summary>
    /// M6 Action Preview 控制器（轻量适配层）。
    ///
    /// 职责：
    /// 1. 校验 Action ID 合法性（必须是 act_xxx）
    /// 2. 缓存当前预览 Action ID
    /// 3. 输出参考实现里统一的 M6 日志，便于验收
    ///
    /// 设计要点：
    /// - VM 不直接操作播放 / Agent / Camera。
    /// - 实际 3D 渲染由 XML 中的 CharacterTableauWidget
    ///   (Tableau / SceneTextureProvider 路线) 完成，
    ///   VM 通过 PreviewActionId 属性把完整 act_xxx 传给它。
    /// - 因此本类不持有 Scene / Agent / Camera，
    ///   避免把渲染上下文与数据层耦合，
    ///   也避免 M6 引入不可被 Gauntlet 显示的孤立相机。
    /// </summary>
    internal sealed class ActionPreview
    {
        private string _currentActionId;

        public bool IsReady
        {
            get { return true; }
        }

        public string CurrentActionId
        {
            get { return _currentActionId; }
        }

        // =========================================================
        // 校验 Action ID
        // =========================================================

        public bool IsValidActionId(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                M0_Probe.M0Log.Info(
                    "M6_PREVIEW_PLAY_FAILED reason=EMPTY_ID");

                return false;
            }

            if (!actionId.StartsWith(
                    ActionPreviewSettings.ValidIdPrefix,
                    StringComparison.Ordinal))
            {
                M0_Probe.M0Log.Info(
                    "M6_PREVIEW_PLAY_FAILED reason=INVALID_PREFIX id=" +
                    actionId);

                return false;
            }

            return true;
        }

        // =========================================================
        // 选择播放（仅记录并输出日志，真实播放由 Tableau 完成）
        // =========================================================

        public bool PlayAction(string actionId)
        {
            if (!IsValidActionId(actionId))
            {
                return false;
            }

            _currentActionId = actionId;

            M0_Probe.M0Log.Info(
                "M6_PREVIEW_PLAY action=" +
                actionId);

            return true;
        }

        // =========================================================
        // 停止
        // =========================================================

        public void Stop()
        {
            if (_currentActionId == null)
                return;

            _currentActionId = null;

            M0_Probe.M0Log.Info(
                "M6_PREVIEW_STOP");
        }

        // =========================================================
        // 清理
        // =========================================================

        public void Dispose()
        {
            Stop();

            M0_Probe.M0Log.Lifecycle(
                "M6",
                "PREVIEW_DISPOSE");
        }
    }
}
