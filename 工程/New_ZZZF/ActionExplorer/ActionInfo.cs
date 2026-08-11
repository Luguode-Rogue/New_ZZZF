namespace New_ZZZF.ActionExplorer
{
    /// <summary>
    /// Action Explorer 中显示的一个动作条目（M2 数据层）。
    ///
    /// M2（当前阶段）：
    ///     Id   = ActionIndexCache 静态字段名，如 "act_idle"（保留 act_ 前缀）
    ///     Name = 显示名称（当前与 Id 相同，UI 可后续改短名）
    ///     IsPlayable = 当前角色是否可播放（M3 才真正计算，M2 默认 false）
    ///     Category   = 动作分类（M8 才做分类，M2 默认空）
    ///     Index      = ActionIndexCache 内部数值索引（便于 M5 复用，不用于排序）
    ///
    /// 设计原则：M2 只负责"扫描 + 收集 + 去重 + 排序"，
    /// 可播放性 / 分类属于后续阶段，这里只预留字段，不改变扫描行为。
    /// </summary>
    public class ActionInfo
    {
        public string Id { get; private set; }

        public string Name { get; private set; }

        /// <summary>M3 填充：当前预览角色的动作集中是否真有对应动画。</summary>
        public bool IsPlayable { get; private set; }

        /// <summary>M8 填充：按命名规则的分类，如 idle / walk / attack。</summary>
        public string Category { get; private set; }

        /// <summary>ActionIndexCache 数值索引，M5 计算可用性时复用。</summary>
        public int Index { get; private set; }

        public ActionInfo(
            string id,
            string name)
            : this(id, name, false, "", -1)
        {
        }

        public ActionInfo(
            string id,
            string name,
            bool isPlayable,
            string category,
            int index)
        {
            Id = id;
            Name = name;
            IsPlayable = isPlayable;
            Category = category ?? "";
            Index = index;
        }
    }
}
