# TacticalMap 战术地图

> 当前主文档。历史审计、计划和排错记录保留在原目录，并通过本页统一索引。

## 1. 功能定位

RTS 风格战术地图 / 小地图。核心职责包括战场地形可视化、单位与编队追踪、地图坐标转换、地图下令、镜头联动，以及 HtmlUI 展示层。

当前代码位于 `工程/New_ZZZF/TacticalMap/`，包含 Config、Core、Terrain、Tracking、UI 等层。

## 2. 当前状态：HtmlUI clean rebuild

旧 TacticalMap HtmlUI 实现已经整体移除，包括旧页面、HtmlUi 生命周期封装、Bridge/Input/Debug 补丁以及 Mission/Bootstrap 中的旧 UI 状态机耦合。

当前分支暂时保留 TacticalMap 核心后端，包括：

- `TacticalMapController`
- `TerrainCache / TerrainAnalyzer`
- `FormationTracker / AgentTracker`
- `OrderSystem`
- `CameraController / TacticalCameraPatch`

新 HtmlUI 将从干净基线重新接入，不继承旧页面结构、旧生命周期方案或旧输入补丁。

## 3. 当前架构

```text
SubModule
  -> TacticalMapBootstrap
      -> TacticalMapMissionLogic
          -> TacticalMapController
              ├─ TerrainCache / TerrainAnalyzer
              ├─ FormationTracker / AgentTracker
              ├─ OrderSystem
              └─ CameraController / TacticalCameraPatch

新 HtmlUI（待重新接入）
      ↓
独立表现层 / Consumer
      ↓
TacticalMapController 提供稳定地图数据与操作接口
```

UI 表现层不得重新承担 TacticalMap 核心状态、Terrain 采样、Agent 扫描或 Mission 生命周期管理。

## 4. Backend 接口原则

当前 `TacticalMapController` 保留地图操作接口，供新的 UI Adapter 使用：

- 地图 UV -> 世界坐标
- 移动命令
- 朝向命令
- 镜头目标切换
- 玩家位置 / 朝向
- 编队快照
- Agent 快照
- Terrain / Risk 静态地图数据

新的 HtmlUI 不应重新复制这些逻辑。

## 5. Terrain

负责战场边界、地形采样、坐标转换、语义分类和缓存。历史审计中已经确认：完整地形格网不应直接作为战场显示范围；应优先使用软边界，失败后使用场景包围盒，再失败才退回完整地形范围。

## 6. Tracking

`AgentTracker` 负责单位层与密度信息，`FormationTracker` 负责编队快照。动态更新采用节流，不应与 UI 绘制混为一谈。

## 7. Order

地图 UV 命中后转换为世界坐标，再交给 `OrderSystem`；移动、攻击推进、朝向、停止等订单保持在游戏逻辑层处理，UI 只负责输入和展示。

## 8. Camera

镜头联动由 `CameraController` 管理状态，Harmony 相机补丁负责把目标注入原生相机流程。涉及私有字段和版本差异的部分必须保留兜底与明确日志。

## 9. 历史关键经验

### 9.1 战场边界

原实现使用 `nodeDim * nodeSize`，导致地图范围过大、单位显示过小。已形成软边界 -> 场景包围盒 -> 完整地形的兜底策略。

### 9.2 纹理兼容性

曾遇到 Bannerlord 特定版本的字节数组纹理路径生成白纹理问题。当前实现保留逐像素绘制回退，不应假定 Texture API 在所有版本行为一致。

### 9.3 UI 坐标 API

历史代码曾直接依赖 `GlobalPosition` / 特定 Rectangle2D 字段，在不同 Bannerlord 版本出现 `MissingMethodException` / `MissingFieldException`。新的 UI 实现应优先使用版本稳定的几何 API，并将版本敏感点隔离。

### 9.4 HtmlUI 历史方案

旧 HtmlUI 已确认存在生命周期、输入、页面资源路径和工程耦合问题，因此本次不是继续修旧实现，而是完整移除后重新建立边界。

原始需求和历史排错记录继续保留，不以删除旧实现为由删除经验资料。

## 10. 当前重建原则

1. HtmlUI 只负责表现和用户输入适配。
2. `TacticalMapController` 是地图运行时协调层。
3. Terrain / Tracking / Order / Camera 保持职责单一。
4. UI 不直接管理 Mission 生命周期。
5. 所有版本敏感 API 集中隔离。
6. 高频日志默认关闭。
7. 新 HtmlUI 不复用旧 `TacticalMapHtmlUi`、Bridge、Input、Debug 实现。
8. 任何新 Bug 修复都必须进入 [Bug 修复经验库](../历史/BUG_HISTORY.md)。

## 11. 历史资料

- [TacticalMapHtmlUi 功能需求](../../New_ZZZF/TacticalMap/TacticalMapHtmlUi功能需求.md)
- [2026-08-01 战术地图审计文档](../UI开发文档/2026-08-01_战术地图TacticalMap_审计文档.md)
- [2026-08-01 战术地图项目计划/进度](../UI开发文档/2026-08-01_战术地图TacticalMap_项目计划书_进度.md)
- [Bug 修复经验库](../历史/BUG_HISTORY.md)

## 12. 使用规则

本页只维护“现在的设计与状态”。旧实现的失败过程必须保留在历史资料中；新实现出现问题时，应优先查 Bug 经验库，再记录新的失败方案和根因。
