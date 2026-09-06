# TacticalMap 拍照式底图（PhotoMap）

> 状态：**拍照链路完整走通（日志实证），第二轮缓存命中已验证；跨场稳定性与卡顿归因待帧率数据**
> 最后更新：2026-09-06（DLL 15:37:10 之后含缓存命中 NRE 修复）
> 相关：`功能/TacticalMap.md`、`功能/TacticalMap_AI开发指南.md`、`历史/BUG_HISTORY.md`

## 1. 功能定义与设计意图（用户明确的，不可违背）

- 用引擎离屏渲染对战场场景做**正交俯视拍照**，真实地形照片作为 TacticalMap 底图。
- **进入战斗（含部署阶段）即启用拍摄**——不等待部署结束/开战信号（`IsDeploymentFinished`/`AllowAiTicking` 等待方案均被否决）。
- **每场战斗只拍一次**；**同场景退出再进直接复用缓存照片**（跨战斗照片缓存，2026-09-06 用户要求新增）。
- 拍照完成前**不发布占位底图**（算法配色/彩色占位均被否决）：前端显示纯黑 + NavMesh 可行区域兜底。
- 寻路走 `NavMeshMap`（独立系统），与底图无关。
- 性能要求：不得造成持续卡顿（帧率诊断已内建，见 §6）。

## 2. 代码清单（唯一源，改这里）

| 文件 | 职责 |
|---|---|
| `工程/New_ZZZF/TacticalMap/Terrain/TerrainPhotoCapture.cs` | 拍照状态机 + 跨战斗照片缓存 + 延迟销毁 |
| `工程/New_ZZZF/TacticalMap/Terrain/TerrainCache.cs` | 轻量烘焙（黑底 20ms）；`BakeSignature`（缓存键）；`PhotoBaseRGBA/Width/Height`；`ApplyPhotoPixels`；`DownsampleNearest` |
| `工程/New_ZZZF/TacticalMap/Core/TacticalMapMissionLogic.cs` | `TickPhotoCapture` 驱动；heartbeat 含 FPS 诊断；`OnEndMission` 必须 `Shutdown()` |
| `工程/New_ZZZF/TacticalMap/UI/TacticalMapHtmlUi.cs` | 发布：`PhotoBaseRGBA != null` 才发 `terrainBaseRgba` + `terrainWidth/Height`；签名含 PhotoVersion/PhotoWidth/PhotoHeight |
| `工程/New_ZZZF/_Module/UI/TacticalMap/tactical-map.js` | **UI 唯一资源源**：letterbox `mapRect`、terrainWidth 解码、navMesh 仅黑窗期显示、渲染 FPS 诊断、小地图跳过编队列表 DOM 重建 |

## 3. 最终流程（当前代码，15:37:10 后）

```
首次进入某场景：
  mission 创建 → bake LIGHT(黑底,20ms) → 创建 SceneView + 非方形 RT(2048×N) + 正交相机 → SetEnable(true)
  → 等 ReadyToRender（启用状态查询）→ settle 180 帧（mip 级联）
  → 渲染 15 帧 → SetEnable(false) → SaveToFile(%TEMP%) → 轮询 PNG 稳定
  → 解码 → 双线性重采样（1024×(1024*WorldH/WorldW)，全图即有效区域）
  → 自动感光（仅 avg<55 拉伸）→ 写照片缓存 → ApplyPhotoPixels → PublishState
  → 延迟 120 帧销毁 view/RT/camera

再次进入同场景：
  Start → BakeSignature 命中缓存（校验 WorldW/H 防碰撞）→ 直接发布，零渲染零落盘零闪烁，elapsed=0ms
```

## 4. 已证伪路线（⚠ 严禁重试，全部有实机日志/崩溃证据）

| # | 方案 | 结果 |
|---|---|---|
| 1 | `SceneView.CheckSceneReadyToRender()` 做等待信号 | 对主战斗场景**恒 false**，整场黑图（14:01 会话） |
| 2 | tick 中轮询 `Scene.IsLoadingFinished()` | 第二场开场主线程冻结（13:47 会话） |
| 3 | 等 `Mission.IsDeploymentFinished` | 无 OrderOfBattle 战斗在 loading 96% 提前置 true（DeploymentMissionController.cs:201-204），部署阶段视图就启用（15:2x 用户实测"部署阶段跑动图"）——且"等开战"本身违背设计意图 |
| 4 | 等 `Mission.AllowAiTicking` | 同 #3 等待方案被用户否决（进战斗即拍） |
| 5 | 第二视图开 postfx（`SetRenderWithPostfx`+`SetDoQuickExposure`+`EnsurePostfxSystem`） | 共享场景双视图 postfx 竞争 → 原版 `RayCastForClosestEntityOrTerrain` **AV 闪退**。曾验证 avg 23→101 的收益，不可取 |
| 6 | `Texture.GetPixelData(byte[])` 回读 RenderTarget | **直接 AccessViolationException**，损坏状态异常托管 catch 拦不住 → 拍照标 Fail 底图黑（14:42 会话）。已彻底移除 |
| 7 | Cleanup 调 `AddClearTask(false)` | 排入渲染队列的任务在 view/RT Release 后执行 → `photo applied` 后 RayCast AV 闪退（14:52 会话）。已移除 |
| 8 | photo applied 后**立即** Cleanup 销毁 view/RT | 同上 AV；已改**延迟 120 帧销毁**（DeferCleanup） |
| 9 | 改 mod 根 `UI/TacticalMap/*.js` | 无效——编译时 `_Module` 覆盖 mod 根。UI 改动必须落 `_Module/UI/TacticalMap/` |
| 10 | 算法配色 / 彩色占位底图 | 用户否决，只接受真实照片或黑底 |

## 5. 引擎 API 可用性速查（源码：`C:\Users\42029\CodeBuddy\骑砍2源码\1.5.0`）

| API | 可用性 |
|---|---|
| `SceneView.ReadyToRender()` | ✅ 启用状态下可用（禁用状态疑似恒 false，勿在禁用时依赖） |
| `SceneView.CheckSceneReadyToRender()` | ❌ 主战斗场景恒 false |
| `Scene.IsLoadingFinished()` | ❌ 禁止 tick 中轮询 |
| `Mission.AllowAiTicking` / `IsDeploymentFinished` | ⚠ 托管可读，但作为"战斗开始"信号已否决 |
| `Texture.SaveToFile` | ✅ 唯一验证过的回读路线（异步，轮询 PNG 大小稳定） |
| `Texture.GetPixelData` | ❌ RT 上 AV |
| `NativeObject.ManualInvalidate` | ✅ 原生对象释放（引用计数） |
| `Scene.EnsurePostfxSystem` / view postfx | ❌ 共享场景第二视图禁用 |

## 6. 诊断体系（帧率 + 全链路日志）

### 游戏主线程（heartbeat，5s 一条）
```
Mission heartbeat. UIVisible=... Mode=... Baked=... FPS=58 avgFrame=17.2ms worstFrame=213ms spikes250ms=1
```
`FPS` 低或 `spikes250ms` 高 → 卡在引擎/主线程方向。

### 前端渲染（JS→主日志，`JS: ` 前缀，5s 一条）
```
JS: render fps=60 (canvas=1132x843)
JS: render slow: 87ms (canvas=...)      ← 单帧 >50ms 限频 2s
```
游戏 FPS 正常但 `render fps` 低 → 卡在 WebView2/前端。

### 拍照链路（事件驱动）
```
[PhotoMap] v6 capture requested. halfW=... halfH=... captureNo=1
[PhotoMap] render window opened (settle=180 frames); rendering for 15 more frames.
[PhotoMap] offscreen render settled (15 frames); view disabled.
[PhotoMap] SaveToFile requested: ...
[PhotoMap] photo stats: avgBefore=... stretch=on/off ... publish=1024x762 (aligned to battle bounds ...)
[PhotoMap] photo applied. captureNo=1 PhotoVersion=1 publishSize=1024 elapsed=...ms
[PhotoMap] photo cache hit (signature=...); skipped rendering.       ← 第二轮
[PhotoMap] static publishing: photo=yes photoVer=1 dims=... terrainBase64Len=...
JS: applyStatic: terrain=decoded dims=1024x762 ver=... navMesh=ok
```

**失败定位**：`[PhotoMap] FAILED at <阶段>: <原因> | waitFrames=... settle=... render=... viewEnabled=... targetAlive=... png=... elapsed=...ms`——一条日志含全部断点状态。

**"photo applied 但地图黑"**：按序查 `static publishing`（C# 是否发布）→ `JS: applyStatic terrain=decoded/DECODE-FAIL/null`（前端是否收到/解码）。

## 7. 未验证 / 未修清单

### 待实机验证
1. **卡顿归因**：用 §6 的 FPS 对照定位（游戏主线程 vs 前端）——2026-09-06 用户报"第二场卡"时缓存已命中（零拍照开销），卡源在别处，待帧率数据
2. 连续多场稳定性（`Shutdown` + 延迟销毁修复后未做多场回归）
3. 部署期 settle=180 防接缝效果

### 代码审查已报未修（2026-09-06 审查）
- P1：`tactical-map.js applyStatic` 到达后不重报 canvasRect——letterbox 与全 rect 尺寸差异大的地图上切换后 3 秒内点击 UV 错位（当前地图比例巧合未暴露）
- P2：`ApplyPhoto` 的 avg 分子含黑像素分母不含——暗边可误触发拉伸（当前 avg=101 未触发）
- P3：`ComputeTerrainSignature` 不含 `PhotoMapSwapRedBlue`；`updateChrome` 高频 rect 上报未做去重

### 已知固有代价（用户知悉）
- 开战/部署期 ~3.3 秒第二视图渲染共享场景 → agent 闪烁（引擎无按对象过滤渲染 API；官方 tableau 全用独立场景）
- 照片烙印拍照瞬间单位
- 落盘轮询期间 RT（约 12MB 显存，2048×1536）驻留 ~1.2s

## 8. 关键设计不变量

1. 照片缓冲行 0 = **南**，列 0 = 西；`ApplyPhoto` 的 `fy = photoH-1-fySouth` 是唯一翻转点
2. 发布缓冲宽高比 = `WorldW:WorldH`，与缓存栅格/UV 1:1 对齐；HTML 只认 `terrainWidth/terrainHeight`
3. `PhotoVersion` 是发布去重的增量因子；改底图必须走 `ApplyPhotoPixels`
4. 视口 = battle bounds 分轴外扩 4m（`_halfW/_halfH` 分轴），RT 非方形匹配——正方形视口方案已废弃（27% 面积浪费）
5. 照片缓存键 = `TerrainCache.BakeSignature`，命中需校验 WorldW/H 匹配（防哈希碰撞）；缓存于首次拍照成功后写入（`ApplyPhoto` 尾部）
6. UI 资源唯一源 = `_Module/UI/TacticalMap/`；C# 唯一源 = `工程/New_ZZZF/TacticalMap/`；构建 = `dotnet build ... -c Debug -p:Platform=x64`（游戏运行时部署会 MSB4018 失败，校验构建结果必须查 `MSB4018` 而非只查 `error CS`）
7. `_stopwatch` 必须在 `Start` 的缓存命中分支**之前**初始化（命中路径也走 `CompleteCapture`——曾因顺序产生 NRE，15:37 修复）
