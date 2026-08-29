# Bug 修复经验库

> 这是全工程 Bug 排错经验的统一入口。**本文件不替代原始 Bug 文档。** 原始记录继续保留，确保以后遇到类似问题时可以直接复用完整排查路径。

## 经验保留标准

每个问题尽量保留完整链路：

`现象 -> 触发条件 -> 日志/堆栈 -> 初始假设 -> 排查过程 -> 失败方案 -> 失败原因 -> 根因 -> 修复 -> 验证 -> 版本/API条件`

不得只保留“最终改了哪几行代码”。失败方案尤其重要，因为它能避免以后重复走已经证伪的路线。

## 目前已确认的经验类别

### UI / Gauntlet / HtmlUI

- Scroll / Clip 裁剪问题
- 技能槽位点击与跳转
- UI 闪退与大地图暂停机制
- UI 层级、点击命中和透明覆盖层
- 不同 Bannerlord 版本的 `GlobalPosition` / Rectangle API 差异
- HtmlUI 生命周期、焦点、输入模式、窗口显隐与 WebView2 初始化

原始来源：`../Bug修复记录/`、`../UI开发文档/`、`../工作日志/`。

### 存档 / 数据持久化

- 词缀/物品系统存档读档丢失
- Debug.Print / 日志替换引发的行为变化
- 世界状态持久化相关问题

### API / 版本迁移

- MissingFieldException / MissingMethodException
- UI 几何 API 类型变化
- 私有 API / 反射字段版本变化
- TaleWorlds 类型行为变化

### TacticalMap

重点保留：

- 战场边界取值错误导致地图比例异常
- Terrain / Texture API 兼容问题
- 地图绘制性能问题
- Agent / Formation 扫描过重
- 点击坐标转换与指令下发
- 镜头 Harmony / 私有字段兼容
- HtmlUI 与原 Gauntlet 生命周期、焦点和输入冲突

原始核心审计：[2026-08-01 TacticalMap 审计](../UI开发文档/2026-08-01_战术地图TacticalMap_审计文档.md)。

## 原始记录索引

### Bug 修复记录

完整原始记录位于 `../Bug修复记录/`。包括历史 API 迁移、技能 UI、存档、点击、暂停、日志等问题，以及该目录中的其他历史 Bug 文档。

### 工作日志中的 Bug / 踩坑记录

部分问题是在工作日志中形成的完整排错记录，而不是独立 Bug 文件。

**这些原始文件不得删除。** 后续整理只允许建立新的分类索引、将重复结论提升到主文档，不能以摘要替代原始排错过程。

## TacticalMap N 键切换多轮排查复盘（2026-08-29）

### 问题

TacticalMap 需要从 `CompactPassive` 与 `FullInteractive` 两态之间通过 N 键切换。实际测试初期表现为“按 N 没有切换”。

### 触发条件

战斗 Mission 中 TacticalMap 已成功打开并处于 `CompactPassive`，按 N 无明显界面切换。

### 日志/证据

早期日志能够证明：

- TacticalMap Mission 正常初始化。
- Terrain Bake、NavMesh 构建和 HtmlUI 页面打开均成功。
- `CompactPassive` 页面成功进入 `inputMode=Passive`。
- M、Shift、鼠标等其它按键可以被输入追踪记录。

但当时 `HtmlUiInputTraceLogger` 的追踪集合没有包含 `InputKey.N`，因此“日志中没有 N”不能证明 N 没进入输入系统。这是本次排查中最重要的错误证据。

后续实机日志终于直接证明：

```text
TacticalMap native N key rising edge observed: ... modeBefore=CompactPassive
TacticalMap native N fallback toggled mode=FullInteractive

TacticalMap native N key rising edge observed: ... modeBefore=FullInteractive
TacticalMap native N fallback toggled mode=CompactPassive
```

同时 Framework 日志同步出现：

```text
Input mode applied: MouseCaptured
Input mode applied: Passive
```

并且 Mission heartbeat 分别报告：

```text
Mode=FullInteractive
Mode=CompactPassive
```

最终确认：**N 键两态切换实际上已经成功。**

### 初始假设

1. `MissionLogic` 没有收到 N。
2. `Input.IsKeyPressed(InputKey.N)` 不是正确输入入口。
3. `SubModule.OnApplicationTick()` 没有执行 N 处理。
4. `BannerlordHtmlUI` 的 InputMode 抢走了 N。
5. Framework 的 `MouseCaptured` 状态覆盖导致切换失败。

这些假设中只有部分对应真实独立问题，没有任何一个能够解释早期全部现象。

### 排查过程

#### 排查 1：从 `DebugInput.IsKeyPressed(N)` 改到 `Input.IsKeyPressed(N)`

**失败原因：** 没有先确认输入证据，也没有确认原实现是否已经工作；只是更换输入 API。`MissionLogic.DebugInput` 本身是 Bannerlord 为 MissionBehavior 提供的正式 `IInputContext`，不能简单视为错误的“调试输入”。

#### 排查 2：改成 `Input.IsKeyDown` + rising-edge

**失败原因：** 仍然是在猜输入入口，没有建立 N 的实际观测；所以没有解决“为什么 N 没触发”的证据缺口。

#### 排查 3：增加 `TacticalMapNKeyFallback` Harmony / Win32 `GetAsyncKeyState`

**失败原因：** 这是第二套输入路径，违反 Consumer hotkey 单一 owner 的架构原则；而且 Harmony patch 的安装时机依赖 `OnNewGameCreated/OnGameLoaded`，并不能保证所有 Mission 入口都已经安装。该方案不应作为长期架构。

#### 排查 4：把 `MouseCaptured` / WindowTracker 跨线程问题作为 N 键根因

**失败原因：** `HtmlUiWindowTracker.SyncNow()` 的 WinForms 跨线程异常确实是真实 Framework Bug，但它不能证明 N 键切换失败。后续实机日志证明 `Passive <-> MouseCaptured` 已经可以正常切换，因此这两个问题必须分开。

#### 排查 5：根据“输入追踪没有 N”认定 Bannerlord 没收到 N

**失败原因：** InputTrace 原先没有追踪 N，这个日志观察点本身不完整。后续增加 N 追踪和最终实机日志才补齐了证据链。

### 最终根因

本次问题之所以经过多轮才做好，不是因为 `ToggleInteractive()` 的状态机复杂，而是因为**最初缺少正确的观测层**：

1. N 没有进入 InputTrace 的追踪集合，导致第一批日志无法证明 N 的真实输入状态。
2. 我们过早把 `DebugInput`、`Input.IsKeyPressed`、`Input.IsKeyDown` 当成等价入口并反复迁移。
3. 后续又增加了 Harmony/Win32 第二输入路径，进一步扩大了状态 owner，而不是先确认原始事实。
4. Framework 的跨线程异常与输入切换问题同时存在，导致排查时容易错误建立因果关系。
5. 最终只有在日志直接记录 `native N rising edge -> modeBefore -> modeAfter`，并同时看到 Framework `Passive <-> MouseCaptured` 后，才能确认切换链路实际已经正常。

### 最终修复 / 当前状态

当前业务规则收敛为两态：

```text
CompactPassive
↕ N
FullInteractive
```

Framework 只负责对应的 InputMode 与 Overlay 行为；TacticalMap 自己负责 Consumer hotkey 与业务状态。

### 验证

2026-08-29 实机日志已经完成多次双向切换验证：

```text
CompactPassive -> FullInteractive
FullInteractive -> CompactPassive
CompactPassive -> FullInteractive
FullInteractive -> CompactPassive
```

因此 N 键切换功能已得到实机验证。

### 适用版本

当前 `New_ZZZF/master` 与 `BannerlordHtmlUI/dev` 的现有实现。

### 关键词

`TacticalMap`、`N key`、`InputTrace`、`DebugInput`、`Input.IsKeyDown`、`Input.IsKeyPressed`、`Harmony fallback`、`GetAsyncKeyState`、`HtmlUiInputMode`、`MouseCaptured`、`Consumer hotkey`

## 快速复用格式

以后修复新问题时，统一增加：

```text
问题：
触发条件：
现象：
日志/堆栈：
初始假设：
排查 1：
排查 2：
失败方案：
失败原因：
最终根因：
最终修复：
验证：
适用版本：
关键词：
```
