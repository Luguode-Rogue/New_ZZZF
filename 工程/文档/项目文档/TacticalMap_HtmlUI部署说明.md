# TacticalMap HtmlUI 部署说明

分支：`feature/tacticalmap-htmlui-redesign`

## 当前状态

本分支保留原有 TacticalMap Gauntlet UI，同时新增右上角 HtmlUI 版本。

当前工程没有依赖一个可靠的“编译后自动复制 HTML 资源”的 IDE/MSBuild 部署流程，因此**运行前需要手动复制 `TacticalMapUI` 资源目录**。

## 手动部署

### 1. 编译 New_ZZZF

先正常编译：

`New_ZZZF (net472)`

编译完成后，实际运行时 DLL 所在目录通常是：

```text
E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\New_ZZZF\bin\Win64_Shipping_Client\
```

以你当前机器为例，必须确认目录中存在：

```text
New_ZZZF.dll
```

### 2. 复制 HtmlUI 资源

源目录：

```text
工程\New_ZZZF\TacticalMap\HtmlUI\
```

目标目录：

```text
游戏目录\Modules\New_ZZZF\bin\Win64_Shipping_Client\TacticalMapUI\
```

最终必须形成：

```text
Modules\New_ZZZF\
└─ bin\
   └─ Win64_Shipping_Client\
      ├─ New_ZZZF.dll
      └─ TacticalMapUI\
         └─ index.html
```

**注意：不要复制到下面这些位置：**

```text
Modules\New_ZZZF\TacticalMapUI\
Modules\New_ZZZF\工程\New_ZZZF\TacticalMapUI\
Modules\New_ZZZF\bin\TacticalMapUI\
```

HtmlUI 的运行时路径依据 `New_ZZZF.dll` 的 `Assembly.Location` 确定，因此必须与实际加载的 DLL 位于同一个 `bin\Win64_Shipping_Client` 运行时目录下。

### 3. 运行前检查

启动游戏前确认：

```text
Modules\New_ZZZF\bin\Win64_Shipping_Client\New_ZZZF.dll
Modules\New_ZZZF\bin\Win64_Shipping_Client\TacticalMapUI\index.html
Modules\BannerlordHtmlUI\bin\Win64_Shipping_Client\BannerlordHtmlUI.dll
```

三者都存在。

### 4. Framework 依赖

New_ZZZF 的 TacticalMap HtmlUI 使用 `BannerlordHtmlUI` Framework，因此需要已经正确安装并加载：

```text
Modules\BannerlordHtmlUI\
└─ bin\
   └─ Win64_Shipping_Client\
      └─ BannerlordHtmlUI.dll
```

同时 `BannerlordHtmlUI` 自己的运行时 `web` 资源也必须按 Framework 的部署说明存在。

## 如何确认部署成功

进入有地形的实际战场。

按 TacticalMap 原来的开关键 `N`。

预期结果：

```text
左上：原 Gauntlet TacticalMap
右上：新的 HtmlUI TacticalMap
```

如果右上角 HtmlUI 没出现，优先检查：

1. `New_ZZZF.dll` 当前实际加载路径。
2. `TacticalMapUI\index.html` 是否与该 DLL 同目录下。
3. `BannerlordHtmlUI.dll` 是否已加载。
4. 日志中是否出现：

```text
[TMap][HtmlUI] 注册失败
```

或者 `DirectoryNotFoundException`。

## 常见错误

### DirectoryNotFoundException

如果日志类似：

```text
DirectoryNotFoundException:
...\Modules\New_ZZZF\bin\Win64_Shipping_Client\TacticalMapUI
```

说明代码已经执行到 `RegisterContentRoot()`，但运行时目录下没有 `TacticalMapUI`。

解决方法：重新执行上面的手动复制步骤。

### 只复制 `index.html` 到错误目录

不要只看源码工程位置。真正运行的是 DLL 同目录资源：

```text
New_ZZZF.dll
TacticalMapUI\index.html
```

必须形成这一对运行时文件。

## 以后自动部署

后续可以继续完善 `Directory.Build.targets`，把：

```text
工程\New_ZZZF\TacticalMap\HtmlUI
```

自动复制到：

```text
$(TargetDir)TacticalMapUI
```

但在自动复制链经过实际构建验证之前，正式测试统一按本文的手动复制方式执行。
