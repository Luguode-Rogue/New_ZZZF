# TacticalMap REV12 地形拍摄交接文档

> 文档类型：功能/排查交接
>
> 交接对象：下一会话 / 下一位开发者，按零历史上下文编写。
>
> 交接范围：TacticalMap 战场地形底图拍摄链，重点为 REV12 Native 离屏拍摄。
>
> 当前状态：**⏳ 进行中，已定位到“私有 Scene 能创建和进入渲染链，但输出 PNG 几乎全黑”这一阶段；尚未得到可用地形照片。**

---

## 0. 快速指令

**接手后的第一任务不是继续改 HTML，也不是继续调 PNG 保存，而是确认 `Scene.Read(Mission.SceneName)` 创建出的私有 Scene 是否真的包含可渲染的战场地形，以及地形是否位于当前相机使用的世界坐标。**

当前建议直接从以下提交继续：

```text
afff1d7292e91c506545084c4e6f07563388acd5
refactor: use isolated SceneView capture for REV12
```

当前核心文件：

```text
工程/New_ZZZF/TacticalMap/Terrain/TerrainPhotographerRev11.cs
```

当前测试战场示例：

```text
battle_terrain_029
```

当前 REV12 输出规格：

```text
宽度：1024
高度：按 TerrainCache.WorldH / WorldW 比例计算，示例日志为 1275
```

当前成功条件不能只看“PNG 文件存在”。必须同时满足：

```text
1. 私有 Scene 创建并读取成功
2. SceneView ReadyToRender
3. RenderTarget 有效
4. SaveToFile 成功
5. PNG 像素统计明显非黑
6. PNG 内容确实是地形，而不是天空、纯色或错误坐标区域
7. 原战场 Agent 不进入照片
8. 原 Mission.Scene 与 Thumbnail 管线不被修改
```

---

## 1. 一句话结论

**✅ REV12 已经跨过 Native 对象创建、独立 Scene、独立 Camera、RenderTarget、SceneView、保存 PNG 这些基础链路；❌ 当前失败点是“私有 Scene 的可见地形没有出现在最终像素中”，具体原因尚未确认。**

最新实机日志给出的最重要证据：

```text
[PhotoNative] REV=12 isolated scene loaded pointer=6348579340288
[PhotoNative] REV=12 isolated tableau created target=1024x1275 scene=6348579340288 view=6347682972160
[PhotoNative] REV=12 START isolatedScene=617676800 liveScene=1288437760 sceneName=battle_terrain_029 target=1024x1275
[PhotoNative] REV=12 isolated tableau paint requested.
[PhotoNative] REV=12 isolated render saved=C:\Users\42029\AppData\Local\Temp\TMapPhotoNative_REV12_1626494530.png warmup=8 paintRequested=True
[PhotoNative] REV=12 PNG=1024x1275 avg=0.1 variance=0.4 nonBlack=0.0% min=0 max=7
[PhotoNative] REV=12 failed: PNG pixel validation failed
```

注意：上面的部分字段来自此前 Tableau 版 REV12 日志；当前代码已经在提交 `afff1d7292e91c506545084c4e6f07563388acd5` 切换到 `SceneView + RenderTarget` 路线。不要把历史 Tableau 日志误认为当前架构仍在使用 Tableau。

---

## 2. 功能目标与边界

### 2.1 目标

TacticalMap 需要一张与战场范围对应的俯视地形照片作为底图。Agent、编队和其他动态信息由现有 TacticalMap Tracking/UI 逻辑单独绘制，因此底图中**不得烘焙 Agent**。

### 2.2 架构边界

当前主线要求：

```text
Mission.Scene
    ├── 正常战斗
    ├── Agent
    ├── RayCast / Gameplay
    └── Mission 生命周期

REV12 私有 Scene
    ├── 读取同名 Battle Scene
    ├── 私有 Camera
    ├── 私有 SceneView
    ├── 私有 RenderTarget
    └── PNG
```

私有拍摄链不得注册到游戏原有 `ThumbnailCreatorView` / `ThumbnailRenderRequest` 共享回调链，也不得把 Mission.Scene 替换为拍摄 Scene。

现行 TacticalMap 总体架构入口仍是：

```text
工程/文档/功能/TacticalMap.md
```

该文档明确 TacticalMap 由 Terrain、Tracking、Order、Camera、UI 等层组成，HtmlUI 负责表现层，TacticalMapController 负责运行时协调。fileciteturn70file0L2-L2

---

## 3. 当前代码入口与关键对象

| 项目 | 路径/符号 | 当前作用 | 状态 |
|---|---|---|---|
| 地形拍摄 | `工程/New_ZZZF/TacticalMap/Terrain/TerrainPhotographerRev11.cs` | REV12 私有 Scene 拍摄主逻辑 | ✅ 当前主线 |
| 地形缓存 | `TerrainCache` | 保存 World 范围、烘焙结果、照片像素 | ✅ 已有 |
| 地形主流程 | `TacticalMapMissionLogic` | 启动/推进拍摄生命周期 | ✅ 已有 |
| HtmlUI | `TacticalMapHtmlUi.cs` | 发布 `photoUrl`、尺寸和其他地图数据 | ✅ 已有 |
| UI 静态资源 | `工程/New_ZZZF/TacticalMap/UI/TacticalMap/` | 展示最终照片 | ✅ 已有，不是当前故障点 |
| 双 Scene 历史记录 | `工程/文档/历史/2026-09-08_TacticalMap_双Scene地形拍摄探索.md` | 记录早期 Thumbnail 路线 | ✅ 历史资料，不作为当前实现 |

当前 `TerrainPhotographerRev11` 的 REV12 核心结构为：

```text
CreatePrivateScene()
    ↓
CreatePrivateRenderView()
    ↓
ConfigurePrivateRenderView()
    ↓
TickWarming()
    ↓
TickCapturing()
    ↓
SaveToFile()
    ↓
TickReading()
    ↓
ApplyAndValidate()
```

---

## 4. 当前 REV12 实现事实

### 4.1 私有 Scene

当前提交中，拍摄 Scene 通过以下路径创建：

```csharp
_photoScene = Scene.CreateNewScene(
    false,
    true,
    DecalAtlasGroup.Worldmap,
    "TacticalMapPhotoSceneREV12");
```

随后使用当前 `Mission.SceneName` 调用 `Scene.Read(...)`，再调用 `ForceLoadResources(true)` 和一次 `Tick(0.1f)`。

当前实现还设置了：

```csharp
UsePhysicsMaterials = false;
EnableFloraPhysics = false;
UseTerrainMeshBlending = false;
CreateOros = false;
```

**⚠️ 这组初始化开关目前没有被证明适用于“完整战场地形静态渲染”。** 这是下一轮优先验证项，不应继续把它们视为既定正确配置。

### 4.2 私有渲染链

当前 REV12 使用：

```text
SceneView.CreateSceneView()
+ 私有 Scene
+ Camera.CreateCamera()
+ Texture.CreateRenderTarget()
```

渲染配置沿用了过去 REV7 在**活体 Mission.Scene** 上已经实际使用过的 SceneView + RenderTarget 参数，包括：

```text
SetRenderOnDemand(false)
SetRenderWithPostfx(false)
SetSceneUsesSkybox(true)
SetSceneUsesShadows(true)
SetSceneUsesContour(false)
SetClearGbuffer(true)
DoNotClear(false)
SetClearAndDisableAfterSucessfullRender(false)
SetDoQuickExposure(true)
SetResolutionScaling(false)
AddClearTask(false)
SetEnable(true)
```

这一部分的价值在于：**RenderTarget/SceneView 的基本拍摄机制已有历史实机依据，不应优先改动。**

### 4.3 Camera

当前 Camera 使用 `TerrainCache` 的世界范围计算俯视正交体，并以：

```text
centerX = OriginX + WorldW / 2
centerY = OriginY + WorldH / 2
cameraZ = MaxH + 300
```

作为中心与高度。

**🧩 当前最大未验证假设：私有 `Scene.Read()` 后的场景坐标与 `TerrainCache` / live Mission.Scene 使用同一世界坐标。** 如果私有 Scene 的场景内容不在这个坐标范围，渲染成功也会得到纯黑或空画面。

### 4.4 输出验证

PNG 保存后由 `System.Drawing.Bitmap` 读取并计算：

```text
Average
Variance
NonBlack
Min
Max
```

当前有效图片门槛：

```text
NonBlack >= 1%
Variance >= 2
Max >= 8
```

这套检查能阻止“文件存在但内容全黑”误判为成功。

---

## 5. 已验证事项

| 状态 | 事项 | 证据 |
|---|---|---|
| ✅ | 独立 Native Scene 可以创建 | REV12 日志出现 `isolated scene loaded pointer=...` |
| ✅ | 同名战场 Scene 可以进入 `Scene.Read()` 流程 | 日志中 `sceneName=battle_terrain_029` 且私有 Scene 成功建立 |
| ✅ | 私有 Camera 创建成功 | 当前代码 `Camera.CreateCamera()` 未触发启动异常 |
| ✅ | RenderTarget 创建成功 | REV12 日志出现目标尺寸并进入 SaveToFile |
| ✅ | SceneView 创建并进入 Ready/渲染阶段 | 当前 REV12 启动日志记录 `viewReady` / 渲染状态 |
| ✅ | PNG 文件可以被保存并读取 | 日志进入 `PNG=1024x1275` 像素分析 |
| ✅ | 空场景不会把 Agent 自动加入底图 | 当前 REV12 不使用 live Mission.Scene 做实际渲染源 |
| ❌ | PNG 包含可用战场地形 | 当前 `nonBlack=0.0%`，明确失败 |
| ❌ | 私有 Scene 的坐标与 TerrainCache 坐标已证明一致 | 尚无直接证据 |
| ❌ | 当前 `SceneInitializationData` 开关组合已证明正确 | 尚无直接证据 |

---

## 6. 当前根因候选排序

### 第一优先：私有 Scene 实际没有加载出可渲染地形

**🧩 推断，未验证。**

依据：Scene、View、RenderTarget、SaveToFile 都能完成，但像素接近全黑。若渲染链完全失效，通常首先会表现为 RenderTarget/SaveToFile 生命周期异常；现在更像是渲染到了“空 Scene”。

重点检查当前：

```csharp
UseTerrainMeshBlending = false;
CreateOros = false;
```

以及其他 `SceneInitializationData` 默认字段是否需要保留。

### 第二优先：私有 Scene 中的地形存在，但不在 Camera 当前坐标范围

**🧩 推断，未验证。**

当前 Camera 直接使用 TerrainCache 坐标。必须证明 `Scene.Read()` 产生的私有 Scene 和 live Mission.Scene 使用相同的空间基准。

### 第三优先：私有 Scene 已加载地形，但缺少完成渲染所需的特定资源/初始化

**⚠️ 未确认。**

这比前两项优先级低，但不能排除。不要在没有新证据的情况下直接增加大量 Native 初始化调用。

### 暂不优先：PNG 保存链

**✅ 基础链已通过。** 当前已经能读取 PNG 并得到像素统计，因此 `SaveToFile -> Bitmap` 不是当前首要阻塞点。

### 暂不优先：HTML/UI

**✅ 非当前故障点。** 现在连底图源 PNG 都是黑的，UI 还没有进入需要排查的阶段。

---

## 7. 下一步执行方案

下一轮只做一个目标：**证明私有 Scene 中“有没有地形”和“地形在哪里”。**

### 7.1 第一组改动：恢复更完整的 Scene 初始化

文件：

```text
工程/New_ZZZF/TacticalMap/Terrain/TerrainPhotographerRev11.cs
```

操作：

1. 暂时移除/停止主动设置以下三个字段：

```csharp
UseTerrainMeshBlending = false;
CreateOros = false;
EnableFloraPhysics = false;
```

2. 暂时保留：

```csharp
UsePhysicsMaterials = false;
```

除非新的源码证据表明该字段也会影响静态渲染。

3. 保留现有 `Scene.Read()`、`ForceLoadResources(true)`、`Tick()`、SceneView、RenderTarget 和 SaveToFile 流程。

**目的：**先排除“为了降低复杂度而关闭了场景渲染依赖”的变量。

### 7.2 第二组改动：增加“空 Scene / 坐标错位”诊断

在不改变现有渲染架构的前提下增加日志，至少记录：

```text
live Mission.Scene pointer
private Scene pointer
Mission.SceneName
private Scene loadingFinished
SceneView ReadyToRender
RenderTarget valid
Camera centerX / centerY / cameraZ
Camera halfW / halfH / far
TerrainCache OriginX / OriginY / WorldW / WorldH / MinH / MaxH
```

并明确标记：

```text
LIVE_COORD
PRIVATE_COORD
```

避免下一个会话把“相机参数合理”误认为“私有 Scene 空间一致”。

### 7.3 第三组改动：做单变量相机探测

在确认 Scene 初始化不再主动关闭 terrain 能力后，才做相机位置探测。

测试至少使用：

```text
A. 当前 TerrainCache 中心
B. 世界原点附近
C. 原点偏移后的少量范围
```

不需要一次做大量位置扫描；只需要判断黑帧是否随相机空间位置发生明显变化。

**验收解释：**

- A/B/C 均纯黑：更像 Scene 内容/资源未加载。
- 某个位置出现地形：坐标系错位基本得到证据。
- 出现天空但无地形：Scene 可渲染，但地形资源/高度范围仍存在问题。
- 出现地形：进入真正的“地形全图构图”阶段。

### 7.4 暂时不要做的改动

不要同时改：

```text
HTML
TerrainCache 边界算法
AgentTracker
OrderSystem
Mission.Scene
ThumbnailCreatorView
ThumbnailRenderRequest
```

否则下一轮测试无法判断到底是哪一个变量解决问题。

---

## 8. 推荐实机测试顺序

每次测试只改变一个主要变量，并保留 REV12 的像素统计。

| 测试 | 修改 | 期望回答的问题 | 成功证据 |
|---|---|---|---|
| T1 | 仅恢复完整 Scene 初始化 | 当前初始化开关是否导致空 Scene | `nonBlack` 明显上升 |
| T2 | T1 不通过时，仅改变 Camera 空间位置 | 私有 Scene 是否与缓存坐标错位 | 某位置出现非黑地形 |
| T3 | 若出现地形，固定正确坐标再调正交范围 | 能否覆盖完整战场 | 图像包含战场主要地形 |
| T4 | 固定相机后检查不同战场 | 方案是否不是只适配 `battle_terrain_029` | 多场景都能得到有效地形图 |

---

## 9. 验收标准

### 9.1 最低验收

必须同时满足：

```text
PNG 文件生成成功
AND
PNG 可以由 Bitmap 正常读取
AND
NonBlack >= 1%
AND
Variance >= 2
AND
Max >= 8
```

### 9.2 功能验收

最低需要实机证明：

```text
1. battle_terrain_029 得到非黑地形图
2. 照片中没有 Agent
3. TacticalMap UI 能使用该照片
4. 原 Mission.Scene 正常战斗不受影响
5. 原 Thumbnail 系统正常
```

### 9.3 架构验收

必须保持：

```text
拍摄 Scene 独立
拍摄 Camera 独立
RenderTarget 独立
不注册共享 Thumbnail 管线
不替换 Mission.Scene
```

---

## 10. 已否决方案——勿重复尝试

### 10.1 双 Scene + ThumbnailCreatorView / ThumbnailRenderRequest

**❌ 当前阶段暂停，不作为 REV12 基础路线。**

历史记录：

```text
工程/文档/历史/2026-09-08_TacticalMap_双Scene地形拍摄探索.md
```

历史实机阶段虽然证明：

```text
第二 Scene 可以创建
Scene 资源可以读取
Thumbnail 请求可以建立
Native 回调可以出现
```

但同时遇到：

```text
rglGPU_device_d3d11::create_data_texture
System.Drawing.Bitmap ArgumentException
speedtree_depth_functions.rsh(34,2-13): error X3018
```

因此现阶段不应重新引入 ThumbnailCreatorView 共享链。历史文档明确将该探索标记为“中断”。fileciteturn68file0L2-L2

### 10.2 直接把 Mission.Scene 接到正式拍摄链

**❌ 不作为当前最终架构。**

早期单 Scene 方案虽然有助于证明 SceneView/RenderTarget 参数能够工作，但 live Scene 会包含 Agent 和战斗动态对象，无法直接满足“纯地形底图”的要求。

### 10.3 继续单纯增加 WarmupFrames

**❌ 不应作为主要修复手段。**

当前已出现完整 SaveToFile 与像素分析流程；继续把 30 帧、60 帧、120 帧逐级增加，在没有证明 Scene 内容存在之前只能延迟失败时间。

---

## 11. 重要历史事实与证据

### 11.1 原始来源约束

项目规定：Bannerlord 原始/反编译实现只允许参考：

```text
https://github.com/Luguode-Rogue/Qika2-Source-Decompiled
```

不要用其他 Bannerlord 反编译仓库、第三方 API 文档或论坛代码来推断 Native 行为。

### 11.2 Qika2 已获得的相关证据

此前已确认的原版/反编译代码模式包括：

- `SandBox.Map.MapConversationTableau` 使用独立/缓存 Scene、独立 Camera、Texture 和 TableauView。
- `TableauView.AddTableau(...)` 会建立专用 Tableau/Texture。
- `View` 存在 RenderTarget、RenderOnDemand、Enable、RenderOption、SaveToFile 等渲染控制。
- `Camera` 存在独立创建、释放、LookAt 和 ViewVolume 设置。
- `NativeObject` 使用引用计数/ManualInvalidate 生命周期。

这些证据证明“同一游戏进程中存在独立 Native Scene + 独立渲染对象”是可行的，但**不能直接证明 `Scene.CreateNewScene + Scene.Read(battle scene)` 一定能得到一个可直接全图渲染的战场复制品。**

### 11.3 当前主线文档

TacticalMap 当前功能定位和层次结构以：

```text
工程/文档/功能/TacticalMap.md
```

为准。该文档要求地形、Tracking、Order、Camera 和 UI 分层，且 HtmlUI 只作为表现层。fileciteturn70file0L2-L2

---

## 12. Git 与版本状态

### 12.1 当前 REV12 基线

```text
Commit:
afff1d7292e91c506545084c4e6f07563388acd5

Message:
refactor: use isolated SceneView capture for REV12
```

该提交将 REV12 从独立 TableauView 路线切换为：

```text
Private Scene
    ↓
Private SceneView
    ↓
Private Camera
    ↓
Private RenderTarget
    ↓
SaveToFile
```

提交 diff 明确移除了 REV12 对 `TableauView.AddTableau(...)` 的依赖，并加入 `SceneView.CreateSceneView()`、独立 RenderTarget 和 `ReadyToRender()` 驱动的 warmup/capture 生命周期。fileciteturn71file0L3-L7

### 12.2 前置提交

```text
9b7f39e8b4a7ce14cf890a88de4b699c11ee57a6
```

该阶段修正了错误的 `Scene.IsLoadingFinished()` 门控问题，并增加 warmup / paint / 保存时机诊断。

```text
ecc6022d52b19d5c3a33bdb91b9414f0119a9348
```

该阶段将 REV12 改为独立 Tableau 路线，目的是避开 ThumbnailCreatorView 共享管线。

### 12.3 历史双 Scene 探索

历史文档对应提交与探索记录见：

```text
工程/文档/历史/2026-09-08_TacticalMap_双Scene地形拍摄探索.md
```

该路线当前标记为：

```text
❌ 中断 / 勿作为当前主线
```

---

## 13. 调试日志规范

后续测试仍使用：

```text
[PhotoNative] REV=12
```

建议新增日志固定采用以下结构：

```text
[PhotoNative] REV=12 PRIVATE_SCENE ...
[PhotoNative] REV=12 CAMERA ...
[PhotoNative] REV=12 VIEW ...
[PhotoNative] REV=12 TARGET ...
[PhotoNative] REV=12 PNG ...
```

重点保留：

```text
scene pointer
scene loadingFinished
scene name
camera center
camera volume
viewReady
renderTargetValid
PNG width/height
avg
variance
nonBlack
min
max
```

不要恢复高频逐帧日志；只记录状态转换和测试关键参数。

---

## 14. 下一会话的停止条件

出现以下任一结果时，应停止继续随机修改，回到证据分析：

1. T1/T2 连续两轮仍然 `nonBlack=0%`，且 SceneView/RenderTarget 全部正常。
2. 改动同时涉及 Scene 初始化、Camera、RenderTarget 三个大变量。
3. 再次出现 D3D11 / SpeedTree shader 错误。
4. 为了解决黑帧重新引入 ThumbnailCreatorView 共享管线。
5. 开始修改 HTML 或 Agent Tracking 来掩盖底图为空的问题。

此时应该追加新的历史排查文档，而不是覆盖本交接文档。

---

## 15. 当前最终状态

| 项目 | 状态 |
|---|---|
| TacticalMap UI | ✅ 已有，不是当前阻塞点 |
| TerrainCache | ✅ 已有，不是当前阻塞点 |
| Agent 独立绘制 | ✅ 现有设计，不纳入照片 |
| 私有 Scene 创建 | ✅ 已验证进入实机 |
| 私有 SceneView | ✅ 已建立 |
| 私有 Camera | ✅ 已建立 |
| 私有 RenderTarget | ✅ 已建立 |
| PNG 保存 | ✅ 已执行 |
| PNG 读取与像素验证 | ✅ 已执行 |
| 有效地形像素 | ❌ 当前为 0% |
| 私有 Scene 地形是否真的存在 | ⏳ 未验证 |
| 私有 Scene 坐标是否与 TerrainCache 一致 | ⏳ 未验证 |
| 完整战场地形照片 | ❌ 尚未完成 |
| 纯地形、无 Agent 的最终底图 | ❌ 尚未完成 |

**接手者应从“私有 Scene 地形存在性 + 坐标验证”开始，而不是重新设计整个 TacticalMap。**
