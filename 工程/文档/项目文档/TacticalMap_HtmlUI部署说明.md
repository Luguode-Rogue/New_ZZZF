# TacticalMap HtmlUI 部署说明

分支：`feature/tacticalmap-htmlui-redesign`

## 当前状态

TacticalMap 已接入 `BannerlordHtmlUI` Framework。Consumer C# 位于 `TacticalMap/UI/TacticalMapHtmlUi.cs`，HTML/CSS/JS 源文件位于：

```text
工程\New_ZZZF\GUI\Html\TacticalMap\
```

运行时资源由 `Directory.Build.targets` 在 `net472` Build 后自动部署到：

```text
$(TargetDir)TacticalMapUI\
```

因此无需再手工复制 HTML 资源。

## 运行时文件布局

编译后，以 `New_ZZZF.dll` 的 `Assembly.Location` 为准，实际目录应类似：

```text
Modules\New_ZZZF\bin\Win64_Shipping_Client\
├─ New_ZZZF.dll
└─ TacticalMapUI\
   ├─ index.html
   ├─ tactical-map.css
   └─ tactical-map.js
```

同时必须存在：

```text
Modules\BannerlordHtmlUI\bin\Win64_Shipping_Client\BannerlordHtmlUI.dll
```

以及 BannerlordHtmlUI Framework 自身要求的 WebView2/`web` 运行时资源。

## 构建部署

执行：

```text
New_ZZZF (net472)
```

`Directory.Build.targets` 会执行：

```text
GUI\Html\TacticalMap\**\*
        ↓
$(TargetDir)TacticalMapUI\
```

源码工程中的 `GUI\Html\TacticalMap` 不是运行时路径；运行时依据 DLL 的 `Assembly.Location` 查找 `TacticalMapUI`。

## 如何确认部署成功

进入支持 TacticalMap 的实际战场后，默认应看到：

```text
右下角：CompactPassive 小地图
```

默认不捕获鼠标，不影响 Bannerlord 正常战斗输入。

N 短按：

```text
CompactPassive <-> CompactInteractive
FullPassive    <-> FullInteractive
```

N 长按：

```text
Compact -> Full -> Hidden -> Compact
```

ESC：

```text
CompactInteractive -> CompactPassive
FullInteractive    -> FullPassive
```

操作状态下：

```text
左键：移动
中键：镜头
右键：朝向
```

全屏地图提供：

```text
左侧：编队列表
中央：战术地图
右侧：编队详情
底部：状态/操作提示
```

## 常见错误

### DirectoryNotFoundException

如果日志显示：

```text
...\Modules\New_ZZZF\bin\Win64_Shipping_Client\TacticalMapUI
```

说明 Build 后实际 DLL 旁没有 `TacticalMapUI`。优先检查：

1. 使用的是 `net472` 构建。
2. `$(GameFolder)` 指向正确的 Bannerlord 安装目录。
3. `Directory.Build.targets` 已被工程加载。
4. `GUI\Html\TacticalMap` 源目录存在。

### Framework 未就绪

必须先加载：

```text
Modules\BannerlordHtmlUI\bin\Win64_Shipping_Client\BannerlordHtmlUI.dll
```

Consumer 使用 `HtmlUiService.OnReady(...)` 注册，不自行创建 WebView2。

### 页面能显示但不能点击

按照 Framework 输入链检查：

```text
N 短按
→ Captured
→ WebView Focus
→ Canvas pointer events
→ HTML Command
```

TacticalMap 本身不直接操作 WebView2/Overlay HWND。

## 实现职责

```text
TacticalMap Core
├─ TerrainCache
├─ FormationTracker
├─ AgentTracker
├─ OrderSystem
└─ CameraController

TacticalMap HtmlUI Consumer
├─ Page 生命周期
├─ Compact/Full/Hidden 状态机
├─ Passive/Captured 输入模式
├─ State
├─ Command / Request
└─ HTML 资源注册

HTML/CSS/JS
├─ Terrain / Risk 绘制
├─ Formation / Agent / Player / Camera Target
├─ 编队列表与详情
└─ 左中右键交互
```
