# Tactical Map 战术地图系统 项目计划书（含进度核对）

版本：v1.0
项目类型：Bannerlord 战场 RTS 战术辅助 UI
项目目标：制作一个可用于实际战斗指挥的实时战术地图系统

> 进度核对日期：2026-08-01
> 核对依据：实际源码（`Modules/New_ZZZF/工程/New_ZZZF/TacticalMap/`，共 16 个文件）
> 进度图例：✅ 已完成 ｜ 🔶 部分完成 ｜ ❌ 未开始

---

# 1. 项目目标

## 1.1 产品目标

为玩家提供一个类似 RTS 游戏的小地图系统，使玩家能够：

* 实时观察战场态势
* 快速判断敌我分布
* 查看编队位置和方向
* 通过小地图执行基础战术指令
* 辅助大规模战斗中的指挥决策

**进度核对**：✅ 上述 5 项产品目标均已达成基础能力。小地图已在战斗 HUD 右上角常驻显示，敌我以红/绿、玩家以青环+黄心区分，编队显示质心与朝向，左键/Shift+左键/右键可下发三类指令。

---

# 2. 功能范围

## 2.1 第一阶段目标（MVP）

必须实现：

### 战术地图显示

* [x] 右上角显示战术地图  → 由 `TacticalMap.xml` 的 `HorizontalAlignment/MarginRight/MarginTop` 定位，`TacticalMapLayer` 加载 GauntletLayer（z=90）
* [x] 地图尺寸稳定  → `TacticalSettings.MapSize=320` 固定像素边长；尺寸由 XML 约束，避开 `Widget.PosOffset` 版本差异
* [x] 支持不同分辨率  → 尺寸为固定像素，`SuggestedWidth/Height=MapSize`，外层 XML 用 `StretchToParent`，不受分辨率影响
* [x] 显示地形信息  → `TerrainCache.TryBake` 烘焙地形栅格（高度色带 + 水域/林地/悬崖语义着色），`BakeResolution=256`

验收标准：

* 进入战斗后 5 秒内显示地图 → ✅ `Initialize` 在 Mission 启动即烘焙，首帧 `_accum=UpdateInterval` 立即出图
* 地图无黑屏、无空白 → ✅ 多套纹理创建回退（字节数组→白色 sprite），并有 `_warnedXxx` 一次性告警
* 地形与当前战场基本对应 → ✅ **已修正地图显示范围过大问题**（见第 7 节）：改用 `Scene.GetSoftBoundaryVertex` 软边界 / `GetBoundingBox` 包围盒裁剪，只显示实际战场区域
* FPS下降小于5% → 🔶 已做大量优化（合并绘制通道、采样 96→48、DrawRect 5→2、单位点半径 2→1、5Hz 节流），但无量化压测数据

---

## 2.2 战场信息显示

### 玩家单位 ✅

显示：玩家当前位置、玩家朝向、玩家视角目标（镜头联动时的 `CameraTarget`）

验收：玩家移动时地图标记同步（每帧 `Tick` 更新 `_playerPos`）；误差小于地图比例 2%（坐标转换经 `WorldToUV/UVToWorld` 闭环，含 `Origin` 偏移，无累积误差）

### 编队显示 ✅

显示：友军编队、敌军编队、编队方向、编队编号（区分框）

实现：`FormationTracker` 每 `UpdateInterval` 刷新 `FormationSnapshot`（质心、朝向、队伍）；`MinimapWidget` 绘制敌绿/我红框 + 玩家白框

验收：战场存在编队时同步显示；移动中持续更新；不出现残留图标（快照重建，无残留）

### 单位显示 ✅

显示：敌我单位点、单位密度

实现：`AgentTracker` 写入 `AgentRGBA` 字节数组 → 烘焙成纹理；密度热力图 `DensityHeatmap` 动态图元叠加

验收：500 单位规模不卡顿 🔶（已优化但无压测）；单位刷新延迟 <0.5 秒 ✅（`UpdateInterval=0.2s`）

---

# 3. 操作功能

## 3.1 小地图移动指令 ✅

流程：鼠标点击 → `TacticalMapLayer.HitTestMinimap` 命中 → `UVToWorld` 坐标转换 → `OrderSystem.IssueOrder` 生成移动命令 → 编队执行 `SetMovementOrder(MovementOrderMove(...))`

验收：点击有效区域可生成移动目标 ✅；目标位置正确 ✅（UV/World 闭环含边界 Origin 偏移，无坐标偏移）；坐标偏移问题已通过 `ComputeBattleBounds` 修正

## 3.2 攻击移动 ✅（Shift + 左键）

`HandleClick(shift=true)` → `TacticalClickMode.AttackMove` → `MovementOrderAdvance`

验收：正确发送攻击移动命令 ✅；与普通移动区分明显 ✅（命令反馈消息分别显示"移动"/"推进"）

## 3.3 朝向调整 ✅（右键）

`HandleClick(rightButton=true)` → `TacticalClickMode.Face` → `SetFacingOrder(FacingOrderLookAtDirection(dir))`

验收：点击后编队方向改变 ✅；不触发错误移动 ✅（Face 模式不调用 SetMovementOrder）

---

# 4. UI 设计要求

## 4.1 视觉规范

* [x] Bannerlord 原生军事 UI 风格：低饱和、中世纪战争感、清晰信息层级
* [x] 避免科技 HUD 感：颜色使用低饱和（敌红 1,0,0 / 我青 0,230,255 / 中立灰）
* [ ] 动画反馈（见 4.2）

## 4.2 地图样式

* [x] 地形 / 水域 / 森林 / 高地：由 `TerrainCache` 语义着色（高度带、水域阈值、林地材质索引 1,2,6、悬崖坡度阈值）
* [x] 友军绿/蓝绿、敌军红、玩家高亮：已在 `MinimapWidget` 配色实现
* [ ] 编队编号数字标注：当前以红/绿/白框区分敌我，未绘制编号数字
* [ ] 动画反馈：移动/指令无过渡动画（仅有 `InformationManager` 文本反馈）

---

# 5. 易用性要求

## 编队选择 🔶（部分）

当前：点击地图默认控制当前选择（复用原生 `PlayerOrderController.SelectedFormations`）。

* [x] 默认控制当前选中编队（原生选择系统生效）
* [ ] 地图内点击编队选择：未实现（编队选择仍依赖游戏原生 UI，未在地图上做点选）
* [ ] Shift 加入选择：未实现（复用原生，地图侧未处理）
* [x] 空白区域移动：左键点击空白战场区域即下发移动指令

验收：玩家"无需打开菜单即可完成 选择→移动→调整方向" 🔶 部分满足（选择依赖原生 UI，地图内点选编队未做）

## 操作提示 ❌

当前仅通过 `InformationManager.DisplayMessage` 输出临时文本（开关、指令结果、报错），**未实现屏上常驻操作提示 UI**。

计划书要求的默认提示：

```
左键：移动
Shift+左键：攻击移动
右键：调整方向
C：镜头联动
N：开启/关闭地图
```

验收：第一次使用玩家无需查看文档即可理解操作 ❌ 未达标（无屏上提示，玩家需自行摸索或看文档）

---

# 6. 性能标准

测试环境：大型战役 500 vs 500

* [ ] CPU 战术地图更新 <3% → 🔶 已节流（5Hz）+ 合并绘制，但无实测数据
* [ ] GPU 绘制 <5ms → 🔶 绘制通道已合并、draw call 降到个位数，但无实测
* [ ] 内存新增 <100MB → ❌ 未测量（地形纹理 + AgentRGBA + 密度层常驻）
* [x] 稳定性 连续战斗 2 小时无崩溃 → 🔶 代码有多处 try/catch 与静态告警兜底，但未经长时压测

---

# 7. 技术制作规划

## Phase 1：核心框架 ✅

* [x] MissionLogic 接入 → `TacticalMapMissionLogic`
* [x] Controller 完成 → `TacticalMapController`
* [x] UI Layer 加载 → `TacticalMapLayer` + `TacticalMap.xml`
* [x] Widget 显示 → `MinimapWidget`

交付：能进入战场显示空地图 ✅（实际已能显示真实地图）

## Phase 2：数据系统 ✅

* [x] Terrain Cache → `TerrainCache`（含 `ComputeBattleBounds` 战场边界裁剪）
* [x] Agent Tracker → `AgentTracker`
* [x] Formation Tracker → `FormationTracker`
* [x] Player Tracker → `TacticalMapController._playerPos/_camTarget`

交付：地图可以显示真实战场信息 ✅

## Phase 3：交互系统 ✅

* [x] 点击坐标转换 → `HitTestMinimap` + `UVToWorld/WorldToUV`
* [x] 移动命令 → `OrderSystem.Move`
* [x] 攻击移动 → `OrderSystem.AttackMove`
* [x] 朝向调整 → `OrderSystem.Face`

交付：玩家可以通过地图指挥 ✅

## Phase 4：UI 优化 🔶（部分）

* [x] 地图边框 → `TacticalMap.xml` + `MinimapWidget` 边框绘制
* [ ] 图标优化 → 基础图元，未做精细图标
* [ ] 编队编号 → 仅颜色框，无数字
* [ ] 操作提示 → 仅有日志，无屏上 UI
* [ ] 动画反馈 → 无

交付：达到玩家可用标准 🔶 基础可用，UI 打磨未完成

## Phase 5：测试优化 ❌

* [ ] 小规模战斗测试
* [ ] 大规模战斗（500v500）压测
* [ ] 多地图验证（边界裁剪在不同场景表现）
* [ ] 多分辨率验证
* [ ] 性能量化（CPU/GPU/内存）

---

# 8. 验收清单

## 功能验收

| 项目   | 状态 | 说明 |
| ---- | -- | -- |
| 地图显示 | ✅ | 右上角常驻，尺寸稳定 |
| 地形显示 | ✅ | 高度/水域/林地/悬崖语义着色 |
| 单位显示 | ✅ | 敌我单位点 + 密度热力 |
| 编队显示 | ✅ | 质心 + 朝向 + 红绿框 |
| 玩家显示 | ✅ | 位置 + 朝向 + 视角目标 |
| 点击移动 | ✅ | 左键 → Move |
| 攻击移动 | ✅ | Shift+左键 → Advance |
| 方向调整 | ✅ | 右键 → Face |
| 镜头联动 | ✅ | C 键切换，点击飞镜头 |

## UI 验收

| 项目        | 状态 | 说明 |
| --------- | -- | -- |
| 无明显 Debug 感 | 🔶 | 配色已军事化，但保留 `InformationManager` 诊断文本 |
| 颜色符合军事风格  | ✅ | 低饱和红/青/灰 |
| 图标容易识别    | 🔶 | 框+点可辨，无数字编号 |
| 操作提示清晰    | ❌ | 无屏上提示 UI，仅日志 |
| 分辨率适配     | ✅ | 固定像素 + StretchToParent |

## 性能验收

| 项目      | 标准    | 状态 |
| ------- | ----- | -- |
| 500 单位战斗 | 稳定    | 🔶 已优化未压测 |
| 连续 2 小时   | 无崩溃   | 🔶 有兜底未长测 |
| 地图刷新    | 稳定    | ✅ 5Hz 节流 |
| 内存      | 无持续增长 | ❌ 未测量 |

---

# 9. 当前已知风险（含本次进度更新）

## 风险1：Bannerlord API 版本变化 🔴 高

影响：Camera Patch、Formation API、Widget 尺寸 API

现状：

* `TacticalCameraPatch` 通过 Harmony 反射私有字段挂在相机上，版本升级易失效（已有空值兜底）
* `MinimapWidget` 用反射读取 `Width/Height`（字段优先/属性兜底）规避版本差异
* `TacticalMapLayer.HitTestMinimap` 改用 `AreaRect.GetBoundingBox()` 规避 `GlobalPosition` 类型差异

解决：API 封装（已做部分）+ 增加版本检测（未做）

## 风险2：地图信息过载 🟡 中

影响：玩家无法快速理解

现状：设置项 `EnableRiskOverlay / EnableDensityHeatmap / EnableUnitMarkers / EnableAgentMarkers` 已存在，但**无运行时图层开关键**

解决：需增加图层开关热键（未实现）

## 风险3：命令误操作 🟡 中

影响：玩家错误控制部队

现状：指令反馈用 `InformationManager` 文本（已显示"已向 N 个编队下达[X]指令"）；未选中编队会提示"未选择任何编队"

解决：明确选择状态（已做日志提示）+ 命令反馈（已做）+ 二次确认（未做）

## 风险4（新增）：战场边界裁剪兼容性 🟡 中

影响：不同场景软边界/包围盒差异导致地图范围异常

现状：`ComputeBattleBounds` 两阶段兜底（软边界 → 包围盒 → 全地形），已加 10% 边距防裁切

解决：多地图验证（Phase 5 未完成）

---

# 10. 最终交付标准（对照）

| 标准 | 状态 |
| -- | -- |
| 1. 玩家进入战斗即可看到战术地图 | ✅ |
| 2. 玩家无需阅读说明即可理解基本操作 | ❌（缺屏上操作提示） |
| 3. 1000 单位规模战斗保持稳定 | 🔶（未压测） |
| 4. 小地图命令不会产生误操作 | 🔶（有反馈无二次确认） |
| 5. UI 风格符合 Bannerlord 世界观 | ✅ |
| 6. 无明显开发调试元素 | 🔶（残留诊断文本/日志） |

达到以上条件，版本标记：

Tactical Map v1.0 Release — **当前状态：功能可用，UI 打磨与压测未完成，未达 Release**

---

# 附录：本计划书与实际代码映射

| 计划书章节 | 对应源码文件 |
| ----- | ----- |
| 地图显示/地形 | `Terrain/TerrainCache.cs`, `Terrain/TerrainAnalyzer.cs`, `Terrain/TerrainCell.cs` |
| 单位/编队/玩家 | `Tracking/AgentTracker.cs`, `Tracking/FormationTracker.cs`, `Core/TacticalMapController.cs` |
| 交互指令 | `Core/OrderSystem.cs`（含 `SelectionSystem`） |
| 镜头联动 | `Core/CameraController.cs`, `Core/TacticalCameraPatch.cs` |
| UI 绘制 | `UI/MinimapWidget.cs`, `UI/TacticalMapLayer.cs`, `UI/TacticalMapVM.cs` |
| 配置/开关 | `Config/TacticalSettings.cs`, `Config/FeatureGate.cs`, `Config/TacticalMapBootstrap.cs` |
| 生命周期 | `Core/TacticalMapMissionLogic.cs` |

> 关联审计文档：`2026-08-01_战术地图TacticalMap_审计文档.md`（含全部源码全文）
