# TacticalMap HtmlUI 重制分支说明

分支：`feature/tacticalmap-htmlui-redesign`

## 目的

在不删除、不替换现有 TacticalMap Gauntlet UI 的前提下，引入 BannerlordHtmlUI 版本的新 UI，并作为并行 A/B 版本用于实际框架验收。

## 当前策略

- 旧版 Gauntlet UI：保留，不修改原有显示链。
- 新版 HtmlUI：显示在屏幕右上角。
- `N` 仍由原 TacticalMap MissionLogic 控制，旧 UI 与新 UI 同步显示/隐藏。
- 地形烘焙、风险分析、编队追踪、单位密度、OrderSystem、CameraController 等现有游戏逻辑继续作为唯一数据/命令来源。
- HtmlUI 只负责展示、用户交互和 Bridge 适配，不复制 TacticalMap 核心逻辑。

## 新 UI 当前范围

1. 战场地形高度底图。
2. 风险层（崖/水/林）。
3. 编队标记：玩家/友军/敌军、人数、朝向。
4. 玩家标记：青环+黄心。
5. 镜头目标：橙色菱形。
6. 移动 / 攻击移动 / 朝向三种地图指令。
7. 镜头联动开关。
8. 输入捕获 / 释放。
9. HtmlUI 的 Request / Command / State / Input API 实际接入。
10. 左上旧 UI 与右上新 UI 并行显示。

## 依赖

新 UI 使用 `BannerlordHtmlUI` v0.44 的公共 Consumer API，因此 `SubModule.xml` 已声明 `BannerlordHtmlUI` 为前置依赖。

## 资源部署

`工程/New_ZZZF/TacticalMap/HtmlUI/` 在 net472 构建后复制到：

`bin/<GameBinariesFolder>/TacticalMapUI/`

运行时通过 `Assembly.Location` 的目录定位该 UI 根目录。

## 当前不做

- 不删除原 `GUI/Prefabs/TacticalMap.xml`。
- 不删除 `MinimapWidget` / `TacticalMapLayer`。
- 不迁移 TerrainCache / FormationTracker / AgentTracker / OrderSystem 等核心逻辑到 JS。
- 不在本分支同时重构 TacticalMap 的战术逻辑。
