namespace New_ZZZF.ActionExplorer
{
    /// <summary>
    /// M6 Action Preview 配置常量（收口版）。
    ///
    /// 注意：M6 的 3D 渲染由内置 CharacterTableauWidget
    /// (Tableau / SceneTextureProvider 路线) 完成，
    /// VM 只通过 PreviewActionId 把完整 act_xxx 传给它。
    ///
    /// 因此这里仅保留"校验"相关常量。
    /// 镜头 / 动作通道 / 循环 等参数由 Tableau 内部处理，
    /// 不做播放 / 暂停 / 停止 / 循环 控制，故无对应常量。
    /// </summary>
    internal static class ActionPreviewSettings
    {
        // =====================================================
        // 校验
        // =====================================================

        /// <summary>
        /// 合法 Action ID 前缀。
        /// 当前 Action 数据库均为 act_xxx 形式。
        /// </summary>
        public const string ValidIdPrefix = "act_";
    }
}
