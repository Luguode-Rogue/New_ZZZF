# TacticalMap HtmlUI 部署说明

分支：`feature/tacticalmap-htmlui-redesign`

本说明按 `BannerlordHtmlUI` Framework 当前 `dev` 文档执行。Framework 明确规定 Consumer 的程序集旁运行时 UI 资源由 `Assembly.Location` 定位，标准布局为 DLL 同目录下的 `UI/`；Framework ConsumerTestMod 也是这一布局。citeturn120file0turn123file0

## 源码位置

TacticalMap HTML/CSS/JS 源文件现在位于：

```text
工程\New_ZZZF\TacticalMap\UI\TacticalMap\
├─ index.html
├─ tactical-map.css
└─ tactical-map.js
```

C# Consumer 位于：

```text
工程\New_ZZZF\TacticalMap\UI\TacticalMapHtmlUi.cs
```

## 运行时位置

以实际加载的 `New_ZZZF.dll` 的 `Assembly.Location` 为基准，Framework Consumer 运行时 UI 根目录是：

```text
<New_ZZZF.dll所在目录>\UI\
```

TacticalMap Page 的相对路径是：

```text
TacticalMap\index.html
```

因此最终应为：

```text
Modules\New_ZZZF\bin\Win64_Shipping_Client\
├─ New_ZZZF.dll
└─ UI\
   └─ TacticalMap\
      ├─ index.html
      ├─ tactical-map.css
      └─ tactical-map.js
```

## C# ContentRoot / Page 对应关系

Consumer 使用：

```csharp
var assemblyDir = Path.GetDirectoryName(typeof(TacticalMapHtmlUi).Assembly.Location) ?? ".";
var uiRoot = Path.Combine(assemblyDir, "UI");
_scope.RegisterContentRoot("tacticalmap", uiRoot);

_scope.RegisterPage(new HtmlUiPage("tacticalmap", "TacticalMap/index.html")
{
    ContentRootId = "tacticalmap"
});
```

注意三者不能混用：

```text
Page ID          = tacticalmap
ContentRoot ID   = tacticalmap
实际 Windows 目录 = ...\bin\Win64_Shipping_Client\UI\
HTML 相对路径    = TacticalMap\index.html
```

## Build / Deploy

`工程\New_ZZZF\Directory.Build.targets` 会在 `net472` Build 后执行：

```text
工程\New_ZZZF\TacticalMap\UI\TacticalMap\**\*
        ↓
$(TargetDir)UI\TacticalMap\
```

因此不需要手工复制 HTML 资源。

## 依赖

必须先存在：

```text
Modules\BannerlordHtmlUI\bin\Win64_Shipping_Client\BannerlordHtmlUI.dll
```

以及 Framework 自身要求的 WebView2 / `web` 运行时资源。

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

如果出现 `DirectoryNotFoundException`，首先检查的是：

```text
New_ZZZF.dll
UI\TacticalMap\index.html
```

是否位于同一个 `bin\Win64_Shipping_Client` 运行时目录层级，而不是检查旧的 `TacticalMapUI` 目录。

## 依据

Framework 当前开发指南：

```text
Project/BannerlordHtmlUI/BannerlordHtmlUI/docs/DEVELOPMENT_GUIDE.md
```

其 ContentRoot 规则明确采用：

```text
Assembly.Location
    ↓
Path.Combine(assemblyDir, "UI")
    ↓
RegisterContentRoot(...)
```

Framework ConsumerTestMod 的实际工程部署也采用：

```text
<Mod>\bin\<GameBinariesFolder>\<Mod>.dll
<Mod>\bin\<GameBinariesFolder>\UI\...
```
