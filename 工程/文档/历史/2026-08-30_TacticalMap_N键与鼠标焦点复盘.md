# TacticalMap N 键与鼠标焦点复盘（2026-08-30）

## 1. 本次记录目的

这是 2026-08-29 N 键排查的连续复盘。重点记录：

- 之前成功版本为什么后来又表现异常。
- 为什么一次 N 可能发生两次切换。
- 为什么 FullInteractive 点击地图后 N / ESC 会失效。
- Framework `MouseCaptured`、WebView2 焦点、Bannerlord 输入三者不能混为一谈。
- 为什么“恢复曾经有效的 fallback”必须以 owner 去重为前提。

## 2. 实机现象

2026-08-30 00:39 日志出现明显的双重 Toggle：

```text
Native fallback:
CompactPassive -> FullInteractive

随后 managed input:
FullInteractive -> CompactPassive
```

同一时间 Framework 反复：

```text
Passive -> MouseCaptured -> Passive
```

这说明一次 N 被两条路径同时处理，最终状态看起来像“按了但没切”，并伴随鼠标模式快速抖动。

## 3. 为什么之前测试没有暴露

此前的日志中经常出现：

```text
nativeDown=True
bannerlordDown=False
```

此时 native fallback 能补上游戏 InputContext 没有观察到的 N，因此表现为一次切换。

后续出现：

```text
nativeDown=True
bannerlordDown=True
```

并且两个 rising-edge 状态不同步时，同一物理按键就可能进入两条路径，形成双 Toggle。

因此不能把“此前实机正常”解释为 fallback 设计本身没有风险；它只是当时没有命中双触发时序。

## 4. 第一次错误判断的原因

早期 InputTrace 没有追踪 N。日志中没有 N 不能证明 N 没有进入输入系统。

随后排查中过早把：

```text
DebugInput.IsKeyPressed
Input.IsKeyPressed
Input.IsKeyDown
GetAsyncKeyState
```

看成简单的可互换入口，导致修改范围逐步扩大。

真正需要先建立的证据链是：

```text
物理按键
-> InputTrace 是否追踪
-> Bannerlord InputContext
-> Consumer hotkey
-> ToggleInteractive
-> Mode 前后值
-> Framework InputMode
-> 最终 UI / 鼠标结果
```

## 5. 关键提交历史

### 0bf7339

首次加入 `TacticalMapNativeNKeyFallback`，使用 Win32 `GetAsyncKeyState(VK_N)`，解决 WebView 焦点导致的输入缺失场景。

### ebfd56e

修复 native fallback 的 namespace/集成问题。此时已有实机日志证明双向切换可行。

### 814a676d

删除整个 native fallback，理由是“remove duplicate native N hotkey owner”。

这个删除过度简化了问题：它解决了潜在双 owner，却同时删除了 WebView 焦点后仍能处理快捷键的重要能力。

### 后续恢复

之后重新恢复 native fallback，并继续尝试处理 ESC 与鼠标焦点。但必须注意：恢复 fallback 不等于解决问题；必须保证它不会与 managed input 在同一物理按键上重复 Toggle。

## 6. FullInteractive 点击后 N / ESC 失效

2026-08-30 01:19 的实机日志确认：

第一次进入 FullInteractive 时：

```text
foreground = game
InputMode = MouseCaptured
```

点击地图后：

```text
foreground = overlay
InputMode = MouseCaptured
```

此后：

- N 不再进入 `GAME_INPUT`。
- ESC 进入 WebView accelerator：`vk=0x1B`。
- TacticalMap 页面原本没有把 ESC 定义成关闭当前 interactive state 的 Consumer 行为。

因此“全屏地图无法退出”的直接问题不是 `ToggleInteractive()` 状态机，而是 WebView 获得键盘焦点后，退出快捷键没有可靠的全局 owner。

## 7. Framework Cross-thread 问题的独立性

启动时一直存在：

```text
HtmlUiWindowTracker.SyncNow()
Control.get_Handle()
Cross-thread operation not valid
```

这是真实的 Framework WinForms 线程问题，但它不能单独证明 N 键问题由它引起。

以后必须分别记录：

```text
Framework window/thread bug
```

和：

```text
TacticalMap hotkey/focus bug
```

只有有直接因果证据时才能关联。

## 8. 已确认的正确设计目标

TacticalMap 只有两个业务状态：

```text
CompactPassive <-> FullInteractive
```

输入职责必须明确：

```text
TacticalMap
-> 拥有 N/ESC 业务语义

Bannerlord/Framework
-> 提供游戏输入状态与 UI 输入模式

WebView2
-> 提供地图鼠标交互
```

不能让同一个按键同时由多个 owner 无条件执行 Toggle。

## 9. 当前代码状态

当前仓库已经恢复 native hotkey fallback，并增加了 ESC 的 FullInteractive 退出路径；同时保留 Framework 的窗口线程修复。

但是本文件建立时，以上恢复方案尚未由用户完成新的完整实机回归，因此不得写成“最终验证通过”。

下一次验证必须至少确认：

```text
1. 被动小图按一次 N -> FullInteractive
2. 全屏点击地图后按一次 N -> CompactPassive
3. 全屏点击地图后按一次 ESC -> CompactPassive
4. 一次 N 不出现两次 mode change
5. MouseCaptured 不发生无故快速抖动
6. Framework 启动不再出现 Cross-thread exception
```

## 10. 永久经验

### 输入问题先做证据，不先换 API

没有日志不等于没有输入。先确认诊断器是否追踪目标键。

### 先做回归定位

当用户明确指出“之前正常、现在异常”时，先锁定最后一个正常 commit 与第一个异常 commit 的 diff。

### fallback 必须是条件性兜底

native fallback 的价值在于覆盖 WebView/overlay 焦点导致的游戏输入缺失；它不能与正常 managed path 无条件并行执行。

### UI 焦点与业务快捷键必须分层

WebView 可以拥有鼠标交互焦点，但不能因为获得焦点就让 TacticalMap 的退出快捷键语义消失。

### 实机验证优先

只有真实日志同时证明输入、状态转移、Framework InputMode 和最终交互结果，才能标记为已修复。
