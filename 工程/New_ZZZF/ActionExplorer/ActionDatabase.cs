using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.ActionExplorer
{
    /// <summary>
    /// M2 —— 真正的 Action 数据库。
    ///
    /// 职责（仅扫描，不碰 UI / 不创建 Agent / 不创建 Scene / 不播放动画）：
    ///     1. 找到 ActionIndexCache 所有静态 Action 字段
    ///     2. 提取 act_ 开头的 Action ID（保留 act_ 前缀，如 "act_idle"）
    ///     3. 按名字去重
    ///     4. 按名字排序
    ///     5. 转成 ActionInfo
    ///     6. 通过静态 Actions 属性提供给 VM（M4 才替换数据源）
    ///
    /// 为什么直接读 ActionIndexCache 而不是 XML：
    ///     这样能覆盖「原版动作 + DLC + 其他 Mod 运行时注册的动作」。
    ///
    /// 后续阶段分工（本文件只做 M2）：
    ///     M3：对每个 Action 计算 IsPlayable（当前角色动作集）
    ///     M5：把「全局存在性」与「当前角色可用性」分离，切换角色只重算可用性
    /// </summary>
    public static class ActionDatabase
    {
        private static readonly List<ActionInfo> _actions =
            new List<ActionInfo>();

        private static bool _initialized;

        /// <summary>扫描得到的全部 Action（只读）。首次访问自动触发扫描。</summary>
        public static IReadOnlyList<ActionInfo> Actions
        {
            get
            {
                EnsureInitialized();
                return _actions;
            }
        }

        public static int Count
        {
            get
            {
                EnsureInitialized();
                return _actions.Count;
            }
        }

        /// <summary>
        /// 线程安全的一次性初始化。VM 或任何调用方直接读 Actions 即可，
        /// 不必显式调用本方法（属性 getter 已包含）。
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;

            M0_Probe.M0Log.Lifecycle(
                "M2",
                "ACTION_DATABASE_INIT");

            ScanActionIndexCache();

            M0_Probe.M0Log.Lifecycle(
                "M2",
                "ACTION_DATABASE_READY count=" + _actions.Count);
        }

        private static void ScanActionIndexCache()
        {
            _actions.Clear();

            Type actionType = typeof(ActionIndexCache);

            // 1. 拿到全部公有静态字段（ActionIndexCache 的每个动作都是一个静态字段）。
            FieldInfo[] fields = actionType.GetFields(
                BindingFlags.Public |
                BindingFlags.Static);

            // 2. 提取 act_ 开头的字段名，按名字去重。
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (FieldInfo field in fields)
            {
                try
                {
                    if (field.FieldType != typeof(ActionIndexCache))
                        continue;

                    string fieldName = field.Name;

                    // 只取 act_ 开头的动作 ID（保留前缀，如 "act_idle"）。
                    if (!fieldName.StartsWith(
                            "act_",
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    // 跳过 act_none 这类空/无效动作。
                    object value = field.GetValue(null);
                    if (!(value is ActionIndexCache))
                        continue;

                    var action = (ActionIndexCache)value;
                    if (action == ActionIndexCache.act_none)
                        continue;

                    // 3. 按名字去重（不同静态字段可能指向同一动作）。
                    if (!seen.Add(fieldName))
                        continue;

                    int index = -1;
                    try { index = action.Index; }
                    catch { /* 某些平台 Index 可能不可读，忽略 */ }

                    _actions.Add(
                        new ActionInfo(
                            fieldName,   // Id 保留 act_ 前缀
                            fieldName,   // Name 当前与 Id 相同，M2 不展示短名
                            false,       // IsPlayable：M3 才计算
                            "",          // Category：M8 才分类
                            index));     // Index：M5 复用
                }
                catch (Exception ex)
                {
                    M0_Probe.M0Log.Warn(
                        "ACTION_SCAN_FIELD_FAILED field=" +
                        field.Name,
                        ex);
                }
            }

            // 4. 按名字排序（不区分大小写，稳定）。
            _actions.Sort(
                delegate(ActionInfo a, ActionInfo b)
                {
                    return string.Compare(
                        a.Id,
                        b.Id,
                        StringComparison.OrdinalIgnoreCase);
                });

            M0_Probe.M0Log.Info(
                "ACTION_SCAN_COMPLETE count=" + _actions.Count);

            // 预览前 20 条，便于不打开 UI 也能确认扫描正常。
            int previewCount = Math.Min(_actions.Count, 20);
            for (int i = 0; i < previewCount; i++)
            {
                ActionInfo entry = _actions[i];
                M0_Probe.M0Log.Info(
                    "ACTION[" + i + "] " + entry.Id +
                    " index=" + entry.Index);
            }
        }
    }
}
