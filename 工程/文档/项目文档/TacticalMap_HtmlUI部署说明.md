# TacticalMap HtmlUI 部署说明

分支：`feature/tacticalmap-htmlui-redesign`

## 统一资源放置规则

本说明不再独立定义 HtmlUI/Mod 资源的工程目录与最终部署目录。统一遵循 BannerlordHtmlUI 的：

[`BUTR_PROJECT_LAYOUT_RULES.md`](https://github.com/Luguode-Rogue/BannerlordHtmlUI/blob/dev/Project/BUTR_PROJECT_LAYOUT_RULES.md)

该文档是资源放置规则的唯一规范。涉及 TacticalMap 的具体运行时位置时，再结合：

1. `New_ZZZF` 的 `.csproj` Build/Deploy Target。
2. `TacticalMapHtmlUi` 中的 `Assembly.Location` / `RegisterContentRoot`。
3. Page 注册使用的 ContentRoot ID 与 HTML 相对路径。

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
    ContentRootId = "tacticalmap"
});
```

因此这里的实际运行时路径必须由 BUTR 资源规则 + `.csproj` 部署目标共同决定，而不是由本文另行发明一个 `TacticalMapUI` 目录规则。

## Build / Deploy

构建后的文件应通过项目自己的 Build/Deploy Target 自动落到代码实际读取的路径。

不要求用户手工复制 HTML/CSS/JS。

如果出现路径问题，按统一规则检查：

```text
工程源路径
    ↓
.csproj Build/Deploy Target
    ↓
Modules/<ModId>/最终路径
    ↓
Assembly.Location / ContentRoot 实际读取路径
```

## 验证重点

进入支持 TacticalMap 的战场后：

```text
默认：CompactPassive 小地图
N 短按：Passive <-> Interactive
N 长按：Compact -> Full -> Hidden -> Compact
ESC：Interactive -> Passive
```

操作状态下：

```text
左键：移动
中键：镜头
右键：朝向
```

如果出现 `DirectoryNotFoundException`，优先按照 `BUTR_PROJECT_LAYOUT_RULES.md` 的四层核对法检查，而不是直接修改运行时目录字符串。

## 相关框架规则

统一资源放置规则：

[`https://github.com/Luguode-Rogue/BannerlordHtmlUI/blob/dev/Project/BUTR_PROJECT_LAYOUT_RULES.md`](https://github.com/Luguode-Rogue/BannerlordHtmlUI/blob/dev/Project/BUTR_PROJECT_LAYOUT_RULES.md)

Framework API / ContentRoot 行为另见 Framework 文档；它们不得重新定义资源工程路径规则。