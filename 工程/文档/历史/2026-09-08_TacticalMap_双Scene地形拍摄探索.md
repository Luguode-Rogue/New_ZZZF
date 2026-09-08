# TacticalMap 双 Scene 地形拍摄探索（2026-09-08）

> 本文记录 TacticalMap 地形底图“第二 Native Scene + 独立 Camera + ThumbnailCreatorView”路线的技术探索结果。
>
> 当前状态：**中断**。
>
> 中断不代表该方向不存在技术价值；后续如 Native 渲染链、Texture 导出链或场景资源隔离方式有新的可靠证据，可从本文恢复探索。

## 1. 探索目标

TacticalMap 需要一张与战场范围对应的俯视地形图作为底图，同时由现有 TacticalMap 逻辑单独绘制 Agent、编队与其他动态元素。

本次探索尝试把“地形图拍摄”与正在运行的战场 Scene 分离：

```text
Mission.Scene
    │
    ├── 正常战斗 / RayCast / Agent
    │
    └── SceneName
          ↓
    CreateNewScene
          ↓
    Read 同一个 battle scene
          ↓
    独立 Camera
          ↓
    ThumbnailCreatorView
          ↓
    Texture / PNG
          ↓
    TacticalMap 地形底图
```

核心假设是：第二个 Native Scene 可以只承担地形/static scene 的视觉捕获，不参与战斗 Agent 系统，从而避免把动态单位烘焙进底图。

## 2. 原始代码证据

本项目规定的 Bannerlord 原始/反编译代码唯一参考来源为：

`https://github.com/Luguode-Rogue/Qika2-Source-Decompiled`

已确认的相关原始行为：

### 2.1 原版存在独立 Scene

Qika2 的 `SandBox.MapScene` 自身持有独立 `Scene`，通过 `Scene.CreateNewScene(...)` 创建后读取场景资源。该结构证明 Bannerlord 本身可以同时存在独立 Native Scene。其初始化过程还会显式调用 `Read(...)`、`ForceLoadResources(...)`、`Tick(...)` 等步骤。fileciteturn212file0L2-L2

### 2.2 原版存在 Scene + 独立 Camera + Tableau 渲染路线

Qika2 的 `MapConversationTableau` 使用缓存的独立 `_tableauScene`，为该 Scene 创建独立 Camera，并通过 `TableauView` 建立专用渲染目标；目标尺寸变化时会重新创建 Tableau。该路径说明“同一个游戏运行环境中使用独立 Scene / Camera / Render Target”属于原版已有架构思路。fileciteturn213file0L2-L2

## 3. 本次实现路线

本次 TacticalMap 实现采用 `TerrainPhotographerRev11`，核心步骤为：

1. 创建 `Scene.CreateNewScene(false, true, DecalAtlasGroup.Worldmap, ...)`。
2. 使用当前 `Mission.SceneName` 读取同名战场 Scene。
3. `ForceLoadResources(true)` 并进行一次 Tick。
4. 创建独立 `Camera`，使用高空正交视角覆盖 TerrainCache 的世界范围。
5. 将第二 Scene 注册给 `ThumbnailCreatorView`。
6. 使用 `ThumbnailRenderRequest.CreateWithoutTexture(...)` 创建渲染请求。
7. 收到 Thumbnail 回调后，调用 `Texture.SaveToFile(...)`。
8. 将 PNG 读取为像素，验证内容并缩放后交给 `TerrainCache.ApplyPhotoPixels(...)`。

最终 Agent 不进入底图，由现有 TacticalMap Tracking/UI 逻辑负责动态单位显示。

## 4. 关键兼容处理

### 4.1 `CreateWithoutTexture` 的 entity 参数

Qika2 对 `ThumbnailRenderRequest.CreateWithoutTexture(...)` 的托管封装会无条件读取 `entity.Pointer`，因此直接传入 `null` 会在 managed wrapper 中产生空引用。

为继续验证双 Scene 路线，增加了一个仅用于渲染请求的空 `GameEntity` anchor，并将其与目标 Scene 绑定。

测试日志已经证明该补丁实际生效：

```text
[PhotoNative] REV=11 created thumbnail anchor pointer=...
```

这说明“请求必须携带 entity”这一层已经被跨过去。现有补丁源码保留在：

`工程/New_ZZZF/TacticalMap/Terrain/ThumbnailRenderRequestTacticalMapPatch.cs`

## 5. 实机验证记录

### 2026-09-08 13:58 左右

进入 `battle_terrain_029` 后出现：

```text
rglGPU_device_d3d11::create_data_texture
参数错误。
```

当时请求尺寸为：

```text
2048 x 2551
```

随后增加 Native 最大边长限制，将请求压缩为约：

```text
1644 x 2048
```

用于继续确认问题是否由单纯 render target 尺寸导致。

### 2026-09-08 14:23 左右

第二次实机日志已经出现完整链路：

```text
[PhotoNative] REV=11 PHOTO_SCENE_READY ...
[PhotoNative] REV=11 created thumbnail anchor pointer=...
[PhotoNative] REV=11 clamped native request 2048x2551 -> 1644x2048
[PhotoNative] REV=11 START ...
[PhotoNative] REV=11 thumbnail callback ...
[PhotoNative] REV=11 render callback received ...
```

这说明：

- 第二 Native Scene 成功创建。
- 战场场景资源成功读取到第二 Scene。
- Thumbnail 请求成功登记。
- Native 渲染路径能够产生回调。

随后在图片读取阶段出现：

```text
System.Drawing.Bitmap..ctor(String filename)
ArgumentException: Parameter is not valid.
```

因此当时保存下来的文件并没有形成可由 `System.Drawing.Bitmap` 正常解析的有效图片，CPU 侧读取链需要额外处理。

同一阶段还观察到 SpeedTree 深度着色器相关错误：

```text
speedtree_depth_functions.rsh(34,2-13)
error X3018: invalid subscript 'depth'
```

该信息说明该额外渲染路径会触及 Native GPU / Shader 兼容链，而当前已有足够理由暂停继续扩大这一条链路。

## 6. 当前结论

本次双 Scene 探索达到的状态是：

```text
独立 Scene 创建        已证实可进入实机
场景资源读取            已进入实机运行
独立 Camera             已建立
Thumbnail 请求           已建立
Native 回调              已出现
Texture -> 文件           已进入，但导出结果需继续确认
文件 -> Bitmap            当前链路需要进一步处理
```

因此本路线在当前阶段**中断**，TacticalMap 主线回归使用 `Mission.Scene`，避免继续引入第二份战场 Native Scene、重复资源加载以及额外 GPU/Shader 渲染链。

## 7. 保留的代码与提交

### 双 Scene 实现阶段

对应实现基于：

`TerrainPhotographerRev11.cs`

### 空 entity 兼容补丁

对应：

`ThumbnailRenderRequestTacticalMapPatch.cs`

### 相关提交

- `d6c7ea7254df5e0edcb9a6a8a5e548b82778776c` — `fix: provide TacticalMap thumbnail anchor entity`
- `396481906855c16334e6e567c2812b182f69c618` — 清理临时自动修复 workflow
- 后续主线调整为单 Scene：`522cccfb8df03de65ba5ddd30743134d91fd1e1b`

## 8. 后续恢复条件

再次恢复双 Scene 探索前，建议至少取得以下任一类新证据：

1. Qika2 中找到与“独立 Scene + 地形全图 RenderTarget + CPU 可读 Texture”高度接近的完整原始调用链。
2. 确认 `ThumbnailCreatorView` / `ThumbnailRenderRequest` 的 Native 目标纹理创建参数、格式和尺寸约束。
3. 找到 Bannerlord 原版已经验证过的 `Texture -> 文件` 导出或 CPU 读取方案，而不是继续依赖当前 `SaveToFile -> System.Drawing.Bitmap` 链路。
4. 明确 SpeedTree / depth shader 在独立 Scene 渲染中的所需初始化条件，并能够复用原版已经存在的初始化过程。

在这些证据出现前，不再把第二 Native Scene 作为 TacticalMap 当前实现的基础架构。

## 9. 与当前主线的关系

当前 TacticalMap 主线以 `Mission.Scene` 为唯一战场 Scene。

当前功能文档仍以：

`工程/文档/功能/TacticalMap.md`

为现行设计入口；本文件只作为“双 Scene 地形拍摄”历史技术探索记录，用于说明该路线为何在当前节点中断，以及未来如何恢复探索。
