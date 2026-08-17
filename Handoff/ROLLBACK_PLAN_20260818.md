# Main 回滚说明（2026-08-18）

本次调整目标：

- 撤回 2026-08-16 起合入 `main` 的 TacticalMap / CustomSkill HtmlUI 改动。
- 保留 `2667164dc8240a8e2e2553c34ab2d393e5d27072` 所代表的“从主 Mod 移除 ActionExplorer”结果。
- `main` 恢复到 HtmlUI 改造开始前的 ActionExplorer M6 基线 `032ae2273f9795f799057c12b2509f8d65e840e1`，再只执行 ActionExplorer 移除。
- 当前 `main` 头部原始状态已先归档至 `archive/main-htmlui-20260818`。

归档分支：`archive/main-htmlui-20260818`
回滚目标基线：`032ae2273f9795f799057c12b2509f8d65e840e1`
保留语义：ActionExplorer 从主 Mod 移除。
