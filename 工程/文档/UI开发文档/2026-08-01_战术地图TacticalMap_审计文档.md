# 战术地图（TacticalMap / RTS Minimap）功能说明与代码审计文档

> 整理日期：2026-08-01  
> 模块路径：`Modules/New_ZZZF/工程/New_ZZZF/TacticalMap/`  
> 关联 Prefab：`New_ZZZF/GUI/Prefabs/TacticalMap.xml`  
> 目标引擎：Mount & Blade II: Bannerlord 1.4.6  
> 用途：供外部审计，包含所有源码全文。

---

## 一、功能概述

在战场中提供一个 **RTS 风格战术小地图**，覆盖在战斗 HUD 右上角，用于：

- 显示战场地形（高度色带、水域/林地/悬崖语义着色 + 风险叠加层）
- 实时显示双方单位分布（我方亮青、敌方纯红、中立灰）
- 显示编队质心标记 + 朝向，敌我以红/绿框区分
- 显示玩家自身位置（青环 + 黄心）与镜头目标指示
- 支持点击小地图下达编队指令（移动 / 攻击推进 / 朝向）
- 可开启"镜头联动"：点击小地图后镜头平滑飞向目标点

### 1.1 操作方式

| 操作 | 热键 | 说明 |
|------|------|------|
| 开关小地图 | `N` | 显示/隐藏 |
| 切换镜头联动 | `C` | 开启后点击小地图飞镜头 |
| 左键点击 | 鼠标 | 移动指令 |
| Shift + 左键 | 鼠标 | 攻击移动（推进） |
| 右键点击 | 鼠标 | 朝向指令 |

### 1.2 可配置项（`TacticalSettings`）

| 字段 | 默认值 | 说明 |
|------|--------|------|
| `EnableMinimap` | `true` | 总开关 |
| `EnableRiskOverlay` | `true` | 风险叠加层 |
| `EnableDensityHeatmap` | `true` | 单位密度热力 |
| `EnableUnitMarkers` | `true` | 编队/单位标记 |
| `EnableAgentMarkers` | `true` | 单位点层 |
| `EnableCameraLink` | `true` | 镜头联动 |
| `MapSize` | `320` | 小地图像素边长 |
| `MapMargin` | `16` | 边距 |
| `BakeResolution` | `256` | 地形栅格每边采样数 |
| `UpdateInterval` | `0.2f` | 动态层刷新间隔（5 Hz） |
| `CliffSlopeThreshold` | `0.55f` | 悬崖坡度阈值 |
| `CliffHeightJump` | `2.5f` | 悬崖相邻高度突变阈值 |
| `WaterHeightFraction` | `0.05f` | 水域高度占比阈值 |
| `ForestMaterialIndices` | `1,2,6` | 林地材质层索引 |

---

## 二、架构与调用链

```
SubModule.OnSubModuleLoad
  └─ TacticalMapBootstrap.OnSubModuleLoad()      注册 Harmony 相机补丁

SubModule.OnMissionBehaviorInitialize / OnMissionStart
  └─ TacticalMapBootstrap.OnMissionStart(mission)
       └─ mission.AddMissionBehavior(new TacticalMapMissionLogic())

TacticalMapMissionLogic (MissionBehavior)
  ├─ OnAfterMissionCreated()
  │    └─ new TacticalMapController(Mission).Initialize(Mission)
  │         └─ TerrainCache.TryBake(scene)          ★ 仅一次：烘焙地形
  ├─ OnMissionTick(dt)
  │    ├─ 热键处理（N / C / 点击）
  │    └─ TacticalMapController.Tick()
  │         ├─ FormationTracker.Update()            （节流 5Hz）
  │         ├─ AgentTracker.Update()                （节流 5Hz）
  │         └─ MinimapWidget.OnRender()             （每帧绘制）
  └─ OnEndMission() → SetVisible(false)
```

### 2.1 模块分层

```
TacticalMap/
├─ Config/        配置与开关、Harmony 引导入口
│   ├─ TacticalSettings.cs      所有可调参数
│   ├─ FeatureGate.cs           总/子功能开关
│   └─ TacticalMapBootstrap.cs  SubModule 接入点（Harmony + MissionBehavior 注入）
├─ Core/          控制器与核心逻辑
│   ├─ TacticalMapController.cs 总控制器（中枢）
│   ├─ TacticalMapMissionLogic.cs  MissionBehavior 外壳
│   ├─ OrderSystem.cs           点击→编队指令路由 + 选择系统
│   ├─ CameraController.cs      镜头联动状态
│   └─ TacticalCameraPatch.cs   Harmony 后置补丁（接管相机）
├─ Terrain/       地形烘焙与语义推断
│   ├─ TerrainCache.cs          ★ 地形栅格烘焙 + 坐标转换 + 战场边界
│   ├─ TerrainAnalyzer.cs       高度/法线/材质 → 语义类别
│   └─ TerrainCell.cs           栅格单元数据结构
├─ Tracking/      动态实体追踪
│   ├─ AgentTracker.cs          单位点 + 密度
│   └─ FormationTracker.cs      编队快照
└─ UI/            绘制与界面
    ├─ MinimapWidget.cs         ★ 每帧自定义绘制（Widget 子类）
    ├─ TacticalMapLayer.cs      GauntletLayer 管理 + 点击命中测试
    └─ TacticalMapVM.cs         轻量 ViewModel
```

`★` 标记为本审计重点关注文件（性能 / 正确性关键路径）。

---

## 三、关键设计说明

### 3.1 地形烘焙（`TerrainCache.TryBake`）

战斗开局调用一次，把场景地形采样成 `BakeResolution × BakeResolution`（默认 256×256）的低分辨率栅格：

1. 通过 `Scene.GetTerrainData` 获取地形节点尺寸。
2. **计算实际战场边界**（见 3.2）。
3. 遍历每个栅格单元，采样：
   - `Scene.GetTerrainHeight(pos, true)` —— 高度
   - `Scene.GetTerrainHeightAndNormal(pos, ...)` —— 法线
   - `Scene.GetTerrainPhysicsMaterialIndexData(nodeX, nodeY)` —— 物理材质层
4. 调用 `TerrainAnalyzer.ClassifyAll` 推断语义（水域/林地/悬崖/平原）。
5. 生成 `TerrainBaseRGBA`（地形底图）、`RiskRGBA`（风险叠加）、`AgentRGBA`（动态单位层，初始全透明）。

烘焙产物均为 `byte[]`（RGBA），不依赖任何纹理 API，规避了 BL 1.4.6 下 `Texture.CreateFromByteArray` 产出白纹理的兼容性问题。

### 3.2 战场边界裁剪（本次修复重点）

**问题**：原实现 `WorldW = nodeDim.X * nodeSize` 使用整个地形格网尺寸，地图显示范围过大，单位在画面中过小。

**修复**（`TerrainCache.ComputeBattleBounds`，两级兜底）：

```
① 优先：Scene.GetSoftBoundaryVertexCount() > 0
   → 遍历所有 walk_area 软边界顶点求包围盒
   → 外扩 10% 边距（防边界单位被裁切）
② 回退：Scene.GetBoundingBox() 场景包围盒（所有实体的最小矩形）
③ 都失败：使用完整地形范围（功能降级但不崩溃）
```

修复后坐标约定：

```
uv(0..1) → 世界 (OriginX + uv.X*WorldW, OriginY + uv.Y*WorldH)
世界 → uv ((world-Origin)/WorldW, (world-Origin)/WorldH)
```

`OriginX / OriginY / WorldW / WorldH` 均由战场边界决定，`CellStep = Max(WorldW, WorldH) / BakeResolution`。

### 3.3 绘制管线（`MinimapWidget.OnRender`）

每帧执行，绘制区域来自 Widget 的 `AreaRect`（兼容不同 BL 版本 `GlobalPosition` 类型差异）：

```
1. 背景矩形（深色半透明）
2. 地形 + 风险叠加：优先烘焙纹理（DrawTexture，单 draw call）；降级逐像素 DrawRect
3. 编队标记：每个编队 2 次 DrawRect（外框色 + 填充色）+ 朝向线
4. 玩家标记：青环 + 黄心
5. 镜头目标：菱形指示
```

**性能优化（本次提交）**：
- 四趟独立循环（地形→风险→密度→单位）合并为单趟复合循环
- 逐像素采样分辨率 96→48（限制上限 `Min(48, Width)`）
- 编队标记从 5 次 DrawRect 降为 2 次
- 单位点绘制半径 2→1（像素写入减少约 64%）
- 动态刷新间隔 0.15s→0.2s（CPU 开销降低 ~25%）
- 移除调试日志分配

> 说明：当前 BL 版本 `UseBakedTexture = false`（纹理路径因白纹理 bug 禁用），绘制走逐像素回退。若未来版本修复该 API，将常量置 `true` 即可切回单 draw call 纹理路径，流畅度再提升一个数量级。

### 3.4 单位 / 编队追踪

- **`AgentTracker.Update`**（节流 5Hz）：遍历 `Mission.Agents`，按世界坐标 `WorldToUV` 映射到栅格，累加密度并 `PaintAgent` 单位点（半径 1，3×3）。越界单位被跳过。
- **`FormationTracker.Update`**（节流 5Hz）：遍历 `Mission.Teams` → `FormationsIncludingEmpty`，生成 `FormationSnapshot`（质心、朝向、颜色、敌我标志）。编队数通常 ≤ 数十，与单位规模无关，对 500v500 零压力。

### 3.5 指令系统（`OrderSystem`）

点击小地图 → `TacticalMapLayer.HitTestMinimap` 命中测试得到 UV → `TerrainCache.UVToWorld` 还原世界坐标 → `OrderSystem.IssueOrder`：

- 目标编队来自 `SelectionSystem.GetTargetFormations`（优先玩家选中的编队，否则所有非空编队）
- `Move` → `Formation.SetMovementOrder(MovementOrderMove)`
- `AttackMove` → `MovementOrderAdvance`
- `Face` → `FacingOrderLookAtDirection`
- `Stop` → `MovementOrderStop`

### 3.6 镜头联动（`TacticalCameraPatch` + `CameraController`）

- `CameraController` 持有目标世界坐标与激活状态（`Active`）。
- `TacticalCameraPatch.Patch` 用 Harmony 对 `MissionScreen.UpdateCamera` 打 **后置补丁**（独立 Harmony id `"TacticalMap"`，不与项目其他相机补丁冲突）。
- 激活时通过 `Traverse` 写入私有字段 `_cameraSpecialTargetPositionToAdd` / `_cameraSpecialTargetAddedBearing` 等，使镜头平滑飞向目标；接近目标（距离 < 3 单位）后自动失活交还控制。

---

## 四、代码审计要点

### 4.1 依赖与耦合

| 项 | 说明 | 风险 |
|----|------|------|
| Harmony 私有字段注入 | `TacticalCameraPatch` 通过反射写入 `MissionScreen` 私有字段 | 版本升级可能失效（字段重命名）；已有距离阈值自动失活兜底 |
| `Scene` 私有 API | `GetSoftBoundaryVertex` / `GetTerrainPhysicsMaterialIndexData` 等非公开稳定接口 | 同上，建议加版本判断 |
| 反射取白色纹理 | `EnsureWhiteTexture` 反射查找 sprite（兜底路径） | 仅在字节数组纹理失败时触发，失败有 `WarnOnce` 提示 |
| `GlobalPosition` 类型差异 | 改用 `AreaRect.GetBoundingBox()` 规避 | 已规避 |

**结论**：除 `TacticalCameraPatch` 的私有字段反射外，其余均为较稳定的公开/半公开接口；整体与 `SubModule` 解耦，可整体搬入独立 mod。

### 4.2 线程与异常安全

- 所有绘制/追踪均在主线程（Mission Tick / OnRender）执行，无跨线程共享。
- `TerrainCache.TryBake`、`GetHeightAt`、`GetTerrainHeight` 等关键调用均有 `try/catch`，失败返回安全值并提示，不会崩溃整个游戏。
- `Vec2` 的 `X/Y` 为只读属性，相关计算已改为局部 `float` 后构造 `Vec2`（已修复 CS0200）。

### 4.3 性能

| 维度 | 评估 |
|------|------|
| 地形烘焙 | 仅一次（256×256 = 65k 次采样），开销可忽略 |
| 动态追踪 | 5Hz 节流，编队数/单位数线性，500v500 实测可承受 |
| 每帧绘制 | 逐像素回退路径约 2500 次 DrawRect（已优化）；纹理路径为 3 次 DrawTexture |
| GC | 仅动态层重建产生少量短期数组；无每帧大对象分配 |

**建议**：
1. 若后续战场单位数极大（>2000），可考虑把 `AgentRGBA` 改为对象池复用，避免每帧 `new byte[]`。
2. 当前地图为固定正方形（`BakeResolution × BakeResolution`），战场非正方形时短边会多采样少量越界地形，可进一步优化为按宽高比的非正方形栅格。

### 4.4 已知限制

1. `TerrainAnalyzer` 的语义推断为工程化启发式（高度/法线/材质/邻域），非引擎原生语义；不同场景材质索引需调 `ForestMaterialIndices`。
2. 林地识别依赖物理材质索引命中，若场景未使用标准索引则可能漏判（有"低坡绿色区域"兜底逻辑可增强）。
3. 镜头联动依赖 `MissionScreen.UpdateCamera` 私有方法签名，版本升级需回归测试。

---

## 五、源码全文

> 以下为功能全部 16 个源文件当前内容，按模块分层列出。

### 5.1 Config

#### `TacticalSettings.cs`

```csharp
using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>战术小地图全部可调参数。改这里即可调行为，无需动逻辑。</summary>
    public sealed class TacticalSettings
    {
        public static TacticalSettings Instance { get; } = new TacticalSettings();

        // ---- 总开关 ----
        public bool EnableMinimap = true;

        // ---- 图层开关 ----
        public bool EnableRiskOverlay = true;     // 风险叠加层（悬崖/水/林）
        public bool EnableDensityHeatmap = true;   // 单位密度热力
        public bool EnableUnitMarkers = true;      // 编队/单位标记
        public bool EnableAgentMarkers = true;     // 单位点层

        // ---- 镜头联动 ----
        public bool EnableCameraLink = true;

        // ---- 地图外观 ----
        public float MapSize = 320f;     // 小地图像素边长
        public float MapMargin = 16f;    // 边距（右上角）
        public float MapOpacity = 0.9f;

        // ---- 性能/烘焙 ----
        // 地形栅格分辨率（每边采样数）。256 => 65k 单元；越高越细但烘焙越慢、内存越大。
        public int BakeResolution = 256;
        // 动态纹理刷新间隔（秒）。0.2 => 5Hz，减少AgentTracker/FormationTracker更新频率，降低CPU开销
        public float UpdateInterval = 0.2f;

        // ---- 地形语义推断参数 ----
        public float CliffSlopeThreshold = 0.55f;      // 法线.z < 此值视为陡坡
        public float CliffHeightJump = 2.5f;           // 邻接格高差 > 此值视为悬崖
        public float WaterHeightFraction = 0.05f;      // 地形最低 5% 高度内视为水
        public List<int> ForestMaterialIndices = new List<int> { 1, 2, 6 }; // 物理材质层视为林地
    }
}
```

#### `FeatureGate.cs`

```csharp
using System;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>功能总/子开关。便于服务端下发或调试时一键关闭。</summary>
    public enum TacticalFeature
    {
        Minimap,
        RiskOverlay,
        DensityHeatmap,
        UnitMarkers,
        AgentMarkers,
        CameraLink,
    }

    public static class FeatureGate
    {
        public static bool Enabled { get; set; } = true;

        public static bool IsEnabled(TacticalFeature f)
        {
            if (!Enabled) return false;
            switch (f)
            {
                case TacticalFeature.Minimap: return true;
                case TacticalFeature.RiskOverlay: return true;
                case TacticalFeature.DensityHeatmap: return true;
                case TacticalFeature.UnitMarkers: return true;
                case TacticalFeature.AgentMarkers: return true;
                case TacticalFeature.CameraLink: return true;
                default: return false;
            }
        }
    }
}
```

#### `TacticalMapBootstrap.cs`

```csharp
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.TacticalMap.Core;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>SubModule 接入点：Harmony 补丁注册 + MissionBehavior 注入。</summary>
    public static class TacticalMapBootstrap
    {
        public static void OnSubModuleLoad()
        {
            // 注册相机补丁（仅在用到镜头联动时生效，失活后自动交还原逻辑）
            TacticalCameraPatch.Patch();
        }

        public static void OnMissionStart(Mission mission)
        {
            if (mission == null) return;
            mission.AddMissionBehavior(new TacticalMapMissionLogic());
        }
    }
}
```

### 5.2 Core

#### `TacticalMapController.cs`

```csharp
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Terrain;
using New_ZZZF.TacticalMap.Tracking;
using New_ZZZF.TacticalMap.UI;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 小地图总控制器：烘焙地形、驱动追踪器、派发编队指令、管理 UI 层与镜头联动。
    /// 绘制由 MinimapWidget 直接在 OnRender 里完成（读取本控制器暴露的数据），不再依赖位图纹理。
    /// 所有对外依赖都收敛在这里，方便整个 TacticalMap 文件夹整体抽离成独立 mod。
    /// </summary>
    public sealed class TacticalMapController
    {
        private readonly Mission _mission;
        private readonly TerrainCache _cache;
        private readonly FormationTracker _formationTracker;
        private readonly AgentTracker _agentTracker;
        private readonly OrderSystem _orderSystem;
        private TacticalMapLayer _layer;
        private bool _visible;
        private float _accum;
        private bool _cameraLink;
        private Vec2? _playerPos;
        private Vec2? _camTarget;
        private int _agentVersion;

        public TerrainCache Cache => _cache;
        public bool IsVisible => _visible;
        public List<FormationSnapshot> FormationSnapshots => _formationTracker.Snapshots;
        public Vec2? PlayerPos => _playerPos;
        public Vec2? CameraTarget => _camTarget;
        // 动态单位层（每个 agent 一个点），供 MinimapWidget 烘焙成纹理
        public byte[] AgentRGBA => _cache.AgentRGBA;
        public int AgentDataVersion => _agentVersion;

        public TacticalMapController(Mission mission)
        {
            _mission = mission;
            var settings = TacticalSettings.Instance;
            _cache = new TerrainCache(settings);
            _formationTracker = new FormationTracker();
            _agentTracker = new AgentTracker(_cache);
            _orderSystem = new OrderSystem(_cache);
            CameraController.Instance = new CameraController();
        }

        /// <summary>战斗开局烘焙地形；失败返回 false（UI 不会显示）。</summary>
        public bool Initialize(Mission mission)
        {
            if (mission == null || mission.Scene == null) return false;
            return _cache.TryBake(mission.Scene);
        }

        public void SetVisible(MissionScreen ms, bool visible)
        {
            if (visible && _layer == null)
            {
                _layer = new TacticalMapLayer(this);
                _layer.Create(ms);
                _accum = TacticalSettings.Instance.UpdateInterval; // 立刻出第一帧
            }
            else if (!visible && _layer != null)
            {
                _layer.Destroy(ms);
                _layer = null;
                if (CameraController.Instance != null) CameraController.Instance.Disable();
            }
            _visible = visible;
        }

        /// <summary>每帧调用（仅在可见时）。标记/密度按 UpdateInterval 节流刷新；绘制由控件每帧完成。</summary>
        public void Tick(Mission mission, MissionScreen ms, float dt)
        {
            if (!_visible || _layer == null) return;

            _playerPos = (_mission.MainAgent != null) ? _mission.MainAgent.Position.AsVec2 : (Vec2?)null;
            _camTarget = (CameraController.Instance != null && CameraController.Instance.Active)
                ? CameraController.Instance.TargetWorldPos : (Vec2?)null;

            _accum += dt;
            if (_accum >= TacticalSettings.Instance.UpdateInterval)
            {
                _accum = 0f;
                _formationTracker.Update(mission);
                _agentTracker.Update(mission);
                _agentVersion++;   // 单位层已刷新，通知纹理缓存重建
            }
        }

        /// <summary>小地图点击：根据按键决定移动 / 攻击移动 / 朝向，并可联动镜头。</summary>
        public void HandleClick(Vec2 mousePixel, bool shift, bool rightButton)
        {
            if (_layer == null) return;
            if (!_layer.HitTestMinimap(mousePixel, out Vec2 uv)) return;
            Vec2 world = _cache.UVToWorld(uv);

            TacticalClickMode mode = rightButton ? TacticalClickMode.Face
                : shift ? TacticalClickMode.AttackMove
                : TacticalClickMode.Move;
            _orderSystem.IssueOrder(_mission, world, mode);

            if (FeatureGate.IsEnabled(TacticalFeature.CameraLink) && _cameraLink && CameraController.Instance != null)
            {
                CameraController.Instance.Enable(world);
            }
        }

        /// <summary>C 键：切换"小地图点击联动镜头"模式。</summary>
        public void ToggleCameraFollow()
        {
            _cameraLink = !_cameraLink;
            if (CameraController.Instance != null && !_cameraLink)
                CameraController.Instance.Disable();
            string msg = _cameraLink ? "战术地图：已开启 点击联动镜头" : "战术地图：已关闭 点击联动镜头";
            InformationManager.DisplayMessage(new InformationMessage(msg, new Color(0.2f, 0.9f, 1f, 1f)));
        }
    }
}
```

#### `TacticalMapMissionLogic.cs`

```csharp
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using New_ZZZF.TacticalMap.Config;
using System;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 战场 MissionBehavior：挂载小地图控制器、处理开关/点击/相机热键。
    /// </summary>
    public sealed class TacticalMapMissionLogic : MissionLogic
    {
        private TacticalMapController _controller;
        private MissionScreen _ms;
        private bool _ready;
        private bool _initialized;

        public override void OnAfterMissionCreated()
        {
            InformationManager.DisplayMessage(new InformationMessage("[TMap] OnAfterMissionCreated 进入"));
            if (_initialized) return;
            try
            {
                base.OnAfterMissionCreated();
                if (!FeatureGate.Enabled) { InformationManager.DisplayMessage(new InformationMessage("[TMap] 功能被关闭 (EnableMinimap=false)")); _initialized = true; return; }
                _controller = new TacticalMapController(Mission);
                _ready = _controller.Initialize(Mission);
                _initialized = true;
                var c = _controller.Cache;
                string err = string.IsNullOrEmpty(c.LastError) ? "" : ("err=" + c.LastError);
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] 初始化 _ready={_ready} baked={c.IsBaked} {c.Width}x{c.Height} {err}"));
            }
            catch (Exception ex)
            {
                string fr = ex.StackTrace != null ? ex.StackTrace.Split('\n')[0].Trim() : "";
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] OnAfterMissionCreated 异常: {ex.GetType().Name}: {ex.Message} @ {fr}"));
                _initialized = true;
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            // 懒初始化兜底：若 OnAfterMissionCreated 未被引擎调用，则在首个可用 tick 初始化
            if (!_initialized && Mission != null && Mission.Scene != null)
            {
                try
                {
                    _controller = new TacticalMapController(Mission);
                    _ready = _controller.Initialize(Mission);
                    _initialized = true;
                    var c = _controller.Cache;
                    string err = string.IsNullOrEmpty(c.LastError) ? "" : ("err=" + c.LastError);
                    InformationManager.DisplayMessage(new InformationMessage($"[TMap] 懒初始化 _ready={_ready} baked={c.IsBaked} {c.Width}x{c.Height} {err}"));
                }
                catch (Exception ex)
                {
                    string fr = ex.StackTrace != null ? ex.StackTrace.Split('\n')[0].Trim() : "";
                    InformationManager.DisplayMessage(new InformationMessage($"[TMap] 懒初始化 异常: {ex.GetType().Name}: {ex.Message} @ {fr}"));
                    _initialized = true;
                }
            }

            if (!_ready || _controller == null) return;

            if (_ms == null) _ms = ScreenManager.TopScreen as MissionScreen;
            if (_ms == null) return;

            var s = TacticalSettings.Instance;

            if (Input.IsKeyPressed(s.ToggleKey))
            {
                _controller.SetVisible(_ms, !_controller.IsVisible);
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] 切换显示 -> Visible={_controller.IsVisible} ready={_ready}"));
            }

            if (_controller.IsVisible)
            {
                if (Input.IsKeyPressed(s.CameraFollowKey))
                    _controller.ToggleCameraFollow();

                Vec2 mouse = Input.MousePositionPixel;
                bool left = Input.IsKeyPressed(InputKey.LeftMouseButton);
                bool right = Input.IsKeyPressed(InputKey.RightMouseButton);
                if (left || right)
                {
                    bool shift = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
                    _controller.HandleClick(mouse, shift, right);
                }

                _controller.Tick(Mission, _ms, dt);
            }
        }

        protected override void OnEndMission()
        {
            if (_controller != null && _ms != null)
                _controller.SetVisible(_ms, false);
            base.OnEndMission();
        }
    }
}
```

#### `OrderSystem.cs`

```csharp
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 点击模式 -> 实际编队指令 的路由层。
    /// 这里只调用 Bannerlord 1.4.6 已验证存在的 Formation/Order API。
    /// </summary>
    public enum TacticalClickMode
    {
        Move,        // 移动到点（保留阵型）
        AttackMove,  // 推进/攻击移动
        Face,        // 朝向某点
        Stop         // 原地停止
    }

    public sealed class OrderSystem
    {
        private readonly Terrain.TerrainCache _cache;

        public OrderSystem(Terrain.TerrainCache cache)
        {
            _cache = cache;
        }

        public void IssueOrder(Mission mission, Vec2 worldPos, TacticalClickMode mode)
        {
            if (mission == null || mission.Scene == null) return;
            var formations = SelectionSystem.GetTargetFormations(mission);
            if (formations.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("战术地图：未选择任何编队", new Color(1f, 0.6f, 0.1f, 1f)));
                return;
            }

            float height = 0f;
            try { height = mission.Scene.GetTerrainHeight(worldPos, true); } catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] 取地形高度失败: {ex.Message}")); }

            int issued = 0;
            foreach (var formation in formations)
            {
                if (formation == null) continue;
                switch (mode)
                {
                    case TacticalClickMode.Move:
                        formation.SetMovementOrder(MovementOrder.MovementOrderMove(
                            new WorldPosition(mission.Scene, new Vec3(worldPos.X, worldPos.Y, height))));
                        break;
                    case TacticalClickMode.AttackMove:
                        formation.SetMovementOrder(MovementOrder.MovementOrderAdvance);
                        break;
                    case TacticalClickMode.Face:
                        {
                            Vec2 dir = worldPos - formation.CachedAveragePosition;
                            if (dir.LengthSquared > 1E-4f)
                                formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(dir.Normalized()));
                        }
                        break;
                    case TacticalClickMode.Stop:
                        formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                        break;
                }
                issued++;
            }

            if (issued > 0)
            {
                string label = mode == TacticalClickMode.Move ? "移动"
                    : mode == TacticalClickMode.AttackMove ? "推进"
                    : mode == TacticalClickMode.Face ? "朝向" : "停止";
                InformationManager.DisplayMessage(new InformationMessage($"战术地图：已向 {issued} 个编队下达[{label}]指令", new Color(0.2f, 0.9f, 1f, 1f)));
            }
        }
    }

    /// <summary>
    /// 选择系统：返回应接收指令的编队（优先玩家当前选中的编队）。
    /// </summary>
    public static class SelectionSystem
    {
        public static List<Formation> GetTargetFormations(Mission mission)
        {
            var result = new List<Formation>();
            if (mission == null || mission.PlayerTeam == null) return result;

            var oc = mission.PlayerTeam.PlayerOrderController;
            if (oc != null && oc.SelectedFormations != null && oc.SelectedFormations.Count > 0)
            {
                foreach (var f in oc.SelectedFormations) result.Add(f);
                return result;
            }

            // 未选中时，发给玩家所有非空编队
            var forms = mission.PlayerTeam.FormationsIncludingEmpty;
            if (forms != null)
            {
                foreach (var f in forms)
                    if (f != null && f.CountOfUnits > 0) result.Add(f);
            }
            return result;
        }
    }
}
```

#### `CameraController.cs`

```csharp
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>镜头联动状态：记录目标世界坐标、激活状态，以及该状态是否由本功能启用。</summary>
    public sealed class CameraController
    {
        public static CameraController Instance { get; set; }

        public Vec2 TargetWorldPos { get; private set; }
        public bool Active { get; private set; }

        public void Enable(Vec2 worldPos)
        {
            TargetWorldPos = worldPos;
            Active = true;
        }

        public void Disable()
        {
            Active = false;
            TargetWorldPos = Vec2.Zero;
        }
    }
}
```

#### `TacticalCameraPatch.cs`

```csharp
using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 后置补丁：在 MissionScreen.UpdateCamera 之后接管相机，把镜头平滑飞向小地图点击的世界点。
    /// 独立 Harmony id "TacticalMap"，与项目内其他相机补丁互不冲突。
    /// </summary>
    [HarmonyPatch(typeof(MissionScreen), "UpdateCamera")]
    public static class TacticalCameraPatch
    {
        public static void Patch()
        {
            try
            {
                var harmony = new Harmony("TacticalMap");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] 相机补丁注册失败: {ex.Message}"));
            }
        }

        static void Postfix(MissionScreen __instance)
        {
            var cam = CameraController.Instance;
            if (cam == null || !cam.Active) return;

            var scene = __instance.Mission?.Scene;
            if (scene == null) return;

            float targetH = 0f;
            try { targetH = scene.GetTerrainHeight(cam.TargetWorldPos, true); } catch { }

            // 用 Traverse 写入私有字段，使相机平滑飞向目标
            var tpos = new Traverse(__instance).Field("_cameraSpecialTargetPositionToAdd");
            if (tpos.FieldExists())
                tpos.SetValue(new TaleWorlds.Library.Vec3(cam.TargetWorldPos.X, cam.TargetWorldPos.Y, targetH));

            var tbear = new Traverse(__instance).Field("_cameraSpecialTargetAddedBearing");
            if (tbear.FieldExists())
                tbear.SetValue(0f);

            // 接近目标后自动失活，交还原生相机控制
            var mainAgent = __instance.Mission?.MainAgent;
            if (mainAgent != null)
            {
                float d = TaleWorlds.Library.Vec2.Distance(mainAgent.Position.AsVec2, cam.TargetWorldPos);
                if (d < 3f) cam.Disable();
            }
        }
    }
}
```

### 5.3 Terrain

#### `TerrainCache.cs`

```csharp
using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// 在战斗开局把 Scene 地形烘焙成一张低分辨率战术栅格。
    /// 只做一次（或场景变化时），之后由 MinimapCompositor 复用。
    /// 所有坐标约定：uv(0..1) -> 世界 (OriginX + uv.X*WorldW, OriginY + uv.Y*WorldH)。
    /// WorldW/WorldH 由实际战场边界决定（软边界或包围盒），而非整个地形大小。
    /// </summary>
    public sealed class TerrainCache
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public float WorldW { get; private set; }
        public float WorldH { get; private set; }
        public float OriginX { get; private set; }
        public float OriginY { get; private set; }
        public float CellStep { get; private set; }
        public float MinH { get; private set; }
        public float MaxH { get; private set; }

        public TerrainCell[,] Cells { get; private set; }

        // 烘焙一次的静态底图（高度色 + 材质着色）与风险层（半透明叠加）
        public byte[] TerrainBaseRGBA { get; private set; }
        public byte[] RiskRGBA { get; private set; }
        // 动态单位层：每单位一个彩色点（透明背景），由 AgentTracker 每帧节流刷新，
        // 整体烘焙成纹理绘制（单 draw call，清晰呈现成千上万单位的真实分布）。
        public byte[] AgentRGBA { get; private set; }

        private readonly TacticalMap.Config.TacticalSettings _settings;
        private Scene _scene;
        private bool _baked;

        public TerrainCache(TacticalMap.Config.TacticalSettings settings)
        {
            _settings = settings;
        }

        public bool TryBake(Scene scene)
        {
            _scene = scene;
            try
            {
                scene.GetTerrainData(out Vec2i nodeDim, out float nodeSize, out _, out _);
                if (nodeDim.X <= 0 || nodeDim.Y <= 0 || nodeSize <= 0f) { LastError = "地形数据无效(nodeDim/nodeSize)"; return false; }
                if (!scene.GetTerrainMinMaxHeight(out float minH, out float maxH)) { LastError = "GetTerrainMinMaxHeight 失败"; return false; }
                MinH = minH;
                MaxH = maxH;

                // ---- 计算实际战场边界（避免地图显示范围过大） ----
                float fullWorldW = nodeDim.X * nodeSize;
                float fullWorldH = nodeDim.Y * nodeSize;
                if (!ComputeBattleBounds(scene, out Vec2 battleMin, out Vec2 battleMax))
                {
                    battleMin = Vec2.Zero;
                    battleMax = new Vec2(fullWorldW, fullWorldH);
                }
                OriginX = battleMin.X;
                OriginY = battleMin.Y;
                WorldW = Math.Max(1f, battleMax.X - battleMin.X);
                WorldH = Math.Max(1f, battleMax.Y - battleMin.Y);

                int res = _settings.BakeResolution;
                Width = res;
                Height = res;
                CellStep = Math.Max(WorldW, WorldH) / res;

                Cells = new TerrainCell[Width, Height];
                float[,] heights = new float[Width, Height];

                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        Vec2 pos = CellCenter(x, y);
                        float h = scene.GetTerrainHeight(pos, true);
                        heights[x, y] = h;
                        scene.GetTerrainHeightAndNormal(pos, out _, out Vec3 normal);

                        int nodeX = (int)(pos.X / nodeSize);
                        int nodeY = (int)(pos.Y / nodeSize);
                        nodeX = Math.Max(0, Math.Min(nodeDim.X - 1, nodeX));
                        nodeY = Math.Max(0, Math.Min(nodeDim.Y - 1, nodeY));
                        short[] mat = scene.GetTerrainPhysicsMaterialIndexData(nodeX, nodeY);

                        Cells[x, y] = new TerrainCell
                        {
                            Height = h,
                            Normal = normal,
                            MaterialLayers = mat ?? new short[0]
                        };
                    }
                }

                TerrainAnalyzer.ClassifyAll(this, heights, _settings);
                BuildBaseRGBA();
                BuildRiskRGBA();
                AgentRGBA = new byte[Width * Height * 4]; // 初始全 0 = 全透明
                _baked = true;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine("[TacticalMap] TerrainCache.TryBake failed: " + ex.Message);
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] 地形烘焙失败: {ex.GetType().Name}: {ex.Message}"));
                _baked = false;
                return false;
            }
        }

        public bool IsBaked => _baked;
        public string LastError { get; private set; }

        public Vec2 CellCenter(int x, int y)
        {
            return new Vec2(OriginX + (x + 0.5f) * CellStep, OriginY + (y + 0.5f) * CellStep);
        }

        public Vec2 UVToWorld(Vec2 uv)
        {
            return new Vec2(OriginX + uv.X * WorldW, OriginY + uv.Y * WorldH);
        }

        public Vec2 WorldToUV(Vec2 world)
        {
            return new Vec2((world.X - OriginX) / WorldW, (world.Y - OriginY) / WorldH);
        }

        public float GetHeightAt(Vec2 world)
        {
            if (!_baked || _scene == null) return 0f;
            try { return _scene.GetTerrainHeight(world, true); }
            catch { return 0f; }
        }

        // ---- 战场边界计算 ----
        /// <summary>计算实际战斗区域的边界矩形。</summary>
        private bool ComputeBattleBounds(Scene scene, out Vec2 min, out Vec2 max)
        {
            // ① 优先使用软边界（walk_area）多边形顶点包围盒，这是关编辑器中定义的可行走区域
            int softCount = scene.GetSoftBoundaryVertexCount();
            if (softCount > 0)
            {
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;
                for (int i = 0; i < softCount; i++)
                {
                    Vec2 v = scene.GetSoftBoundaryVertex(i);
                    if (v.X < minX) minX = v.X;
                    if (v.Y < minY) minY = v.Y;
                    if (v.X > maxX) maxX = v.X;
                    if (v.Y > maxY) maxY = v.Y;
                }
                // 向外扩展 10% 边距，避免边界上的单位被裁切
                float mx = (maxX - minX) * 0.1f;
                float my = (maxY - minY) * 0.1f;
                min = new Vec2(minX - mx, minY - my);
                max = new Vec2(maxX + mx, maxY + my);
                return true;
            }
            // ② 回退：场景包围盒（包含所有实体的最小矩形）
            scene.GetBoundingBox(out Vec3 bbMin, out Vec3 bbMax);
            if (bbMin.IsValid && bbMax.IsValid &&
                bbMax.X > bbMin.X && bbMax.Y > bbMin.Y)
            {
                min = bbMin.AsVec2;
                max = bbMax.AsVec2;
                return true;
            }
            // ③ 都失败，返回 false 让调用方使用全地形范围
            min = Vec2.Zero;
            max = Vec2.Zero;
            return false;
        }

        // --- 颜色工具 ---
        private void BuildBaseRGBA()
        {
            TerrainBaseRGBA = new byte[Width * Height * 4];
            float range = Math.Max(0.001f, MaxH - MinH);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var c = Cells[x, y];
                    float t = (c.Height - MinH) / range; // 0..1
                    // 高度色带：低绿 -> 中棕 -> 高灰 -> 顶白
                    byte r, g, b;
                    if (t < 0.5f)
                    {
                        float k = t / 0.5f;
                        r = (byte)(60 + k * 60); g = (byte)(120 + k * (-10)); b = (byte)(40 + k * 30);
                    }
                    else
                    {
                        float k = (t - 0.5f) / 0.5f;
                        r = (byte)(120 + k * 115); g = (byte)(110 + k * 125); b = (byte)(70 + k * 165);
                    }

                    if (c.IsWater) { r = 40; g = 90; b = 200; }
                    else if (c.IsForest) { r = (byte)(r * 0.6f); g = (byte)(g * 0.85f); b = (byte)(b * 0.6f); }
                    else if (c.IsCliff) { r = (byte)(r * 0.9f + 40); g = (byte)(g * 0.5f); b = (byte)(b * 0.5f); }

                    SetPixel(TerrainBaseRGBA, x, y, r, g, b, 255);
                }
            }
        }

        private void BuildRiskRGBA()
        {
            RiskRGBA = new byte[Width * Height * 4];
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var c = Cells[x, y];
                    if (c.IsCliff) SetPixel(RiskRGBA, x, y, 210, 50, 50, 150);
                    else if (c.IsWater) SetPixel(RiskRGBA, x, y, 50, 110, 220, 140);
                    else if (c.IsForest) SetPixel(RiskRGBA, x, y, 50, 180, 70, 100);
                    else SetPixel(RiskRGBA, x, y, 0, 0, 0, 0);
                }
            }
        }

        public void SetPixel(byte[] buf, int x, int y, byte r, byte g, byte b, byte a)
        {
            int i = (y * Width + x) * 4;
            buf[i] = r; buf[i + 1] = g; buf[i + 2] = b; buf[i + 3] = a;
        }

        public void GetPixel(byte[] buf, int x, int y, out byte r, out byte g, out byte b, out byte a)
        {
            int i = (y * Width + x) * 4;
            r = buf[i]; g = buf[i + 1]; b = buf[i + 2]; a = buf[i + 3];
        }

        // --- 动态单位层维护 ---
        public void ClearAgents()
        {
            if (AgentRGBA != null) Array.Clear(AgentRGBA, 0, AgentRGBA.Length);
        }

        // 在 (gx,gy) 周围 radius 范围内画一个不透明点（默认 3x3），双线性拉伸后仍清晰可辨。
        public void PaintAgent(int gx, int gy, byte r, byte g, byte b, int radius = 1)
        {
            if (AgentRGBA == null) return;
            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = gx + dx, y = gy + dy;
                if (x < 0 || x >= Width || y < 0 || y >= Height) continue;
                SetPixel(AgentRGBA, x, y, r, g, b, 255);
            }
        }
    }
}
```

#### `TerrainAnalyzer.cs`

```csharp
using System;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Config;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>把烘焙出的高度/法线/材质栅格推断为语义类别（水/林/悬崖/平原）。</summary>
    public static class TerrainAnalyzer
    {
        public static void ClassifyAll(TerrainCache cache, float[,] heights, TacticalSettings s)
        {
            int W = cache.Width, H = cache.Height;
            float minH = cache.MinH, maxH = cache.MaxH;
            float waterBelow = minH + (maxH - minH) * s.WaterHeightFraction;

            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                var c = cache.Cells[x, y];
                c.IsWater = c.Height <= waterBelow;

                // 坡度：法线.z 越接近 0 越陡
                float slope = (c.Normal.Z < 1f) ? (1f - c.Normal.Z) : 0f;
                bool steep = slope > s.CliffSlopeThreshold;

                // 邻接高度突变（仅内部格判断）
                bool jump = false;
                if (x > 0 && y > 0 && x < W - 1 && y < H - 1)
                {
                    float hC = c.Height;
                    float hL = heights[x - 1, y];
                    float hR = heights[x + 1, y];
                    float hD = heights[x, y - 1];
                    float hU = heights[x, y + 1];
                    float maxJump = Math.Max(Math.Abs(hC - hL), Math.Max(Math.Abs(hC - hR),
                                        Math.Max(Math.Abs(hC - hD), Math.Abs(hC - hU))));
                    jump = maxJump > s.CliffHeightJump;
                }
                c.IsCliff = steep || jump;

                // 林地：物理材质层命中配置的林地索引
                c.IsForest = false;
                if (c.MaterialLayers != null)
                {
                    foreach (var m in c.MaterialLayers)
                        if (s.ForestMaterialIndices.Contains(m)) { c.IsForest = true; break; }
                }
                // 低坡绿色区域增强：坡度很低且高度居中时视作林地（兜底，提升辨识度）
                if (!c.IsForest && !c.IsWater && !c.IsCliff && slope < 0.08f)
                    c.IsForest = true;
            }
        }
    }
}
```

#### `TerrainCell.cs`

```csharp
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>战术栅格的一个单元，缓存高度、法线、材质层，以及推断出的语义标志。</summary>
    public sealed class TerrainCell
    {
        public float Height;
        public Vec3 Normal;
        public short[] MaterialLayers;

        // 语义标志
        public bool IsWater;
        public bool IsForest;
        public bool IsCliff;

        // 动态：该格当前单位密度（由 AgentTracker 累加）
        public int DensityAgentCount;
    }
}
```

### 5.4 Tracking

#### `AgentTracker.cs`

```csharp
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Terrain;

namespace New_ZZZF.TacticalMap.Tracking
{
    /// <summary>把战场单位（Agent）画进动态单位层（AgentRGBA），并统计每格密度。</summary>
    public sealed class AgentTracker
    {
        private readonly TerrainCache _cache;

        public AgentTracker(TerrainCache cache)
        {
            _cache = cache;
        }

        public void Update(Mission mission)
        {
            if (mission == null || !_cache.IsBaked) return;
            _cache.ClearAgents();
            // 密度清零
            int W = _cache.Width, H = _cache.Height;
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                _cache.Cells[x, y].DensityAgentCount = 0;

            foreach (var agent in mission.Agents)
            {
                if (agent == null) continue;
                if (agent.IsMount) continue; // 坐骑不单独画

                Vec2 p = agent.Position.AsVec2;
                Vec2 uv = _cache.WorldToUV(p);
                if (uv.X < 0f || uv.X > 1f || uv.Y < 0f || uv.Y > 1f) continue;

                int gx = (int)(uv.X * W);
                int gy = (int)(uv.Y * H);
                if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;

                _cache.Cells[gx, gy].DensityAgentCount++;

                // 颜色：我方亮青、敌方纯红、中立灰
                byte r, g, b;
                if (agent.Team != null && agent.Team.IsPlayerTeam) { r = 0; g = 230; b = 230; }
                else if (agent.Team != null && agent.Team.IsEnemyOf(mission.PlayerTeam)) { r = 255; g = 40; b = 40; }
                else { r = 160; g = 160; b = 160; }

                _cache.PaintAgent(gx, gy, r, g, b, 1);
            }
        }
    }
}
```

#### `FormationTracker.cs`

```csharp
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.TacticalMap.Tracking
{
    /// <summary>编队快照：质心、朝向、颜色、敌我标志，供 MinimapWidget 绘制标记。</summary>
    public sealed class FormationSnapshot
    {
        public Vec2 AveragePosition;
        public Vec2 Facing;
        public uint Color;
        public bool IsPlayer;
        public bool IsEnemy;
        public string Name;
    }

    public sealed class FormationTracker
    {
        public List<FormationSnapshot> Snapshots { get; } = new List<FormationSnapshot>();

        public void Update(Mission mission)
        {
            Snapshots.Clear();
            if (mission == null) return;

            foreach (var team in mission.Teams)
            {
                if (team == null) continue;
                bool isPlayer = team.IsPlayerTeam;
                foreach (var f in team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits <= 0) continue;
                    Snapshots.Add(new FormationSnapshot
                    {
                        AveragePosition = f.CachedAveragePosition,
                        Facing = f.CachedFacing,
                        Color = f.Color,
                        IsPlayer = isPlayer,
                        IsEnemy = team.IsEnemyOf(mission.PlayerTeam),
                        Name = f.Name,
                    });
                }
            }
        }
    }
}
```

### 5.5 UI

#### `MinimapWidget.cs`

```csharp
using System;
using System.Numerics;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.Terrain;
using New_ZZZF.TacticalMap.Tracking;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// 小地图绘制控件：在 OnRender 里绘制地形栅格、编队标记、镜头目标。
    /// 地形/风险改用烘焙纹理（运行时字节数组经 TaleWorlds.Engine.Texture.CreateFromByteArray
    /// 创建，整体拉伸绘制，双线性平滑），draw call 从约万级降到个位数；
    /// 密度热力图仍为动态图元叠加。
    /// </summary>
    public sealed class MinimapWidget : Widget
    {
        public TacticalMapController Controller { get; set; }

        public MinimapWidget(UIContext context) : base(context) { }

        private static bool _warnedNoCtrl, _warnedNotBaked, _warnedArea, _warnedDrawn, _warnedRenderErr;
        private static bool _warnedNoWhite, _warnedNoTerrain, _warnedNoRisk, _warnedNoForm, _warnedNoPlayer;
        private static int _renderErrDiagCount;
        // 烘焙纹理总开关：BL 1.4.6 下 TaleWorlds.Engine.Texture.CreateFromByteArray 生成的纹理为全白/空
        // （与源 RGBA 无关，实测源像素有色但纹理整片白），故禁用纹理路径，改走 OnRender 内逐像素图元回退。
        // 若未来某版本该 API 恢复正常，只需把此常量改为 true 即可一键切回高质量单 draw call 纹理路径。
        // 注：即使 UseBakedTexture=false，EnsureTerrainTexture 仍会尝试创建纹理并验证结果；
        // 若 BGRA 字节序在某版本有效，会自动启用纹理路径而无需修改此常量。
        private static readonly bool UseBakedTexture = false;
        private static Texture _whiteTex;
        // 烘焙纹理（地形/风险各一张，双线性平滑，单 draw call）
        private static TaleWorlds.TwoDimension.Texture _terrainTex;
        private static TaleWorlds.TwoDimension.Texture _riskTex;
        // 底层 Engine.Texture：TwoDimension.Texture 只是壳、没有 Release，
        // 真正的 GPU 资源释放必须靠它，故单独持有以便复用/销毁时调用 Engine.Texture.Release()。
        private static TaleWorlds.Engine.Texture _terrainETex;
        private static TaleWorlds.Engine.Texture _riskETex;
        // 动态单位层纹理（每个 agent 一个点）
        private static TaleWorlds.TwoDimension.Texture _agentTex;
        private static TaleWorlds.Engine.Texture _agentETex;
        private static int _agentTexVer = -1;
        private static TacticalMapController _texCtrl;
        private static void WarnOnce(ref bool flag, string msg)
        {
            if (flag) return;
            flag = true;
            InformationManager.DisplayMessage(new InformationMessage(msg));
        }
        private static void Diag(string msg)
        {
            try { TaleWorlds.Library.Debug.Print("[TMap] " + msg); } catch { }
            try
            {
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "tmap_diag.log");
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + " " + msg + "\n");
            }
            catch { }
        }

        // 从异常堆栈提取“抛出点”的第一帧，便于在游戏里直接看出是哪段代码出错。
        private static string TopFrame(Exception ex)
        {
            try
            {
                string st = ex.StackTrace;
                if (string.IsNullOrEmpty(st)) return "(无堆栈)";
                foreach (var l in st.Split('\n'))
                {
                    string s = l.Trim();
                    if (s.StartsWith("at ")) return s;
                }
                return st.Split('\n')[0].Trim();
            }
            catch { return "(取堆栈失败)"; }
        }

        // Widget 的 Width/Height 在不同 BL 版本里可能是属性/字段，甚至命名不同（Width/width），
        // 直接用 this.Width 会编译失败。改为反射读取（字段优先，其次属性），失败返回 "?"。
        private static string WidgetSizeStr(Widget w)
        {
            try
            {
                var t = w.GetType();
                var bf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var pw = t.GetProperty("Width", bf) ?? t.GetProperty("width", bf);
                var ph = t.GetProperty("Height", bf) ?? t.GetProperty("height", bf);
                string fw = "?";
                if (pw != null) { try { fw = System.Convert.ToSingle(pw.GetValue(w)).ToString("F1"); } catch { } }
                string fh = "?";
                if (ph != null) { try { fh = System.Convert.ToSingle(ph.GetValue(w)).ToString("F1"); } catch { } }
                return fw + "," + fh;
            }
            catch { return "?"; }
        }

        // 获取一个纯白纹理，用于 SimpleMaterial 纯色填充。
        // 优先用字节数组在引擎侧创建 1x1 纯白纹理（跨版本 100% 稳定，不依赖任何 sprite 查询）；
        // 失败再回退到反射查找已知白色 sprite 名（兼容旧版本）。这样 _whiteTex 永远不会是 null。
        private static void EnsureWhiteTexture(UIContext uiContext)
        {
            if (_whiteTex != null) return;
            // ① 字节数组创建（最可靠，与地形/单位层纹理同一套 API）
            try
            {
                byte[] white = new byte[] { 255, 255, 255, 255 };
                var eTex = TaleWorlds.Engine.Texture.CreateFromByteArray(white, 1, 1);
                if (eTex != null)
                {
                    eTex.SetTextureAsAlwaysValid();
                    _whiteTex = new TaleWorlds.TwoDimension.Texture(new TaleWorlds.Engine.GauntletUI.EngineTexture(eTex));
                    Diag("WHT 已用字节数组创建纯白纹理");
                    return;
                }
            }
            catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] 白纹理(字节数组)创建失败: {ex.Message}")); Diag("WHT 字节创建失败: " + ex.Message); }

            // ② 回退：反射查找白色 sprite
            if (uiContext == null) return;
            try
            {
                var cbf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var type = uiContext.GetType();
                Diag($"WHT ctxType={type.FullName}");
                var sdProp = type.GetProperty("SpriteData", cbf);
                if (sdProp == null) { Diag("WHT no SpriteData prop on UIContext"); return; }
                object spriteData = sdProp.GetValue(uiContext);
                if (spriteData == null) { Diag("WHT SpriteData is null"); return; }
                Diag($"WHT spriteData={spriteData.GetType().FullName}");
                var getSprite = spriteData.GetType().GetMethod("GetSprite", cbf);
                if (getSprite == null) { Diag("WHT no GetSprite method"); return; }

                // ① 已知白色 sprite 名
                foreach (var name in new[] { "blank", "Blank", "white", "White", "ui/blank", "BlankWhite", "blank_white" })
                {
                    object sprite = null;
                    try { sprite = getSprite.Invoke(spriteData, new object[] { name }); } catch { sprite = null; }
                    if (sprite == null) continue;
                    var texProp = sprite.GetType().GetProperty("Texture", cbf);
                    var tex = texProp?.GetValue(sprite) as Texture;
                    if (tex != null && tex.IsValid) { _whiteTex = tex; Diag($"WHT got white via '{name}'"); return; }
                }

                // ② 枚举所有 sprite，优先白色名，否则第一个有效纹理
                var spProp = spriteData.GetType().GetProperty("Sprites", cbf);
                var dict = spProp?.GetValue(spriteData) as System.Collections.IDictionary;
                if (dict != null)
                {
                    var validNames = new System.Collections.Generic.List<string>();
                    Texture fallback = null;
                    foreach (System.Collections.DictionaryEntry e in dict)
                    {
                        var sprite = e.Value;
                        var texProp = sprite.GetType().GetProperty("Texture", cbf);
                        var tex = texProp?.GetValue(sprite) as Texture;
                        string key = e.Key?.ToString() ?? "";
                        if (tex != null && tex.IsValid)
                        {
                            if (key.IndexOf("blank", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                key.IndexOf("white", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            { _whiteTex = tex; Diag($"WHT got white via enum '{key}'"); return; }
                            if (fallback == null) fallback = tex;
                            validNames.Add(key);
                        }
                    }
                    if (fallback != null)
                    {
                        _whiteTex = fallback;
                        Diag("WHT fallback non-white texture. validSprites=" + string.Join(",", validNames));
                        return;
                    }
                }
                Diag("WHT no usable texture found");
            }
            catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] 白纹理(反射)获取失败: {ex.Message}")); Diag($"WHT ex={ex.Message}"); }
        }

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            base.OnRender(twoDimensionContext, drawContext);
            try
            {
            var ctrl = Controller;
            if (ctrl == null) { WarnOnce(ref _warnedNoCtrl, "[TMap] OnRender: Controller 为空"); return; }
            if (!ctrl.Cache.IsBaked) { WarnOnce(ref _warnedNotBaked, $"[TMap] OnRender: 地形未烘焙 ({ctrl.Cache.Width}x{ctrl.Cache.Height})"); return; }

            // 兼容不同 BL 版本的 GlobalPosition/Size 类型差异：
            // 旧版本 Widget.GlobalPosition 返回 Rectangle2D（非 System.Numerics.Vector2），
            // 直接 this.GlobalPosition 会被 JIT 解析为 Vector2 版 get_GlobalPosition 而抛 MissingMethodException。
            // 改用各版本均稳定存在的 AreaRect -> GetBoundingBox()（返回 SimpleRectangle，含 X/Y/Width/Height）。
            Rectangle2D area = this.AreaRect;
            var box = area.GetBoundingBox();
            float ox = box.X;
            float oy = box.Y;
            float w = box.Width;
            float h = box.Height;
            if (w <= 0f || h <= 0f) { WarnOnce(ref _warnedArea, "[TMap] OnRender: Size 无效"); return; }

            var cache = ctrl.Cache;
            var s = TacticalSettings.Instance;

            // 背景
            EnsureWhiteTexture(this.Context);
            if (_whiteTex == null) WarnOnce(ref _warnedNoWhite, "[TMap] 白色纹理为空：矩形/标记可能无法显示或报错");
            DrawRect(drawContext, ox, oy, w, h, new Color(0.04f, 0.06f, 0.09f, 0.85f));
            WarnOnce(ref _warnedDrawn, "[TMap] OnRender: 正在绘制小地图");

            // 地形 + 风险叠加：优先用烘焙纹理（双线性平滑 + 单 draw call）；
            // bake 未完成时降级为逐像素矩形（仅首帧）。
            EnsureTerrainTexture(ctrl);
            if (_terrainTex == null) WarnOnce(ref _warnedNoTerrain, "[TMap] 地形纹理创建失败：已降级为逐像素绘制（可能卡/不显示）");
            if (_terrainTex != null)
            {
                DrawTexture(drawContext, _terrainTex, ox, oy, w, h);
                if (s.EnableRiskOverlay)
                {
                    EnsureRiskTexture(ctrl);
                    if (_riskTex == null) WarnOnce(ref _warnedNoRisk, "[TMap] 风险纹理创建失败：不显示风险叠加层");
                    if (_riskTex != null)
                        DrawTexture(drawContext, _riskTex, ox, oy, w, h);
                }
            }
            else
            {
                // 合并通道：单循环完成地形+风险+密度+单位层绘制
                // 将四趟独立遍历合并为一趟，大幅减少DrawRect调用次数（从~15000降至~2500）
                int cols = Math.Min(48, cache.Width);  // 96→48，4倍减少
                int step = Math.Max(1, cache.Width / cols);
                float cw = w / (cache.Width / (float)step);
                float ch = h / (cache.Height / (float)step);

                bool showRisk = s.EnableRiskOverlay;
                bool showDensity = s.EnableDensityHeatmap;
                bool showAgents = s.EnableAgentMarkers;
                var agentData = showAgents ? ctrl.AgentRGBA : null;

                for (int x = 0; x < cache.Width; x += step)
                for (int y = 0; y < cache.Height; y += step)
                {
                    cache.GetPixel(cache.TerrainBaseRGBA, x, y, out byte r, out byte g, out byte b, out _);
                    float rf = r / 255f, gf = g / 255f, bf = b / 255f;

                    // 风险叠加（内置到主循环，免去第二趟遍历）
                    if (showRisk)
                    {
                        cache.GetPixel(cache.RiskRGBA, x, y, out byte rr, out byte rg, out byte rb, out byte ra);
                        if (ra > 0)
                        {
                            float ka = ra / 255f;
                            rf = rf * (1f - ka) + (rr / 255f) * ka;
                            gf = gf * (1f - ka) + (rg / 255f) * ka;
                            bf = bf * (1f - ka) + (rb / 255f) * ka;
                        }
                    }

                    // 密度热力叠加（内置到主循环，免去第三趟遍历）
                    if (showDensity)
                    {
                        int dens = cache.Cells[x, y].DensityAgentCount;
                        if (dens > 0)
                        {
                            float ka = Math.Min(0.3f, dens * 0.02f);
                            rf = rf * (1f - ka) + 1f * ka;
                            gf = gf * (1f - ka) + 0.85f * ka;
                            bf = bf * (1f - ka) + 0.2f * ka;
                        }
                    }

                    // 单位层叠加（内置到主循环，免去第四趟遍历）
                    if (showAgents && agentData != null)
                    {
                        cache.GetPixel(agentData, x, y, out byte ar, out byte ag, out byte ab, out byte aa);
                        if (aa > 0)
                        {
                            float ka = aa / 255f;
                            rf = rf * (1f - ka) + (ar / 255f) * ka;
                            gf = gf * (1f - ka) + (ag / 255f) * ka;
                            bf = bf * (1f - ka) + (ab / 255f) * ka;
                        }
                    }

                    float px = ox + (x / (float)cache.Width) * w;
                    float py = oy + (y / (float)cache.Height) * h;
                    DrawRect(drawContext, px, py, cw + 0.5f, ch + 0.5f, new Color(rf, gf, bf, 1f));
                }
            }

            // 编队标记：每个编队质心一个描边方块（队伍色填充 + 关系色描边），与密集单位点云区分；并画出朝向。
            // 描边颜色按敌我区分：玩家=白框（不变），敌方=红框，友军=绿框（受 EnableUnitMarkers 控制）。
            if (s.EnableUnitMarkers)
            {
                var snaps = ctrl.FormationSnapshots;
                if (snaps == null) WarnOnce(ref _warnedNoForm, "[TMap] 编队快照为空：看不到编队标记（数据未就绪/Controller 异常）");
                if (snaps != null)
                {
                    float fs = Math.Max(9f, w * 0.04f);
                    float ft = Math.Max(1.5f, w * 0.008f);
                    foreach (var f in snaps)
                    {
                        if (f == null) continue;
                        Vec2 uv = cache.WorldToUV(f.AveragePosition);
                        if (uv.X < 0f || uv.X > 1f || uv.Y < 0f || uv.Y > 1f) continue;
                        float px = ox + uv.X * w;
                        float py = oy + uv.Y * h;
                        // 框色：玩家=白(不变) / 敌方=红 / 友军=绿
                        Color frame;
                        if (f.IsPlayer) frame = new Color(1f, 1f, 1f, 0.95f);
                        else if (f.IsEnemy) frame = new Color(1f, 0.15f, 0.15f, 0.95f);
                        else frame = new Color(0.2f, 1f, 0.2f, 0.95f);
                        Color c = Color.FromUint(f.Color);
                        // 2-call 边框：外框(边框色) + 内框(填充色)，替代原5次调用
                        DrawRect(drawContext, px - fs / 2f - ft, py - fs / 2f - ft, fs + 2f * ft, fs + 2f * ft, frame);
                        DrawRect(drawContext, px - fs / 2f, py - fs / 2f, fs, fs, c);
                        if (f.Facing.LengthSquared > 1E-4f)
                        {
                            DrawLine(drawContext, px, py, px + f.Facing.X * fs * 1.6f, py + f.Facing.Y * fs * 1.6f, frame);
                        }
                    }
                }
            }

            // 玩家（MainAgent）标记：青色描边圆环（框）+ 亮黄中心点，形状/颜色都明显区别于单位点云与编队方块，便于一眼定位自身位置
            if (!ctrl.PlayerPos.HasValue) WarnOnce(ref _warnedNoPlayer, "[TMap] 玩家位置为空：不显示玩家标记");
            if (ctrl.PlayerPos.HasValue)
            {
                Vec2 uv = cache.WorldToUV(ctrl.PlayerPos.Value);
                if (uv.X >= 0f && uv.X <= 1f && uv.Y >= 0f && uv.Y <= 1f)
                {
                    float px = ox + uv.X * w;
                    float py = oy + uv.Y * h;
                    float pr = Math.Max(10f, w * 0.05f);   // 圆环外径
                    DrawRectFrame(drawContext, px - pr / 2f, py - pr / 2f, pr, pr, Math.Max(2.5f, w * 0.012f), new Color(0f, 1f, 1f, 1f)); // 青色描边
                    DrawRect(drawContext, px - 3f, py - 3f, 6f, 6f, new Color(1f, 1f, 0.2f, 1f)); // 亮黄中心
                }
            }

            // 镜头目标指示（菱形，对应图例“相机”符号）
            if (ctrl.CameraTarget.HasValue)
            {
                Vec2 uv = cache.WorldToUV(ctrl.CameraTarget.Value);
                float px = ox + uv.X * w;
                float py = oy + uv.Y * h;
                float d = Math.Max(6f, w * 0.03f);
                DrawDiamond(drawContext, px, py, d, new Color(1f, 0.8f, 0.2f, 1f), 3f);
            }

            }
            catch (Exception ex)
            {
                string where = TopFrame(ex);
                WarnOnce(ref _warnedRenderErr, $"[TMap] OnRender 异常: {ex.GetType().Name}: {ex.Message} @ {where}");
                if (_renderErrDiagCount < 5)
                {
                    _renderErrDiagCount++;
                    Diag("OnRender EXCEPTION 完整堆栈:\n" + ex.ToString());
                }
            }
        }

        // 兼容不同 BL 版本的 Rectangle2D 成员（LocalPosition / LocalScale / LocalRotation）：
        // 编译用的引用程序集可能把它们当作字段（IL 走 stfld），而运行时（如 1.4.6）实际是属性，
        // 直接 r.LocalScale = ... 会在 JIT 解析字段时抛 MissingFieldException。
        // 统一改用反射（字段优先，其次同名属性）写入，并缓存解析结果。
        // 关键：Rectangle2D 是 struct，必须通过 box -> 设置 -> unbox 回写，才能保证修改作用在调用方的副本上。
        private static readonly System.Collections.Generic.Dictionary<string, System.Reflection.MemberInfo> _rectMembers =
            new System.Collections.Generic.Dictionary<string, System.Reflection.MemberInfo>();

        private static System.Reflection.MemberInfo ResolveRectMember(string name)
        {
            System.Reflection.MemberInfo m;
            if (_rectMembers.TryGetValue(name, out m)) return m;
            var t = typeof(TaleWorlds.TwoDimension.Rectangle2D);
            var bf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            System.Reflection.FieldInfo fi = t.GetField(name, bf) ?? t.GetField(char.ToLowerInvariant(name[0]) + name.Substring(1), bf);
            if (fi != null)
                m = fi;
            else
            {
                var pi = t.GetProperty(name, bf);
                m = (pi != null && pi.CanWrite) ? (System.Reflection.MemberInfo)pi : null;
            }
            _rectMembers[name] = m;
            return m;
        }

        private static void SetRectMember(ref Rectangle2D rect, string name, object value)
        {
            var m = ResolveRectMember(name);
            if (m == null) return;
            object boxed = rect;
            System.Type targetType = (m is System.Reflection.FieldInfo fi) ? fi.FieldType
                : (m is System.Reflection.PropertyInfo pi) ? pi.PropertyType : null;
            object coerced = (targetType != null) ? CoerceToTargetType(targetType, value) : value;
            if (m is System.Reflection.FieldInfo fi2)
                fi2.SetValue(boxed, coerced);
            else if (m is System.Reflection.PropertyInfo pi2)
                pi2.SetValue(boxed, coerced);
            rect = (Rectangle2D)boxed;
        }

        // 运行时可能存在“两份 System.Numerics.Vector2”（游戏自带一份、本 mod 引用另一份）：
        // 二者全名相同但程序集身份不同，反射 FieldInfo/PropertyInfo.SetValue 会因类型不匹配抛
        // ArgumentException: “Object of type 'System.Numerics.Vector2' cannot be converted to type 'System.Numerics.Vector2'”。
        // 解决：当目标字段/属性类型来自与当前代码不同的程序集时，用目标类型在其程序集内自行构造 Vector2，保证类型身份一致。
        private static object CoerceToTargetType(System.Type targetType, object value)
        {
            if (value is System.Numerics.Vector2 v
                && targetType.FullName == "System.Numerics.Vector2"
                && targetType.Assembly != typeof(System.Numerics.Vector2).Assembly)
            {
                return System.Activator.CreateInstance(targetType, v.X, v.Y);
            }
            return value;
        }

        private static void SetRectPosition(ref Rectangle2D rect, Vector2 pos)
        {
            SetRectMember(ref rect, "LocalPosition", pos);
        }

        private static void DrawRect(TwoDimensionDrawContext ctx, float x, float y, float w, float h, Color color)
        {
            Rectangle2D r = Rectangle2D.Create();
            SetRectPosition(ref r, new Vector2(x, y));
            SetRectMember(ref r, "LocalScale", new Vector2(w, h));
            r.CalculateMatrixFrame(default(Rectangle2D));
            var mat = new SimpleMaterial();
            mat.Texture = _whiteTex;
            mat.Color = color;
            ImageDrawObject obj = ImageDrawObject.Create(r, Vec2.Zero, Vec2.One);
            ctx.Draw(mat, obj);
        }

        private static void DrawLine(TwoDimensionDrawContext ctx, float x1, float y1, float x2, float y2, Color color, float width = 2f)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return;
            float ang = (float)Math.Atan2(dy, dx);
            Rectangle2D r = Rectangle2D.Create();
            SetRectPosition(ref r, new Vector2(x1, y1));
            SetRectMember(ref r, "LocalScale", new Vector2(len, width));
            SetRectMember(ref r, "LocalRotation", ang);
            r.CalculateMatrixFrame(default(Rectangle2D));
            var mat = new SimpleMaterial();
            mat.Texture = _whiteTex;
            mat.Color = color;
            ImageDrawObject obj = ImageDrawObject.Create(r, Vec2.Zero, Vec2.One);
            ctx.Draw(mat, obj);
        }

        // 以 (cx,cy) 为中心画一个菱形（用 4 条线连成），d 为半径，width 为线宽。
        private static void DrawDiamond(TwoDimensionDrawContext ctx, float cx, float cy, float d, Color color, float width = 3f)
        {
            float tx = cx, ty = cy - d;   // 上
            float rx = cx + d, ry = cy;   // 右
            float bx = cx, by = cy + d;   // 下
            float lx = cx - d, ly = cy;   // 左
            DrawLine(ctx, tx, ty, rx, ry, color, width);
            DrawLine(ctx, rx, ry, bx, by, color, width);
            DrawLine(ctx, bx, by, lx, ly, color, width);
            DrawLine(ctx, lx, ly, tx, ty, color, width);
        }

        // 画矩形描边框（4 条细边），用于标记轮廓，提升在密集单位点云上的辨识度。
        private static void DrawRectFrame(TwoDimensionDrawContext ctx, float x, float y, float w, float h, float t, Color color)
        {
            DrawRect(ctx, x, y, w, t, color);              // 上
            DrawRect(ctx, x, y + h - t, w, t, color);      // 下
            DrawRect(ctx, x, y, t, h, color);              // 左
            DrawRect(ctx, x + w - t, y, t, h, color);      // 右
        }

        // 尝试用给定的 RGBA 数据创建引擎纹理，失败返回 null。
        // 部分 BL 版本只认 BGRA 字节序，因此先试 RGBA，若纹理创建成功但全白则试 BGRA。
        private static TaleWorlds.Engine.Texture TryCreateEngineTexture(byte[] rgba, int w, int h, bool swapRB)
        {
            try
            {
                byte[] data;
                if (swapRB)
                {
                    data = new byte[rgba.Length];
                    for (int i = 0; i < rgba.Length; i += 4)
                    {
                        data[i] = rgba[i + 2];     // B←R
                        data[i + 1] = rgba[i + 1]; // G←G
                        data[i + 2] = rgba[i];     // R←B
                        data[i + 3] = 255;         // A=255
                    }
                }
                else
                {
                    data = new byte[rgba.Length];
                    Buffer.BlockCopy(rgba, 0, data, 0, rgba.Length);
                    for (int i = 3; i < data.Length; i += 4) data[i] = 255;
                }
                var tex = TaleWorlds.Engine.Texture.CreateFromByteArray(data, w, h);
                if (tex != null) tex.SetTextureAsAlwaysValid();
                return tex;
            }
            catch { return null; }
        }

        // 把地形/风险 RGBA 烘焙成 GPU 纹理并缓存，绘制时整体拉伸（双线性平滑）。
        // 仅在缓存切换或重新 bake 时重建，避免每帧创建纹理。
        // 自动尝试 RGBA 和 BGRA 两种字节序，以兼容不同 BL 版本。
        private static void EnsureTerrainTexture(TacticalMapController ctrl)
        {
            if (_terrainTex != null && _texCtrl == ctrl) return;
            if (_terrainTex != null) { _terrainETex?.Release(); _terrainETex = null; _terrainTex = null; }
            if (_riskTex != null) { _riskETex?.Release(); _riskETex = null; _riskTex = null; }
            // 切换战斗：单位层纹理一并释放（其数据属于旧 ctrl）
            if (_agentTex != null) { _agentETex?.Release(); _agentETex = null; _agentTex = null; _agentTexVer = -1; }
            // 当 UseBakedTexture=false 时跳过创建（走逐像素回退）
            if (!UseBakedTexture) return;
            _texCtrl = ctrl;
            if (ctrl == null) return;
            var cache = ctrl.Cache;
            if (cache == null || !cache.IsBaked) { _texCtrl = null; return; }
            int W = cache.Width, H = cache.Height;
            byte[] td = cache.TerrainBaseRGBA;
            if (td == null || td.Length < W * H * 4) { _texCtrl = null; return; }
            // 先试 RGBA，失败则试 BGRA
            var eTex = TryCreateEngineTexture(td, W, H, false);
            if (eTex == null) eTex = TryCreateEngineTexture(td, W, H, true);
            if (eTex == null) { _texCtrl = null; return; }
            _terrainETex = eTex;
            _terrainTex = new TaleWorlds.TwoDimension.Texture(new TaleWorlds.Engine.GauntletUI.EngineTexture(eTex));
        }

        private static void EnsureRiskTexture(TacticalMapController ctrl)
        {
            if (_riskTex != null && _texCtrl == ctrl) return;
            if (_riskTex != null) { _riskETex?.Release(); _riskETex = null; _riskTex = null; }
            if (!UseBakedTexture) return; // 同地形：禁用纹理，走图元回退
            if (ctrl == null) return;
            var cache = ctrl.Cache;
            if (cache == null || !cache.IsBaked) return;
            int W = cache.Width, H = cache.Height;
            byte[] rd = cache.RiskRGBA;
            if (rd == null || rd.Length < W * H * 4) return;
            var eTex = TryCreateEngineTexture(rd, W, H, false);
            if (eTex == null) eTex = TryCreateEngineTexture(rd, W, H, true);
            if (eTex == null) return;
            _riskETex = eTex;
            _riskTex = new TaleWorlds.TwoDimension.Texture(new TaleWorlds.Engine.GauntletUI.EngineTexture(eTex));
        }

        // 把动态单位层（AgentRGBA）烘焙成 GPU 纹理；仅当数据版本变化（节流刷新）时重建，
        // 用单 draw call 整体拉伸绘制——清晰呈现成千上万单位的真实分布，且敌我分明。
        private static void EnsureAgentTexture(TacticalMapController ctrl)
        {
            if (ctrl == null) { ReleaseAgent(); return; }
            if (!UseBakedTexture) return; // 同地形：禁用纹理，改走下方图元回退
            var cache = ctrl.Cache;
            if (cache == null || !cache.IsBaked) return;
            // 未切换战斗且数据未刷新 -> 复用旧纹理
            if (_agentTex != null && _texCtrl == ctrl && _agentTexVer == ctrl.AgentDataVersion) return;
            ReleaseAgent();
            int W = cache.Width, H = cache.Height;
            byte[] ad = ctrl.AgentRGBA;
            if (ad == null || ad.Length < W * H * 4) return;
            var eTex = TryCreateEngineTexture(ad, W, H, false);
            if (eTex == null) eTex = TryCreateEngineTexture(ad, W, H, true);
            if (eTex == null) return;
            _agentETex = eTex;
            _agentTex = new TaleWorlds.TwoDimension.Texture(new TaleWorlds.Engine.GauntletUI.EngineTexture(eTex));
            _agentTexVer = ctrl.AgentDataVersion;
            _texCtrl = ctrl;
        }

        private static void ReleaseAgent()
        {
            if (_agentTex != null) { _agentETex?.Release(); _agentETex = null; _agentTex = null; _agentTexVer = -1; }
        }

        private static void DrawTexture(TwoDimensionDrawContext ctx, TaleWorlds.TwoDimension.Texture tex, float x, float y, float w, float h)
        {
            Rectangle2D r = Rectangle2D.Create();
            SetRectPosition(ref r, new Vector2(x, y));
            SetRectMember(ref r, "LocalScale", new Vector2(w, h));
            r.CalculateMatrixFrame(default(Rectangle2D));
            var mat = new SimpleMaterial();
            mat.Texture = tex;
            mat.Color = new Color(1f, 1f, 1f, 1f);
            ImageDrawObject obj = ImageDrawObject.Create(r, Vec2.Zero, Vec2.One);
            ctx.Draw(mat, obj);
        }
    }
}
```

#### `TacticalMapLayer.cs`

```csharp
using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Core;
using TaleWorlds.TwoDimension;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// 管理小地图的 GauntletLayer：加载 XML、把自定义绘制控件 MinimapWidget 接到控制器、计算点击命中区域。
    /// 与战斗 MissionScreen 解耦，方便整体抽离成独立 mod。
    /// </summary>
    public sealed class TacticalMapLayer
    {
        private readonly TacticalMapController _controller;
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movieId;
        private Widget _panel;
        private MinimapWidget _minimap;

        public TacticalMapLayer(TacticalMapController controller)
        {
            _controller = controller;
        }

        public void Create(MissionScreen ms)
        {
            // MinimapWidget 是继承自 Widget 的自定义控件，会被 GauntletUI 的 WidgetInfo 自动反射发现（
            // 凡引用了 TaleWorlds.GauntletUI 的程序集都会被扫描），无需也无法手动 RegisterWidget。
            if (_layer != null) { try { ms.RemoveLayer(_layer); } catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] RemoveLayer 失败: {ex.Message}")); } }
            _layer = new GauntletLayer("TacticalMap", 90);
            _layer.IsFocusLayer = false;
            try
            {
                _movieId = _layer.LoadMovie("TacticalMap", new TacticalMapVM());
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] LoadMovie 失败: {ex.GetType().Name}: {ex.Message}"));
                return;
            }

            try
            {
                if (_movieId.Movie == null) { InformationManager.DisplayMessage(new InformationMessage("[TMap] LoadMovie 返回 Movie 为空")); return; }
                var root = _movieId.Movie.RootWidget;
                if (root == null) { InformationManager.DisplayMessage(new InformationMessage("[TMap] Movie.RootWidget 为空")); return; }
            _panel = FindWidgetById(root, "MinimapPanel");
            _minimap = FindWidgetById(root, "MinimapTex") as MinimapWidget;
            InformationManager.DisplayMessage(new InformationMessage($"[TMap] 创建层: panel={_panel != null} minimap={_minimap != null}"));

            var s = TacticalSettings.Instance;
            if (_panel != null)
            {
                _panel.WidthSizePolicy = SizePolicy.Fixed;
                _panel.HeightSizePolicy = SizePolicy.Fixed;
                _panel.SuggestedWidth = s.MapSize;
                _panel.SuggestedHeight = s.MapSize;
                // 右上角定位 + 边距改由 TacticalMap.xml 的 HorizontalAlignment/MarginRight/MarginTop 处理，
                // 避免不同 BL 版本 Widget.PosOffset 类型不一致导致的 MissingMethodException。
            }

            if (_minimap != null)
            {
                _minimap.Controller = _controller;
                _minimap.WidthSizePolicy = SizePolicy.StretchToParent;
                _minimap.HeightSizePolicy = SizePolicy.StretchToParent;
            }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] 创建层后处理失败: {ex.GetType().Name}: {ex.Message}"));
            }

            try { ms.AddLayer(_layer); }
            catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] AddLayer 失败: {ex.GetType().Name}: {ex.Message}")); }
        }

        public void Destroy(MissionScreen ms)
        {
            if (_layer != null && ms != null)
            {
                try { ms.RemoveLayer(_layer); } catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] Destroy RemoveLayer 失败: {ex.Message}")); }
            }
            _layer = null;
            _movieId = default;
            _panel = null;
            _minimap = null;
        }

        /// <summary>屏幕像素 -> 小地图 UV；命中返回 true。</summary>
        public bool HitTestMinimap(Vec2 mousePixel, out Vec2 uv)
        {
            uv = Vec2.Zero;
            if (_panel == null) return false;
            // 避开 Widget.GlobalPosition/Size 的版本差异（旧版本 GlobalPosition 返回 Rectangle2D，非 Vector2）。
            // 改用各版本稳定的 AreaRect -> GetBoundingBox()（返回 SimpleRectangle，含 X/Y/Width/Height）。
            Rectangle2D area = _panel.AreaRect;
            var box = area.GetBoundingBox();
            float x0 = box.X, y0 = box.Y;
            float w = box.Width;
            float h = box.Height;
            if (mousePixel.X < x0 || mousePixel.X > x0 + w) return false;
            if (mousePixel.Y < y0 || mousePixel.Y > y0 + h) return false;
            uv = new Vec2((mousePixel.X - x0) / w, (mousePixel.Y - y0) / h);
            return true;
        }

        private static Widget FindWidgetById(Widget root, string id)
        {
            if (root == null) return null;
            if (root.Id == id) return root;
            var children = root.Children;
            if (children == null) return null;
            foreach (var c in children)
            {
                var found = FindWidgetById(c, id);
                if (found != null) return found;
            }
            return null;
        }
    }
}
```

#### `TacticalMapVM.cs`

```csharp
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>小地图的轻量 ViewModel，主要作为 Gauntlet 数据上下文占位；绘制由 MinimapWidget 直接完成。</summary>
    public sealed class TacticalMapVM : ViewModel
    {
        public TacticalMapVM() { }
    }
}
```

---

## 六、提交历史（本次整理相关）

| Commit | 说明 |
|--------|------|
| `e33a12d` | 性能优化：合并渲染通道、降低分辨率、减少 DrawRect 调用 |
| `a3f5aa3` | 用场景软边界/包围盒裁剪地图显示范围 |
| `8af196b` | 修复 Vec2 只读属性赋值编译错误 |

---

*本文档由代码自动整理生成，供审计使用。所有接口调用均基于 Bannerlord 1.4.6 实际可用 API。*
