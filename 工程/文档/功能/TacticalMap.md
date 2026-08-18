# TacticalMap 战术地图

> 当前主文档。历史审计、计划和排错记录保留在原目录，并通过本页统一索引。

## 1. 功能定位

RTS 风格战术地图 / 小地图。核心职责包括战场地形可视化、单位与编队追踪、地图坐标转换、地图下令、镜头联动，以及新一代 HtmlUI 展示层。

当前代码位于 `工程/New_ZZZF/TacticalMap/`，包含 Config、Core、Terrain、Tracking、UI 等层；当前重制分支已经存在 `TacticalMapHtmlUi` UI 接入。

## 2. 当前架构

```text
SubModule
  -> TacticalMapBootstrap
      -> TacticalMapMissionLogic
          -> TacticalMapController
              ├─ TerrainCache / TerrainAnalyzer
              ├─ FormationTracker / AgentTracker
              ├─ OrderSystem
              ├─ CameraController / TacticalCameraPatch
              └─ UI: Gauntlet / TacticalMapHtmlUi
```

### Terrain

负责战场边界、地形采样、坐标转换、语义分类和缓存。历史审计中已经确认：完整地形格网不应直接作为战场显示范围；应优先使用软边界，失败后使用场景包围盒，再失败才退回完整地形范围。

### Tracking

`AgentTracker` 负责单位层与密度信息，`FormationTracker` 负责编队快照。动态更新采用节流，不应与每帧 UI 绘制混为一谈。

### Order

地图 UV 命中后转换为世界坐标，再交给 `OrderSystem`；移动、攻击推进、朝向、停止等订单保持在游戏逻辑层处理，UI 只负责输入和展示。

### Camera

镜头联动由 `CameraController` 管理状态，Harmony 相机补丁负责把目标注入原生相机流程。涉及私有字段和版本差异的部分必须保留兜底与明确日志。

### UI

当前重制方向为 HtmlUI；原有 Gauntlet 层与 ViewModel 可以继续存在作为兼容/过渡，但 UI 表现层不得承担 TacticalMap 核心状态。

## 3. 已知关键历史经验

### 3.1 战场边界

原实现使用 `nodeDim * nodeSize`，导致地图范围过大、单位显示过小。已形成两级/三级兜底边界策略：软边界 -> 场景包围盒 -> 完整地形。

### 3.2 纹理兼容性

曾遇到 Bannerlord 特定版本的字节数组纹理路径生成白纹理问题。当前实现保留逐像素绘制回退，不应假定 Texture API 在所有版本行为一致。

### 3.3 UI 坐标 API

历史代码曾直接依赖 `GlobalPosition` / 特定 Rectangle2D 字段，在不同 Bannerlord 版本出现 `MissingMethodException` / `MissingFieldException`。现行实现应优先使用版本稳定的几何 API（例如 AreaRect bounding box 等），并将版本敏感点隔离。

### 3.4 性能

地形烘焙是一次性工作；动态追踪需要节流；UI 绘制每帧进行。不要把 MissionTick 的动态扫描、地形采样和 UI 重绘混成一个无节制循环。

## 4. 当前重制原则

1. HtmlUI 是表现层，不直接读取或修改 Mission 内部状态。
2. TacticalMapController 是运行时协调层。
3. Terrain / Tracking / Order / Camera 模块保持职责单一。
4. 所有版本敏感 API 集中隔离。
5. 高频日志默认关闭；只有排查问题时临时提高日志级别。
6. 任何新 Bug 修复都必须进入 [Bug 修复经验库](../历史/BUG_HISTORY.md)。

## 5. 历史资料

- [2026-08-01 战术地图审计文档](../UI开发文档/2026-08-01_战术地图TacticalMap_审计文档.md)
- [2026-08-01 战术地图项目计划/进度](../UI开发文档/2026-08-01_战术地图TacticalMap_项目计划书_进度.md)
- [UI 开发文档索引](../UI开发文档/README.md)
- [Bug 修复经验库](../历史/BUG_HISTORY.md)

## 6. 使用规则

本页只维护“现在的设计与状态”。历史排错细节不要覆盖掉；修改当前结论时应同步更新相关历史记录的状态，而不是删除旧方案。
