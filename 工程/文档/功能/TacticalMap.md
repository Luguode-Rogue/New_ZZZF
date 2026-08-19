# TacticalMap 战术地图

> 当前主文档。历史审计、计划和排错记录保留在原目录，并通过本页统一索引。

## 1. 功能定位

RTS 风格战术地图 / 小地图。核心职责包括战场地形可视化、单位与编队追踪、地图坐标转换、地图下令、镜头联动，以及 HtmlUI 展示层。

当前代码位于 `工程/New_ZZZF/TacticalMap/`，包含 Config、Core、Terrain、Tracking、UI 等层。

## 2. 当前状态：HtmlUI clean rebuild

旧 TacticalMap HtmlUI 实现已经整体移除，包括旧页面、旧生命周期封装、Bridge/Input/Debug 补丁以及 Mission/Bootstrap 中的旧 UI 状态机耦合。

当前分支使用新的 BannerlordHtmlUI Consumer。UI 由 `TacticalMapHtmlUi` 管理，地图后端仍由 `TacticalMapController` 提供。

## 3. HtmlUI 正确接入方式（必须遵守）

这是 TacticalMap 接入 BannerlordHtmlUI 时已经实测确认的规则。

### 3.1 Page 必须保存 `RegisterPage()` 返回值

错误：

```csharp
_scope.RegisterPage(new HtmlUiPage("tacticalmap", "index.html")
{
    ContentRootId = rootId
});
HtmlUiService.Pages.Open("tacticalmap");
```

正确：

```csharp
_pageId = _scope.RegisterPage(new HtmlUiPage("tacticalmap", "index.html")
{
    ContentRootId = rootId,
    DefaultInputMode = HtmlUiInputMode.Passive
});

HtmlUiService.Pages.Open(_pageId);
```

原因：`HtmlUiConsumerScope.RegisterPage()` 会自动给 Page 增加 Owner 前缀，例如：

```text
tacticalmap
→ New_ZZZF.TacticalMap.tacticalmap
```

Framework 日志中的 `Page registered` 应该看到完整 scoped id。后续 `Open / Close` 必须使用 `RegisterPage()` 返回的真实 id。

### 3.2 State / Command / Request 必须通过 Scope

正确：

```csharp
_scope.SetState("map", value);
_scope.SetState("static", value);
_scope.RegisterCommand("mapClick", handler);
_scope.RegisterRequest("getMapData", handler);
```

前端对应使用 Consumer Scope：

```javascript
const scope = game.scope('New_ZZZF.TacticalMap');
scope.state.subscribe('map', handler);
scope.state.subscribe('static', handler);
scope.call('mapClick', payload);
```

不要在 Consumer 中混用裸 `HtmlUiService.State.Set("tacticalmap", ...)`、裸页面 id 或裸 Command 名称。

### 3.3 被动显示不能调用 `ReleaseInput()`

BannerlordHtmlUI 的 API 语义是：

```text
Show()          显示 UI，但不抢游戏输入
CaptureInput()  显示 UI，并进入 HTML 交互输入模式
ReleaseInput()  隐藏 UI，并释放输入
Hide()          隐藏 UI
```

因此 TacticalMap：

```text
被动模式 → Show()
交互模式 → CaptureInput()
隐藏      → Hide() / Close()
```

曾经把 `ReleaseInput()` 当成“退出交互但保持显示”，会直接导致页面不可见。这条经验已纳入当前实现。

### 3.4 页面显示链

当前正确链路：

```text
Framework Ready
    ↓
TacticalMapHtmlUi.Register()
    ↓
CreateScope("New_ZZZF.TacticalMap")
    ↓
RegisterContentRoot()
    ↓
RegisterPage() → 保存返回的 _pageId
    ↓
Mission 创建 Controller
    ↓
AttachController()
    ↓
SetUiState(CompactPassive)
    ↓
Pages.Open(_pageId)
    ↓
Show()
```

出现以下日志时，说明页面没有真正使用 Consumer Scope 的真实 Page ID：

```text
Page registered: New_ZZZF.TacticalMap.tacticalmap
Page open failed: page not registered: tacticalmap
```

这是明确的 Page ID 使用错误，不是 WebView2 初始化问题。

## 4. 当前架构

```text
SubModule
  -> TacticalMapBootstrap
      -> TacticalMapMissionLogic
          -> TacticalMapController
              ├─ TerrainCache / TerrainAnalyzer
              ├─ FormationTracker / AgentTracker
              ├─ OrderSystem
              └─ CameraController / TacticalCameraPatch

TacticalMapHtmlUi
      ↓
BannerlordHtmlUI ConsumerScope
      ↓
HTML / JavaScript
```

UI 表现层不得重新承担 TacticalMap 核心状态、Terrain 采样、Agent 扫描或 Mission 生命周期管理。

## 5. 当前 UI 状态

```text
Hidden
CompactPassive
CompactInteractive
FullPassive
FullInteractive
```

当前默认进入战场为 `CompactPassive`。

`N` 键负责显示/隐藏 TacticalMap。

交互模式下：

- 左键：移动命令
- 中键：镜头目标
- 右键：朝向命令
- ESC：退出交互

全屏切换保持当前交互/被动状态。

## 6. 地图数据发布

TacticalMap 的静态地图数据由 `TerrainCache` 烘焙：

- `TerrainBaseRGBA`
- `RiskRGBA`
- `WorldW / WorldH`
- `OriginX / OriginY`

当前 HtmlUI 在 Consumer State 中发布：

```text
New_ZZZF.TacticalMap.static
```

动态态势发布：

```text
New_ZZZF.TacticalMap.map
```

其中包括：

- 玩家位置 / 朝向
- 镜头目标
- Formation 快照
- Agent 快照
- Agent LOD 距离
- 当前 UI 状态

前端收到 `static` 后在 Canvas 本地绘制地图底图；收到 `map` 后只更新动态态势层。

## 7. Backend 接口原则

当前 `TacticalMapController` 保留地图操作接口，供 UI Adapter 使用：

- 地图 UV -> 世界坐标
- 移动命令
- 朝向命令
- 镜头目标切换
- 玩家位置 / 朝向
- 编队快照
- Agent 快照
- Terrain / Risk 静态地图数据

新的 HtmlUI 不应重新复制这些逻辑。

## 8. Terrain

负责战场边界、地形采样、坐标转换、语义分类和缓存。历史审计中已经确认：完整地形格网不应直接作为战场显示范围；应优先使用软边界，失败后使用场景包围盒，再失败才退回完整地形范围。

## 9. Tracking

`AgentTracker` 负责单位层与密度信息，`FormationTracker` 负责编队快照。动态更新采用节流，不应与 UI 绘制混为一谈。

## 10. Order

地图 UV 命中后转换为世界坐标，再交给 `OrderSystem`；移动、朝向等订单保持在游戏逻辑层处理，UI 只负责输入和展示。

## 11. Camera

镜头联动由 `CameraController` 管理状态，Harmony 相机补丁负责把目标注入原生相机流程。涉及私有字段和版本差异的部分必须保留兜底与明确日志。

## 12. 历史关键经验

### 12.1 战场边界

原实现使用 `nodeDim * nodeSize`，导致地图范围过大、单位显示过小。已形成软边界 -> 场景包围盒 -> 完整地形的兜底策略。

### 12.2 纹理兼容性

曾遇到 Bannerlord 特定版本的字节数组纹理路径生成白纹理问题。当前实现保留逐像素绘制回退，不应假定 Texture API 在所有版本行为一致。

### 12.3 UI 坐标 API

历史代码曾直接依赖 `GlobalPosition` / 特定 Rectangle2D 字段，在不同 Bannerlord 版本出现 `MissingMethodException` / `MissingFieldException`。新的 UI 实现应优先使用版本稳定的几何 API，并将版本敏感点隔离。

### 12.4 HtmlUI Page Scope

`HtmlUiConsumerScope` 会为 Page、Command、Request、State、ContentRoot 自动加 Owner 前缀。Consumer 必须保存 `RegisterPage()` 的返回值，并通过 Scope 管理自己的 State/Command/Request。

### 12.5 HtmlUI 被动显示

`ReleaseInput()` 的语义是“隐藏并释放输入”，不能用于“保持被动显示”。被动 TacticalMap 必须使用 `Show()`。

### 12.6 HtmlUI 历史方案

旧 HtmlUI 已确认存在生命周期、输入、页面资源路径和工程耦合问题，因此本次不是继续修旧实现，而是完整移除后重新建立边界。

原始需求和历史排错记录继续保留，不以删除旧实现为由删除经验资料。

## 13. 当前重建原则

1. HtmlUI 只负责表现和用户输入适配。
2. `TacticalMapController` 是地图运行时协调层。
3. Terrain / Tracking / Order / Camera 保持职责单一。
4. UI 不直接管理 Mission 生命周期。
5. 所有版本敏感 API 集中隔离。
6. 高频日志默认关闭。
7. 新 HtmlUI 不复用旧 `TacticalMapHtmlUi`、Bridge、Input、Debug 实现。
8. ConsumerScope 的 Page / State / Command / Request 规则必须遵守 Framework 官方模式。
9. 任何新 Bug 修复都必须进入 [Bug 修复经验库](../历史/BUG_HISTORY.md)。

## 14. 历史资料

- [TacticalMapHtmlUi 功能需求](../../New_ZZZF/TacticalMap/TacticalMapHtmlUi功能需求.md)
- [TacticalMap 数据展示方案](../../New_ZZZF/TacticalMap/TacticalMapHtmlUi数据展示方案.md)
- [2026-08-01 战术地图审计文档](../UI开发文档/2026-08-01_战术地图TacticalMap_审计文档.md)
- [2026-08-01 战术地图项目计划/进度](../UI开发文档/2026-08-01_战术地图TacticalMap_项目计划书_进度.md)
- [Bug 修复经验库](../历史/BUG_HISTORY.md)

## 15. 使用规则

本页只维护“现在的设计与状态”。旧实现的失败过程必须保留在历史资料中；新实现出现问题时，应优先查 Bug 经验库，再记录新的失败方案和根因。
