# TacticalMap HtmlUI 输入模型

TacticalMap 交互态使用 `BannerlordHtmlUI.HtmlUiMouseCapture`，不使用 `HtmlUiInputMode.Captured`。

运行时模型：

```text
CompactPassive / FullPassive
    InputMode = Passive
    鼠标、键盘继续交给 Bannerlord

CompactInteractive / FullInteractive
    InputMode = MouseCaptured
    鼠标由 WebView2/HTMLUI 接收
    键盘焦点保持在 Bannerlord
    N 由 TacticalMap C# MissionLogic 处理

Hidden
    InputMode = Hidden
```

HTMLUI 不应调用 `window.focus()`、`element.focus()` 抢夺游戏键盘焦点。地图鼠标操作使用 Pointer Events；右键必须 `preventDefault()`，只执行 TacticalMap 的 `face` 命令，不允许 WebView2 原生上下文菜单。
