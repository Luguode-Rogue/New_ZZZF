# ProjectileTrajectorySystem — MCM 集成文档

> 本文档记录本项目如何集成 MCM (Mod Configuration Menu)，以及脱离 MCM 独立运行的实现方式。

---

## 一、架构总览

本项目采用 **双层设置架构**，同时支持 MCM GUI 和纯 XML 配置：

```
┌─────────────────────────────────────────────────┐
│                  玩家操作界面                      │
│  ┌──────────────┐     ┌──────────────────────┐  │
│  │  MCM GUI     │     │  手动编辑 XML 文件    │  │
│  │  (游戏内菜单) │     │  (热重载自动生效)     │  │
│  └──────┬───────┘     └──────────┬───────────┘  │
│         │                        │               │
│         ▼                        ▼               │
│  ┌──────────────────────────────────────────┐   │
│  │   ProjectileTrajectorySettings (MCM)     │   │
│  │   AttributeGlobalSettings<T> 子类        │   │
│  │   → OnPropertyChanged → SyncFromMCM()    │   │
│  └──────────────────┬───────────────────────┘   │
│                     │ 同步                       │
│                     ▼                            │
│  ┌──────────────────────────────────────────┐   │
│  │   ProjectileTrajectorySettingsData       │   │
│  │   纯 POCO + XmlSerializer                │   │
│  │   持久化到 XML 文件                       │   │
│  └──────────────────┬───────────────────────┘   │
│                     │ 读取                       │
│                     ▼                            │
│  ┌──────────────────────────────────────────┐   │
│  │   ProjectileTrajectorySettingsManager    │   │
│  │   Load / Save / 热重载 / SyncFromMCM     │   │
│  └──────────────────────────────────────────┘   │
└─────────────────────────────────────────────────┘
```

### 核心设计理念

| 设计目标 | 实现方式 |
|----------|----------|
| MCM 可用时提供 GUI | `ProjectileTrajectorySettings : AttributeGlobalSettings<T>` |
| MCM 不可用时仍可配置 | 独立的 XML 序列化 + FileSystemWatcher 热重载 |
| 双端数据一致性 | MCM 修改 → `OnPropertyChanged` → `SyncFromMCM()` → 写入 XML |
| 业务代码解耦 | 统一通过 `ProjectileTrajectorySettingsManager.Settings` 读取 |

---

## 二、三个核心文件

### 2.1 ProjectileTrajectorySettings.cs — MCM 设置类

```csharp
// 继承 AttributeGlobalSettings<T>，MCM 自动发现并生成 GUI
public class ProjectileTrajectorySettings : AttributeGlobalSettings<ProjectileTrajectorySettings>
{
    // 必须重写的 4 个属性
    public override string Id => "ProjectileTrajectorySystem";       // 唯一标识
    public override string DisplayName => new TextObject("{=PTS_DisplayName}...").ToString();
    public override string FolderName => "ProjectileTrajectorySystem"; // 配置文件夹
    public override string FormatType => "xml";                       // ⚠️ 必须指定！

    // 布尔开关
    [SettingPropertyBool("{=PTS_001}Enable Trajectory System",
        RequireRestart = false,
        HintText = "{=PTS_H001}Master switch...",
        Order = 0)]
    [SettingPropertyGroup("{=PTS_G001}Trajectory Display")]
    public bool EnableTrajectory { get; set; } = true;

    // 整数滑块 — 注意 HintText 是命名参数！
    [SettingPropertyInteger("{=PTS_010}Max Tracked Enemies", 0, 50,
        HintText = "{=PTS_H010}Maximum number...",
        Order = 9)]
    public int MaxTrackedEnemiesLegacy { get; set; } = 10;

    // 浮点滑块
    [SettingPropertyFloatingInteger("{=PTS_102}Time Scale", 0.05f, 1f,
        HintText = "{=PTS_H102}Slow motion multiplier.",
        Order = 22)]
    public float SlowMoTimeScale { get; set; } = 0.35f;

    // MCM → XML 同步
    public override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
        ProjectileTrajectorySettingsManager.SyncFromMCM(this);
    }

    // 构造时从 XML 初始化 MCM 显示值
    public ProjectileTrajectorySettings()
    {
        var data = ProjectileTrajectorySettingsManager.Settings;
        if (data != null)
        {
            EnableTrajectory = data.EnableTrajectory;
            // ... 逐字段同步
        }
    }
}
```

**关键陷阱 — SettingPropertyInteger 的参数**：

```csharp
// ❌ 错误：HintText 内容被当成了 valueFormat（第4位置参数）
[SettingPropertyInteger("Name", 0, 3, "{=PTS_H031}0=Off 1=...", Order = 30)]

// ✅ 正确：HintText 使用命名参数
[SettingPropertyInteger("Name", 0, 3, HintText = "{=PTS_H031}0=Off 1=...", Order = 30)]
```

构造函数签名：`(displayName, minValue, maxValue, valueFormat = "0")`，第 4 个参数是**格式字符串**，`HintText` 必须用命名参数。

### 2.2 ProjectileTrajectorySettingsData.cs — 独立数据类

```csharp
[Serializable]
[XmlRoot("ProjectileTrajectorySettings")]
public class ProjectileTrajectorySettingsData
{
    public bool EnableTrajectory = true;
    public bool PlayerTrajectory = true;
    // ... 纯字段，无 MCM 依赖
}
```

- 纯 POCO，零 MCM 依赖
- `XmlSerializer` 直接序列化/反序列化
- 无 MCM 环境下是唯一的配置持久化手段

### 2.3 ProjectileTrajectorySettingsManager.cs — 管理器

```csharp
public static class ProjectileTrajectorySettingsManager
{
    // XML 路径: <Game>/Modules/ProjectileTrajectorySystem/ProjectileTrajectorySettings.xml
    private static readonly string XmlPath = Path.Combine(
        BasePath.Name, "Modules", "ProjectileTrajectorySystem", "ProjectileTrajectorySettings.xml");

    public static ProjectileTrajectorySettingsData Settings => Data ?? Load();

    // 加载
    public static void Load() { /* 从 XML 读取，不存在则生成默认 */ }

    // MCM → XML 同步
    public static void SyncFromMCM(ProjectileTrajectorySettings mcm) { /* 逐字段复制 + SaveToXml() */ }

    // 热重载（FileSystemWatcher）
    public static void ProcessPendingReload() { /* Mission Tick 中调用 */ }
}
```

---

## 三、脱离 MCM 独立运行

本项目 **可以在没有安装 MCM 的环境中正常运行**，方式如下：

### 3.1 运行机制

| 场景 | 行为 |
|------|------|
| MCM 已安装 | 游戏内菜单修改 → `OnPropertyChanged` → 同步到 XML |
| MCM 未安装 | `ProjectileTrajectorySettings` 类仍被实例化但无 GUI；玩家直接编辑 XML 文件 |

### 3.2 业务代码读取方式

Harmony 补丁中通过 `ProjectileTrajectorySettings.Instance` 读取（MCM 静态实例）：

```csharp
// SiegeWeaponShootDebugPatch.cs
var settings = ProjectileTrajectorySettings.Instance;
if (settings?.SiegeWeaponShootFix == true) { ... }
```

Mission Tick 中通过 `ProjectileTrajectorySettingsManager.Settings` 读取（XML 数据）：

```csharp
// SkillSystemBehavior.cs
var settings = ProjectileTrajectorySettingsManager.Settings;
if (!settings.EnableTrajectory) return;
```

**两种读取方式的区别**：

| 方式 | 数据源 | 是否需要 MCM | 热重载 |
|------|--------|-------------|--------|
| `ProjectileTrajectorySettings.Instance` | MCM 内部缓存 | 需要 MCM 运行时 | MCM 触发 |
| `ProjectileTrajectorySettingsManager.Settings` | XML 文件 | 不需要 | FileSystemWatcher |

### 3.3 XML 热重载

玩家编辑 XML 保存后，`FileSystemWatcher` 检测变更，在下一次 Mission Tick 中自动重载：

```csharp
// SkillSystemBehavior.OnMissionTick
ProjectileTrajectorySettingsManager.ProcessPendingReload();
```

防抖间隔 300ms，避免多次触发。

---

## 四、本地化

### 4.1 文件位置

| 语言 | 路径 |
|------|------|
| 中文 | `ModuleData/Languages/CNs/std_module_strings_xml-zho-CN.xml` |
| 英文 | `ModuleData/Languages/EN/std_module_strings_xml.xml` |

### 4.2 本地化键格式

MCM 属性名和 HintText 使用 `{=KEY}` 格式引用翻译：

```csharp
[SettingPropertyBool("{=PTS_001}Enable Trajectory System",   // 英文为默认值
    HintText = "{=PTS_H001}Master switch...")]                // 中文玩家看到翻译
```

XML 翻译文件：

```xml
<!-- 中文 -->
<string id="PTS_001" text="启用弹道系统" />
<string id="PTS_H001" text="总开关：是否启用弹道可视化和敌人高亮。" />

<!-- 分组名也支持 -->
<string id="PTS_G001" text="弹道显示设置" />
```

### 4.3 键命名规范

| 前缀 | 用途 | 示例 |
|------|------|------|
| `PTS_XXX` | 属性显示名 | `PTS_001` = 启用弹道系统 |
| `PTS_HXXX` | 属性提示文本 | `PTS_H001` = 总开关说明 |
| `PTS_GXXX` | 分组名 | `PTS_G001` = 弹道显示设置 |
| `PTS_DisplayName` | 设置面板标题 | 弹道轨迹系统 |

---

## 五、项目引用配置

### 5.1 csproj 引用

本项目使用本地 DLL 引用（非 NuGet 包），因为开发环境已安装 MCM：

```xml
<Reference Include="MCMv5">
    <HintPath>..\..\Bannerlord.MBOptionScreen\bin\Win64_Shipping_Client\MCMv5.dll</HintPath>
</Reference>
<Reference Include="MCM.UI.Adapter.MCMv5">
    <HintPath>..\..\Bannerlord.MBOptionScreen\bin\Win64_Shipping_Client\MCM.UI.Adapter.MCMv5.dll</HintPath>
</Reference>
<!-- MCM 各版本兼容适配器 -->
<Reference Include="Bannerlord.MBOptionScreen.v1.0.0">...</Reference>
<!-- ... 更多版本适配器 ... -->
```

### 5.2 SubModule.xml 依赖

当前未在 `DependedModules` 中声明 MCM 依赖，项目可在无 MCM 环境运行。如需强制要求 MCM：

```xml
<DependedModules>
    <DependedModule Id="Bannerlord.MBOptionScreen"/>
</DependedModules>
```

---

## 六、已踩过的坑

| 坑 | 症状 | 修复 |
|----|------|------|
| `HintText` 写成位置参数 | 提示文本不显示，长文本被当成数字格式 | 改用 `HintText = "..."` 命名参数 |
| `FormatType` 未设置 | 设置修改后不持久化，重启恢复默认 | 设置 `FormatType => "xml"` |
| `SettingPropertyInteger` 用整数选枚举 | 滑块不好用，0/1/2/3 不直观 | 改成多个 `SettingPropertyBool` 勾选框 |
| MCM 实例未初始化就读取 | `Instance` 为 null，空引用异常 | 先判空，或使用 XML 后端作为兜底 |
| XML 热重载频繁触发 | `FileSystemWatcher` 连续多次 Changed 事件 | 加 300ms 防抖 |
