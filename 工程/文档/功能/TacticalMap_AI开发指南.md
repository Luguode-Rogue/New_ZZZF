# TacticalMap AI 开发指南

> 本文是给 AI/代码代理使用的 TacticalMap 开发约束与知识库。目标不是描述某一次临时实现，而是让后续 AI 在修改 TacticalMap 前先理解职责边界、资源部署、输入焦点、地图算法、验证规则，以及本轮对话已经踩过的坑。

## 1. 开发前必须先确认

1. 先读取本文件。
2. 再读取 `工程/文档/功能/TacticalMap.md`。
3. 遇到历史 Bug、输入、HtmlUI、焦点、部署问题时，再读取 `工程/文档/历史/BUG_HISTORY.md` 以及相关日期记录。
4. 涉及 HtmlUI Framework 时，同时读取 `BannerlordHtmlUI` 仓库对应版本的开发文档；不要根据旧版本记忆推断当前 API。
5. 如果用户说“之前正常、现在异常”，第一步做 commit-level regression，而不是直接重构。

当前代码与最近一次实机验证优先于旧文档、旧计划和 AI 自己的假设。

## 2. 当前功能边界

TacticalMap 当前只保留两个运行状态：

```text
CompactPassive
    ↕ N
FullInteractive
```

含义：

- `CompactPassive`：被动小图，只观察，不抢鼠标操作。
- `FullInteractive`：全屏地图，可操作地图、下达地图指令。

不要重新引入过去的“短按/长按”设计，也不要自行增加第三个 UI 状态。

ESC 的职责是退出 FullInteractive；不要把 ESC 变成关闭整个页面的通用行为，除非用户明确要求。

## 3. 架构与职责

推荐的职责链：

```text
SubModule
  ↓
TacticalMapBootstrap
  ↓
TacticalMapMissionLogic
  ↓
TacticalMapController
  ├─ Terrain / TerrainCache / TerrainAnalyzer
  ├─ NavMeshMap
  ├─ SceneObstacleMap
  ├─ FormationTracker / AgentTracker
  ├─ OrderSystem
  └─ CameraController / Camera Patch
       ↓
TacticalMapHtmlUi
       ↓
HtmlUI Framework / WebView2
       ↓
HTML / CSS / JS
```

### 3.1 游戏逻辑层

`TacticalMapMissionLogic`、`TacticalMapController` 负责真实战场状态、刷新、订单和状态机。

### 3.2 数据层

`TerrainCache` / `TerrainAnalyzer` 负责地形数据与缓存。

`NavMeshMap` 应优先表达“实际可通行区域”，因为导航网格是游戏 AI 路径系统的真实依据。

`SceneObstacleMap` 作为障碍物视觉/几何辅助，不能在没有证据时替代 NavMesh，也不能通过粗糙 AABB 直接把大型障碍覆盖成巨型色块。

### 3.3 Tracking

`FormationTracker` / `AgentTracker` 只负责单位和编队快照。动态扫描应节流，不应把高成本 Mission 扫描塞进每帧 UI 绘制。

### 3.4 Order

地图点击：

```text
屏幕/Canvas UV
  ↓
地图 UV
  ↓
世界坐标
  ↓
OrderSystem
  ↓
Bannerlord 游戏订单
```

UI 不应该直接操作 Formation 内部状态来“模拟”订单。

### 3.5 Camera

镜头联动与 Harmony 私有字段访问必须保持隔离。版本敏感 API 单独封装，不要把反射字段直接散落在 UI 代码中。

## 4. 地图算法原则

### 4.1 NavMesh 优先

TacticalMap 的核心问题是：

> “实际部队哪里能走、必须从哪里绕？”

因此地图可通行性应优先来自 Bannerlord 自己的导航体系，而不是自己猜。

推荐职责：

```text
NavMesh
→ 可走 / 不可走

Height / Terrain
→ 高低关系、地形视觉

Scene geometry / obstacle scan
→ 辅助表达建筑、栅栏等具体障碍
```

如果能枚举导航面，优先把 NavMesh face 投影到地图；如果只能调用路径查询，则路径本身也应优先复用游戏 AI Pathfinder。

### 4.2 不要继续扩大“粗障碍”算法

本轮曾出现：一个栅栏在地图上比人还大、房屋/围栏无法正确表达等问题。根本原因之一是空间离散与实体包围盒的粒度不匹配。

后续不要通过继续放大/缩小统一网格单元去“修好所有障碍”。应优先区分：

- 导航可通行区域。
- 障碍边界。
- 视觉轮廓。
- 单位显示。

四者不是同一个数据层。

### 4.3 战场边界

不要直接使用完整地形采样范围作为视觉战场范围。历史上已经出现过 `nodeDim * nodeSize` 导致地图过大、单位过小的问题。

当前边界策略应保持：

```text
软边界
  ↓失败
场景包围盒
  ↓失败
完整地形范围
```

## 5. HtmlUI 资源与部署

### 5.1 唯一资源源

TacticalMap HTML 资源统一放在：

```text
工程/New_ZZZF/_Module/UI/TacticalMap/
```

典型内容：

```text
index.html
tactical-map.css
tactical-map.js
```

不要再新增：

```text
工程/New_ZZZF/TacticalMap/HtmlUI/
工程/New_ZZZF/TacticalMap/UI/TacticalMap/
bin/Win64_Shipping_Client/UI/TacticalMap/
```

等第二、第三份副本。

### 5.2 BUTR 部署机制

本工程基于 BUTR 模板。`_Module` 是最终 Mod 资源的主要源目录。新增 HtmlUI 时，优先利用 `_Module` + `Bannerlord.BuildResources`，不要自行增加“复制到 bin”的专用 Target。

### 5.3 C# ContentRoot

TacticalMap C# 注册页面时，运行时查找路径必须与 `_Module` 最终输出保持一致：

```text
_Mod​ule/UI/TacticalMap
        ↓ BuildResources
Modules/New_ZZZF/UI/TacticalMap
        ↓
RegisterContentRoot / RegisterPage
```

如果改资源目录，必须同时检查 C# `Register` 的 ContentRoot；只移动 HTML 不修改加载路径是无效改动。

### 5.4 不要从“代码树”推断运行时资源

必须检查实际加载链：

```text
工程文件
→ BuildResources
→ 最终 Mod 文件
→ ContentRoot
→ WebView2 source
```

代码改了但游戏无变化时，优先检查这条链，而不是立即怀疑 JS 或 C# 逻辑。

## 6. 输入与焦点：最高优先级规则

### 6.1 Input owner 必须清晰

Consumer（TacticalMap）拥有自己的业务快捷键；Framework 拥有 InputMode、Overlay、WebView2 生命周期和窗口行为。

禁止在没有证据的情况下叠加：

```text
SubModule hotkey
+ Harmony hotkey
+ GetAsyncKeyState
+ WebView keydown
+ Framework key patch
```

### 6.2 输入排查必须按证据链

统一顺序：

```text
物理按键
  ↓
InputTrace 是否真正追踪该键
  ↓
Bannerlord InputContext 是否得到状态
  ↓
Consumer hotkey 是否执行
  ↓
业务方法是否执行
  ↓
业务状态是否改变
  ↓
Framework InputMode 是否同步
  ↓
最终界面是否变化
```

不要跨层跳跃。

### 6.3 “没有日志”不能直接等于“没有输入”

必须先检查 InputTrace 的 `tracedKeys` / 过滤器是否包含目标键。

本轮 N 键就是典型案例：早期日志没有 N，但当时的追踪集合本身没有 N，因此不能证明 N 没有进入输入系统。

### 6.4 Rising edge

对于按键切换状态，必须使用稳定的 rising-edge 语义：

```text
down && !wasDown
```

否则按住一个键可能在多个 Tick 连续触发切换。

### 6.5 WebView 获得焦点后的按键

FullInteractive 点击地图后，WebView2 可能取得键盘焦点。如果 N / ESC 必须在该状态下仍可退出，就必须明确设计“HTML 焦点”和“全局快捷键 owner”的关系。

禁止仅靠 `SetForegroundWindow` 的经验性调用判断问题已解决。Windows 前台窗口、overlay focus、WebView accelerator 和 Bannerlord InputContext 必须分别验证。

### 6.6 不要把 Framework 异常直接当成 Consumer 根因

例如：

```text
HtmlUiWindowTracker Cross-thread exception
```

和：

```text
TacticalMap N 无法切换
```

可以同时出现，但必须分别证明因果关系。

## 7. UI 状态与鼠标模式

当前语义应保持：

```text
CompactPassive
  → Framework Passive
  → overlay pass-through
  → 游戏正常输入
```

```text
FullInteractive
  → Framework MouseCaptured
  → 地图可点击/拖拽
  → 不应永久夺走退出快捷键能力
```

切换后的日志至少应该能对应：

```text
业务状态：CompactPassive / FullInteractive
Framework：Passive / MouseCaptured
Overlay：pass-through true / false
```

如果三个层次不一致，不要先改 JS 样式。

## 8. 性能规则

### 一次性工作

```text
Terrain bake
NavMesh build
初始 SceneObstacleMap build
```

允许较高成本，但必须避免 Mission 启动永久卡顿。

### 动态工作

```text
Formation/Agent tracking
位置/朝向更新
UI runtime payload
```

必须节流。不要让 100+ 编队/数百 Agent 的数据扫描每帧完整重建。

### UI 工作

前端绘制可以高频，但应尽量复用已计算的数据；不要把游戏世界查询塞入 JS。

## 9. 日志规则

默认只保留必要日志。高频调试必须使用临时独立日志，尤其是：

```text
方向 / 坐标
输入 / 焦点
导航 / 路径
```

不要把临时每 200ms 一条的 trace 混进长期主日志。

临时日志结束后可以删除；如果形成通用经验，则将“经验”写入 Bug 文档，而不是把海量原始 trace 永久塞进主日志。

## 10. Bug 排查规则

### 10.1 之前正常、现在异常

第一优先级：

```text
最后一个已知正常 commit
        ↓
第一个异常 commit
        ↓
逐文件 diff
        ↓
定位回归
```

不要在没有做回归定位之前重新设计整个系统。

### 10.2 分层验证

对于状态问题，至少分别证明：

```text
Input
Business
Framework
UI
```

不能用 UI 没变化反推业务没有执行。

### 10.3 不要把“理论修复”写成“已修复”

只有真实游戏日志证明状态迁移，并且用户实际看到目标行为，才能标记“实机验证通过”。

### 10.4 失败方案要留下

失败方案不是噪声。未来 AI 需要知道哪些方向已经被实机证伪。

## 11. 本轮对话形成的关键经验

### 11.1 N 键多轮误判

早期因为 InputTrace 没有追踪 N，错误认为 N 没有进入 Bannerlord 输入系统；之后连续切换 API，又增加了第二套 native fallback，最终扩大了问题范围。

正确做法：先确认观测器，再确认输入，再决定是否增加 fallback。

### 11.2 Native fallback 曾经有实际价值

当 WebView2 取得键盘焦点后，单靠游戏 InputContext 可能无法处理 N/ESC。过去的 `GetAsyncKeyState` fallback 曾经在实机中成功让地图进入/退出 FullInteractive。

但是 fallback 不能与主输入路径无条件同时执行，否则会形成：

```text
一次 N
→ Native toggle
→ Managed toggle
→ 两次切换回原状态
```

典型症状就是：

```text
CompactPassive → FullInteractive → CompactPassive
```

以及：

```text
Passive ↔ MouseCaptured
```

快速抖动。

### 11.3 一次按键双触发的定位方法

看到类似：

```text
Native fallback toggled mode=FullInteractive
```

紧接着：

```text
TacticalMap toggle key pressed: N
TacticalMap mode after toggle: CompactPassive
```

就应该立即判定为两个 owner 同时处理，不应继续修改地图状态机。

### 11.4 FullInteractive 无法退出的定位方法

如果第一次进入 FullInteractive 成功，但点击地图后 N/ESC 都失效，应检查：

```text
foreground game HWND
foreground overlay HWND
WebView accelerator
InputMode
Consumer hotkey owner
```

不能只看 `ToggleInteractive()`。

### 11.5 WindowTracker 跨线程不是万能根因

`HtmlUiWindowTracker.SyncNow()` 的 WinForms 跨线程异常是独立 Framework Bug。它需要独立修复和独立验证，不应因为它与 TacticalMap 同时出现就直接认定是 TacticalMap 输入问题。

### 11.6 UI 副本会制造“假回归”

仓库里存在多个 HTML 副本时：

```text
改了源码
但运行时仍加载另一份
```

AI 可能会错误地继续修改业务代码。

所以 HtmlUI 的第一件事不是改 JS，而是确认唯一资源源和实际 ContentRoot。

## 12. 建议的开发流程

```text
1. 读取本指南
2. 读取 TacticalMap.md
3. 查询当前分支 / 当前 commit
4. 找最近实机验证版本
5. 如果是回归问题，先做 diff
6. 明确数据来源与职责层
7. 修改最小范围代码
8. 添加必要的临时日志
9. 构建
10. 检查最终 Mod 输出资源
11. 实机测试
12. 根据日志确认状态链
13. 用户未明确要求前，不写新的 Bug 历史
14. 用户要求记录时，再更新 Bug 文档
15. 直接提交修复
```

## 13. 当前测试最低标准

### N 键

```text
被动小图
→ 按一次 N
→ FullInteractive

点击地图
→ 再按一次 N
→ CompactPassive
```

### ESC

```text
FullInteractive
→ 点击地图
→ ESC
→ CompactPassive
```

### 鼠标

```text
CompactPassive：鼠标正常操作游戏
FullInteractive：地图可以点击/拖拽
退出 FullInteractive 后：鼠标控制恢复正常
```

### 资源

必须确认：

```text
最终 Mod/UI/TacticalMap/index.html
最终 Mod/UI/TacticalMap/tactical-map.css
最终 Mod/UI/TacticalMap/tactical-map.js
```

并确认 WebView2 `source changed` 指向 TacticalMap 页面。

## 14. AI 修改禁止事项

未经用户明确要求，不要：

1. 创建新的 GitHub 分支。
2. 引入多个同名 UI 资源副本。
3. 重新引入 N 长按/短按规则。
4. 为同一个业务热键增加多个 owner。
5. 把 Framework 的线程问题直接归因给 TacticalMap。
6. 用单条日志证明“没有输入”或“已经修复”。
7. 删除历史失败方案。
8. 在没有回归 diff 的情况下大范围重构已经验证正常的功能。
9. 把未经用户实机验证的状态写成“验证通过”。

## 15. 参考文档

- `工程/文档/功能/TacticalMap.md`
- `工程/文档/历史/BUG_HISTORY.md`
- `工程/文档/DOCUMENT_MAP.md`
- `工程/文档/UI开发文档/README.md`
- `工程/文档/历史/README.md`
