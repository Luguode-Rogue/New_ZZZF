namespace New_ZZZF.ActionExplorer
{
    /// <summary>
    /// M6 Action Preview 配置常量。
    ///
    /// 集中管理镜头 / 动作参数，调参时无需改动 VM 与渲染层。
    ///
    /// 注意：M6 的 3D 渲染由内置 CharacterTableauWidget
    /// (Tableau / SceneTextureProvider 路线) 完成，
    /// 因此这里只保留"播放行为"相关常量，
    /// 镜头参数交由 Tableau 内部处理。
    /// </summary>
    internal static class ActionPreviewSettings
    {
        // =====================================================
        // Action
        // =====================================================

        /// <summary>
        /// Action 使用的 Agent Action Channel。
        /// </summary>
        public const int ActionChannel = 1;

        /// <summary>
        /// 动作播放 Blend 时间。
        /// </summary>
        public const float BlendInTime = 0f;

        /// <summary>
        /// 动作开始时间偏移。
        /// </summary>
        public const float StartOffset = -0.2f;

        /// <summary>
        /// 是否循环播放。
        /// M6 第一阶段建议开启，方便观察动作。
        /// </summary>
        public const bool Loop = true;

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
