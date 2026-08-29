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

## N 键 / 输入问题通用排查经验（可复用）

这次问题额外形成一套独立的通用排查顺序。以后遇到任何“某个按键没有效果”的问题，必须优先按证据链排查，不得直接替换输入 API。

### 正确证据链

统一按照下面顺序建立事实：

```text
物理按键
  ↓
输入观察层是否记录
  ↓
Bannerlord InputContext 是否得到按键状态
  ↓
Consumer 热键处理是否执行
  ↓
业务方法是否执行
  ↓
业务状态是否改变
  ↓
Framework/UI 状态是否同步
  ↓
最终视觉/交互结果是否改变
```

只有上一层已经被日志或代码证据证明正常，才能进入下一层排查。

### 经验 1：日志缺少某个键，不等于输入缺少某个键

必须先确认日志系统是否实际追踪该键。

例如：

```text
InputTrace 没有 N
```

首先只能得到：

```text
不知道 N 的日志状态
```

不能直接得到：

```text
N 没有进入输入系统
```

任何输入诊断器都必须先检查“观测集合/过滤条件”，再解释日志缺失。

### 经验 2：不要在证据不足时连续更换输入 API

以下修改不能作为独立的排错证据：

```text
DebugInput.IsKeyPressed
→ Input.IsKeyPressed
→ Input.IsKeyDown
→ GetAsyncKeyState
```

每次换 API 都应该先回答：

```text
原 API 实际返回什么？
新 API 实际返回什么？
两者差异是什么？
```

没有这三个答案时，换 API 只是扩大变量数量，并不能缩小故障范围。

### 经验 3：输入 owner 必须唯一

业务热键应由 Consumer 自己拥有；Framework 的 InputMode、Overlay、WebView2 输入归属则由 Framework 唯一管理。

禁止在已有 Consumer hotkey 之外继续堆叠：

```text
第二个 Harmony hotkey
第三个 Win32 fallback
额外的 Framework 输入 Patch
```

除非已经证明原 owner 本身无法覆盖目标输入场景，而且新路径有明确生命周期和 owner 设计。

### 经验 4：不同层的 Bug 不要直接建立因果关系

例如：

```text
WindowTracker Cross-thread exception
```

和：

```text
N 键没有切换地图
```

可以同时发生，但必须分别建立证据。不能因为两个问题同时出现，就直接把前者认定为后者的根因。

### 经验 5：先验证业务状态，再验证 UI 表现

对于状态型功能，必须把：

```text
后端状态
```

和：

```text
界面视觉结果
```

分开验证。

例如本次最终日志已经证明：

```text
CompactPassive
→ FullInteractive
→ MouseCaptured
```

所以后端状态切换已经成功。即使用户当时主观感觉“地图没切”，下一步也应该检查：

```text
页面布局
CSS class
前端 runtime state
资源部署路径
```

而不是再次修改 N 键。

### 经验 6：运行时资源路径必须和工程部署源一一对应

HtmlUI 类问题必须同时检查：

```text
工程资源源
↓
BUTR _Module
↓
最终 Mod 目录
↓
C# RegisterContentRoot
↓
实际加载页面
```

如果仓库中存在多份：

```text
源码 UI
bin UI
旧 UI
```

就必须先确认哪一份是真正运行时资源，否则会出现：

```text
代码已经修改
但游戏表现完全没变化
```

这种假性“功能修复失败”。

### 经验 7：最终修复必须由完整证据闭环，而不是单条日志

一个输入问题只有在至少能够同时确认：

```text
输入到达
→ 业务处理执行
→ 状态前后值变化
→ 下游状态同步
```

之后才能说“已经修复”。

### 经验 8：修复过程中不得把“理论正确”写成“实机验证通过”

源码逻辑正确只说明：

```text
代码层面可能正确
```

只有真实游戏日志同时证明状态转移和实际表现后，才能记录为：

```text
实机验证通过
```

### 本次经验的核心原则

以后遇到类似问题，优先遵守：

> **先建立观测，再建立因果；先证明是哪一层坏了，再修改那一层。**

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
