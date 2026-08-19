# TacticalMap HtmlUI 部署说明

分支：`feature/tacticalmap-htmlui-redesign`

## 统一资源放置规则

本说明不再独立定义 HtmlUI/Mod 资源的工程目录与最终部署目录。统一遵循 BannerlordHtmlUI 的：

[`BUTR_PROJECT_LAYOUT_RULES.md`](https://github.com/Luguode-Rogue/BannerlordHtmlUI/blob/dev/Project/BUTR_PROJECT_LAYOUT_RULES.md)

该文档是资源放置规则的唯一规范。涉及 TacticalMap 的具体运行时位置时，再结合：

1. `New_ZZZF` 的 `.csproj` / `Directory.Build.targets` Build/Deploy Target。
2. `TacticalMapHtmlUi` 中的 `Assembly.Location` / `RegisterContentRoot`。
3. Page 注册使用的 ContentRoot ID 与 HTML 相对路径。
4. BannerlordHtmlUI 的 `INPUT.md` 页面级 `Hidden / Passive / Captured` 输入契约。

## 当前实现对应关系

TacticalMap C# Consumer：

```text
工程/New_ZZZF/TacticalMap/UI/TacticalMapHtmlUi.cs
```

TacticalMap 前端源码：

```text
工程/New_ZZZF/TacticalMap/UI/TacticalMap/
├─ index.html
├─ tactical-map.css
└─ tactical-map.js
```

当前 Consumer 代码以加载后的 `New_ZZZF.dll` 所在目录为运行时基准，并将其旁的 `UI` 注册为 ContentRoot：

```csharp
var assemblyDir = Path.GetDirectoryName(typeof(TacticalMapHtmlUi).Assembly.Location) ?? ".";
var uiRoot = Path.Combine(assemblyDir, "UI");
_scope.RegisterContentRoot("tacticalmap", uiRoot);

_scope.RegisterPage(new HtmlUiPage("tacticalmap", "TacticalMap/index.html")
{
    ContentRootId = "tacticalmap",
    DefaultInputMode = HtmlUiInputMode.Passive,
    CloseOnEscape = false
});
```

因此这里的实际运行时路径必须由 BUTR 资源规则 + `.csproj`/`Directory.Build.targets` 部署目标共同决定，而不是由本文另行发明一个 `TacticalMapUI` 目录规则。

## Build / Deploy

构建后的 HTML/CSS/JS 由 `Directory.Build.targets` 自动部署到：

```text
Modules/<ModId>/bin/<GameBinariesFolder>/UI/TacticalMap/
```

对应关系：

```text
工程/New_ZZZF/TacticalMap/UI/TacticalMap/
    ↓
Directory.Build.targets
    ↓
Modules/New_ZZZF/bin/<GameBinariesFolder>/UI/TacticalMap/
    ↓
Assembly.Location + UI
    ↓
TacticalMap/index.html
```

不要求用户手工复制 HTML/CSS/JS。旧的 `TacticalMapUI` 输出目录会在 Build 后清理。

## 输入状态

TacticalMap 严格使用 Framework 的页面级输入模型：

```text
CompactPassive / FullPassive
    → HtmlUiInputMode.Passive
    → WebView2 穿透，不抢焦点

CompactInteractive / FullInteractive
    → HtmlUiInputMode.Captured
    → WebView2 获得交互焦点

Hidden
    → HtmlUiInputMode.Hidden
    → 覆盖层真正隐藏
```

因此 HTML/CSS 不负责实现跨窗口的 DOM 区域穿透；进入交互模式后整个 TacticalMap Page 由 Framework Captured，地图内部再用 DOM `pointerdown` 区分左/中/右键。

TacticalMap 设置 `CloseOnEscape=false`，所以 Framework 不会在 Overlay / 全局过滤器 / WebView2 Accelerator 层关闭 Page，Captured 状态下 ESC 由页面 JavaScript 处理为“退出操作模式”。

## 功能状态

```text
默认：CompactPassive 小地图
N 短按：CompactPassive <-> CompactInteractive
N 长按：Compact -> Full -> Hidden -> Compact
ESC：Interactive -> Passive
```

操作状态下：

```text
左键：移动
中键：镜头
右键：朝向
```

如果出现路径问题，按统一规则检查：

```text
工程源路径
    ↓
Directory.Build.targets
    ↓
Modules/<ModId>/bin/<GameBinariesFolder>/UI/TacticalMap
    ↓
Assembly.Location / ContentRoot 实际读取路径
```

如果出现输入问题，优先检查 Framework `INPUT.md` 的页面级模式，而不是重新实现 Overlay/Win32 输入穿透。

## 相关框架规则

统一资源放置规则：

[`https://github.com/Luguode-Rogue/BannerlordHtmlUI/blob/dev/Project/BUTR_PROJECT_LAYOUT_RULES.md`](https://github.com/Luguode-Rogue/BannerlordHtmlUI/blob/dev/Project/BUTR_PROJECT_LAYOUT_RULES.md)

Framework 输入规则：

[`https://github.com/Luguode-Rogue/BannerlordHtmlUI/blob/dev/Project/BannerlordHtmlUI/BannerlordHtmlUI/docs/INPUT.md`](https://github.com/Luguode-Rogue/BannerlordHtmlUI/blob/dev/Project/BannerlordHtmlUI/BannerlordHtmlUI/docs/INPUT.md)
