using System;
using TaleWorlds.Core;

namespace New_ZZZF.ActionExplorer
{
    /// <summary>
    /// M6 Action Preview 控制器（轻量适配层，收口版）。
    ///
    /// 收口后的 M6 职责很干净：
    ///   用户点击左侧 Action -> VM 设 PreviewActionId ->
    ///   XML 的 CharacterTableauWidget.CustomAnimation 绑定变化 ->
    ///   内置 CharacterTableauTextureProvider 立即执行该 Action。
    ///
    /// 本类只做：
    /// 1. 校验 Action ID 合法性（必须是 act_xxx）
    /// 2. 缓存当前预览 Action ID（供日志/调试）
    /// 3. 输出统一 M6 日志，便于验收
    ///
    /// 明确不做：播放/暂停/停止/循环 等控制接口。
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
        // 记录待播放 Action（仅记录 + 日志，
        // 真实播放由 Tableau 绑定驱动，无"播放"控制概念）
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
        // 生命周期清理（仅解除引用，非"停止"控制）
        // UI 关闭时 CharacterTableauWidget 随面板自动销毁，
        // 模型纹理自然释放，这里只做日志与引用置空。
        // =========================================================

        public void Dispose()
        {
            _currentActionId = null;

            M0_Probe.M0Log.Lifecycle(
                "M6",
                "PREVIEW_DISPOSE");
        }
    }
}
