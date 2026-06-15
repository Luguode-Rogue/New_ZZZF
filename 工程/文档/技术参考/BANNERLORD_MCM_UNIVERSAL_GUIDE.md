# Bannerlord Mod — MCM 集成通用指南

> 从零开始在 Bannerlord Mod 中集成 MCM (Mod Configuration Menu) v5 的完整教程。
> 适用于任何需要在游戏内提供设置界面的 Mod 项目。

---

## 一、MCM 是什么

MCM (Mod Configuration Menu) 是 Mount & Blade II: Bannerlord 的统一设置管理框架，由 Aragas 开发维护。

- **仓库**: https://github.com/Aragas/Bannerlord.MBOptionScreen
- **文档**: https://mcm.bannerlord.aragas.org
- **NexusMods**: 搜索 "Mod Configuration Menu"

### 核心能力

| 能力 | 说明 |
|------|------|
| 游戏 GUI | 在选项菜单中自动生成设置界面 |
| 持久化 | 自动保存/加载设置到 JSON 或 XML |
| 本地化 | 支持游戏的多语言翻译系统 `{=KEY}` |
| 预设 | 内置预设切换 |
| 多种设置类型 | Bool/Int/Float/String/Dropdown/Button |
| 多种存储范围 | Global/PerCampaign/PerSave |

### 运行时依赖

MCM 需要以下 Mod 作为前置：

1. `Bannerlord.Harmony`
2. `Bannerlord.UIExtenderEx`
3. `Bannerlord.ButterLib`

---

## 二、两种集成方式

### 方式 A：内嵌到 Mod 的 `/bin`（推荐新手）

将 MCM DLL 一起打包到你的 Mod 中，玩家无需额外安装。

**步骤**：

1. csproj 添加 NuGet 包引用：

```xml
<ItemGroup>
    <!-- ⚠️ IncludeAssets="compile" 不可省略！否则会打包多余 DLL -->
    <PackageReference Include="Bannerlord.MCM" Version="5.0.0" IncludeAssets="compile" />
</ItemGroup>
```

2. `_Module/SubModule.xml` 中添加 MCM 子模块：

```xml
<SubModules>
    <!-- 你的 Mod -->
    <SubModule>
        <Name value="MyMod" />
        <DLLName value="MyMod.dll" />
        <SubModuleClassType value="MyMod.SubModule" />
        <Tags />
    </SubModule>
    <!-- MCM 核心 -->
    <SubModule>
        <Name value="MCMv5" />
        <DLLName value="MCMv5.dll" />
        <SubModuleClassType value="MCM.MCMSubModule" />
        <Tags />
    </SubModule>
    <!-- MCM UI 实现 -->
    <SubModule>
        <Name value="MCMv5 Basic Implementation" />
        <DLLName value="MCMv5.dll" />
        <SubModuleClassType value="MCM.Internal.MCMImplementationSubModule" />
        <Tags />
    </SubModule>
</SubModules>
```

3. 将 MCMv5 的 DLL 文件复制到 `/bin/Win64_Shipping_Client/`

**优点**：玩家无需额外安装 MCM，开箱即用
**缺点**：可能导致与其他 Mod 内嵌的 MCM 版本冲突

### 方式 B：依赖独立 Mod（推荐正式发布）

要求玩家单独安装 MCM Mod，你的 Mod 只声明依赖。

**步骤**：

1. csproj 同样添加 NuGet 包引用（同上）
2. `_Module/SubModule.xml` 中声明依赖：

```xml
<DependedModules>
    <DependedModule Id="Bannerlord.MBOptionScreen"/>
</DependedModules>
```

3. **不要**将 MCM 的 DLL 放入你的 `/bin` 文件夹

**优点**：无版本冲突，体验统一
**缺点**：玩家需要额外安装前置

---

## 三、定义设置类

### 3.1 Attribute API（推荐，简洁直观）

```csharp
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

public class MyModSettings : AttributeGlobalSettings<MyModSettings>
{
    // ===== 必须重写的 4 个属性 =====

    public override string Id => "MyMod_v1";            // 唯一标识
    public override string DisplayName => "My Mod Settings";
    public override string FolderName => "MyMod";        // 配置存储文件夹
    public override string FormatType => "json2";        // ⚠️ 必须指定！默认 "none" 不保存

    // ===== 设置属性 =====

    [SettingPropertyBool("Enable Feature", RequireRestart = false,
        HintText = "Whether to enable this feature.", Order = 0)]
    [SettingPropertyGroup("General")]
    public bool EnableFeature { get; set; } = true;

    [SettingPropertyInteger("Max Count", 0, 100,
        HintText = "Maximum count allowed.", Order = 1)]
    [SettingPropertyGroup("General")]
    public int MaxCount { get; set; } = 50;

    [SettingPropertyFloatingInteger("Ratio", 0f, 1f, "#0%",
        HintText = "The ratio value.", Order = 2)]
    [SettingPropertyGroup("Advanced")]
    public float Ratio { get; set; } = 0.75f;

    [SettingPropertyText("Name", HintText = "Enter a name.", Order = 3)]
    [SettingPropertyGroup("Advanced")]
    public string Name { get; set; } = "Default";

    // ===== 实时同步 =====

    public override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
        // 值变更时触发，可在此做额外处理
    }
}
```

### 3.2 设置类型对比

| 类型 | 属性 | C# 类型 | 控件 | 特有参数 |
|------|------|---------|------|----------|
| 布尔 | `SettingPropertyBool` | `bool` | 勾选框 | `IsToggle` |
| 整数 | `SettingPropertyInteger` | `int` | 滑块 | `minValue`, `maxValue`, `valueFormat` |
| 浮点 | `SettingPropertyFloatingInteger` | `float` | 滑块 | `minValue`, `maxValue`, `valueFormat` |
| 文本 | `SettingPropertyText` | `string` | 文本框 | — |
| 下拉 | `SettingPropertyDropdown` | `Dropdown<T>` | 下拉框 | 泛型选择 |
| 按钮 | `SettingPropertyButton` | `Action` | 按钮 | `Content` |

### 3.3 设置存储范围

| 范围 | 基类 | 说明 |
|------|------|------|
| 全局 | `AttributeGlobalSettings<T>` | 所有存档共享 |
| 按战役 | `AttributePerCampaignSettings<T>` | 同战役不同存档共享 |
| 按存档 | `AttributePerSaveSettings<T>` | 存在存档文件中 |

> ⚠️ `PerSave` 类型的设置：如果移除 MCM 后保存一次，设置将被永久清除。

---

## 四、API 参数详解

### 4.1 公共命名参数（所有属性通用）

```csharp
[SettingPropertyBool("Name",
    Order = 0,              // 排序索引，数字越小越靠前
    RequireRestart = false, // 修改后是否需要重启游戏
    HintText = "说明文字"    // 鼠标悬停时显示的提示
)]
```

### 4.2 SettingPropertyInteger（重点）

```csharp
[SettingPropertyInteger(
    "Display Name",   // 位置1: 显示名
    0,                // 位置2: 最小值
    100,              // 位置3: 最大值
    "0 Denars",       // 位置4: valueFormat（可选，默认"0"）
    Order = 1,
    RequireRestart = false,
    HintText = "Description"   // ← 命名参数，不是位置参数！
)]
public int MyInt { get; set; } = 50;
```

**构造函数签名**：

```
SettingPropertyIntegerAttribute(string displayName, int minValue, int maxValue, string valueFormat = "0")
```

> ⚠️ **常见错误**：把 HintText 写成第 4 个位置参数。第 4 个参数是 `valueFormat`（数值显示格式），`HintText` 必须用命名参数 `HintText = "..."` 。

### 4.3 SettingPropertyFloatingInteger

```csharp
[SettingPropertyFloatingInteger(
    "Time Scale",     // 显示名
    0.05f,            // 最小值
    1f,               // 最大值
    "#0%",            // valueFormat（如百分比显示）
    HintText = "说明",
    Order = 2
)]
public float TimeScale { get; set; } = 0.35f;
```

`valueFormat` 语法参考：[自定义数字格式字符串](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings)

### 4.4 SettingPropertyDropdown

```csharp
[SettingPropertyDropdown("Mode", HintText = "Select a mode.", Order = 3)]
public Dropdown<string> Mode { get; set; } = new Dropdown<string>(
    new[] { "Easy", "Normal", "Hard" }, selectedIndex: 1);

// 自定义对象（需重写 ToString）
public class MyOption
{
    private readonly string _value;
    public MyOption(string value) => _value = value;
    public override string ToString() => _value;
}

[SettingPropertyDropdown("Custom Mode", Order = 4)]
public Dropdown<MyOption> CustomMode { get; set; } = new Dropdown<MyOption>(
    new[] { new MyOption("A"), new MyOption("B") }, selectedIndex: 0);
```

### 4.5 SettingPropertyGroup（分组）

```csharp
// 单层分组
[SettingPropertyGroup("General")]

// 嵌套分组（用 / 分隔）
[SettingPropertyGroup("Advanced/Performance")]

// 带排序
[SettingPropertyGroup("General", GroupOrder = 1)]

// 组切换开关
[SettingPropertyBool("Enable Advanced", IsToggle = true)]
[SettingPropertyGroup("Advanced")]
public bool AdvancedToggle { get; set; } = false;
```

---

## 五、本地化

### 5.1 设置中的本地化格式

MCM 属性名和 HintText 均支持游戏的 `{=KEY}` 本地化系统：

```csharp
[SettingPropertyBool("{=MYMOD_001}Enable Feature",
    HintText = "{=MYMOD_H001}Description of this feature.")]
[SettingPropertyGroup("{=MYMOD_G001}General Settings")]
public bool EnableFeature { get; set; } = true;
```

### 5.2 翻译文件

创建翻译 XML 文件，放在 `ModuleData/Languages/` 下：

**英文** — `ModuleData/Languages/EN/std_module_strings_xml.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<base xmlns:xsi="..." xmlns:xsd="..." type="string">
  <tags>
    <tag language="English" />
  </tags>
  <strings>
    <string id="MYMOD_001" text="Enable Feature" />
    <string id="MYMOD_H001" text="Description of this feature." />
    <string id="MYMOD_G001" text="General Settings" />
  </strings>
</base>
```

**中文** — `ModuleData/Languages/CNs/std_module_strings_xml-zho-CN.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<base xmlns:xsi="..." xmlns:xsd="..." type="string">
  <tags>
    <tag language="简体中文" />
  </tags>
  <strings>
    <string id="MYMOD_001" text="启用功能" />
    <string id="MYMOD_H001" text="此功能的描述。" />
    <string id="MYMOD_G001" text="通用设置" />
  </strings>
</base>
```

### 5.3 翻译键命名规范建议

| 前缀 | 用途 | 示例 |
|------|------|------|
| `MOD_XXX` | 属性显示名 | `MYMOD_001` |
| `MOD_HXXX` | 属性提示文本 | `MYMOD_H001` |
| `MOD_GXXX` | 分组名 | `MYMOD_G001` |

---

## 六、读取设置值

### 6.1 静态实例访问

```csharp
// 在 MCM 初始化完成后（OnBeforeInitialModuleScreenSetAsRoot 之后）可用
var settings = MyModSettings.Instance;
if (settings?.EnableFeature == true)
{
    // ...
}
```

> ⚠️ `Settings.Instance` 在 `OnSubModuleLoad` 期间尚不可用，最早可在 `OnBeforeInitialModuleScreenSetAsRoot` 中使用。

### 6.2 INotifyPropertyChanged

设置类实现了 `INotifyPropertyChanged`，MCM 会自动订阅并在值变更时刷新 UI：

```csharp
private bool _myOption = false;

public bool MyOption
{
    get => _myOption;
    set
    {
        if (_myOption != value)
        {
            _myOption = value;
            OnPropertyChanged(); // 触发 UI 刷新
        }
    }
}
```

---

## 七、脱离 MCM 的独立实现

如果你的 Mod 需要在**没有 MCM** 的环境中运行，需实现独立的配置持久化层。

### 7.1 双层架构模式

```
MCM Settings 类 ←→ 独立数据类 ←→ XML/JSON 文件
     ↑                  ↑
  (有 GUI)          (无 GUI 也能工作)
```

### 7.2 独立数据类

```csharp
[Serializable]
[XmlRoot("MyModSettings")]
public class MyModSettingsData
{
    public bool EnableFeature = true;
    public int MaxCount = 50;
    public float Ratio = 0.75f;
}
```

### 7.3 管理器

```csharp
public static class MyModSettingsManager
{
    private static readonly string ConfigFolder =
        Path.Combine(BasePath.Name, "Modules", "MyMod");

    private static readonly string XmlPath =
        Path.Combine(ConfigFolder, "MyModSettings.xml");

    private static readonly object _lock = new object();
    private static FileSystemWatcher _watcher;

    public static MyModSettingsData Data { get; private set; }

    public static MyModSettingsData Settings => Data ?? Load();

    public static void Load()
    {
        lock (_lock)
        {
            if (!Directory.Exists(ConfigFolder))
                Directory.CreateDirectory(ConfigFolder);

            if (File.Exists(XmlPath))
            {
                var serializer = new XmlSerializer(typeof(MyModSettingsData));
                using var stream = new FileStream(XmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Data = (MyModSettingsData)serializer.Deserialize(stream) ?? new MyModSettingsData();
            }
            else
            {
                Data = new MyModSettingsData();
                SaveToXml();
            }

            StartWatcher();
        }
    }

    public static void Save()
    {
        lock (_lock) { SaveToXml(); }
    }

    /// <summary>
    /// MCM → XML 同步：在 MCM Settings 的 OnPropertyChanged 中调用
    /// </summary>
    public static void SyncFromMCM(MyModSettings mcm)
    {
        if (mcm == null) return;
        lock (_lock)
        {
            if (Data == null) Data = new MyModSettingsData();
            Data.EnableFeature = mcm.EnableFeature;
            Data.MaxCount = mcm.MaxCount;
            Data.Ratio = mcm.Ratio;
            SaveToXml();
        }
    }

    /// <summary>
    /// 在 Mission Tick 中调用，处理 XML 热重载
    /// </summary>
    public static void ProcessPendingReload() { /* 详见项目文档 */ }

    private static void SaveToXml() { /* XmlSerializer 序列化 */ }
    private static void StartWatcher() { /* FileSystemWatcher 监控 */ }
}
```

### 7.4 MCM Settings 中桥接

```csharp
public class MyModSettings : AttributeGlobalSettings<MyModSettings>
{
    // ... 属性定义 ...

    public override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
        MyModSettingsManager.SyncFromMCM(this); // 同步到 XML
    }

    public MyModSettings()
    {
        var data = MyModSettingsManager.Settings;
        if (data != null)
        {
            EnableFeature = data.EnableFeature;
            MaxCount = data.MaxCount;
            Ratio = data.Ratio;
        }
    }
}
```

### 7.5 业务代码统一读取

```csharp
// 方式 1：通过 MCM 实例（有 MCM 时更实时）
var settings = MyModSettings.Instance;
if (settings?.EnableFeature == true) { ... }

// 方式 2：通过 XML 管理器（无 MCM 也能工作，支持热重载）
var data = MyModSettingsManager.Settings;
if (data.EnableFeature) { ... }
```

### 7.6 XML 热重载

```csharp
// 在 MissionBehavior.OnMissionTick 中
MyModSettingsManager.ProcessPendingReload();
```

---

## 八、Fluent Builder API（运行时动态创建）

适用于需要在运行时根据条件动态生成设置的场景。

```csharp
bool _boolValue = false;
int _intValue = 1;

var builder = BaseSettingsBuilder.Create("MyMod_Dynamic", "Dynamic Settings")!
    .SetFormat("json2")
    .SetFolderName("MyMod")
    .CreateGroup("General", groupBuilder => groupBuilder
        .AddBool("prop_1", "Enable Feature",
            new ProxyRef<bool>(() => _boolValue, o => _boolValue = o),
            boolBuilder => boolBuilder
                .SetHintText("Description")
                .SetRequireRestart(false)))
    .CreateGroup("Advanced", groupBuilder => groupBuilder
        .AddInteger("prop_2", "Max Count", 0, 10,
            new ProxyRef<int>(() => _intValue, o => _intValue = o),
            integerBuilder => integerBuilder.SetHintText("Description")));

var settings = builder.BuildAsGlobal();
settings.Register();
```

> ⚠️ `PerCampaign` 和 `PerSave` 类型的设置必须在玩家已加入战役之后注册，否则注册列表会在加入战役时被清除。

### IRef 接口

| 实现 | 用途 |
|------|------|
| `PropertyRef` | 链接到实际属性 (`PropertyInfo`) |
| `ProxyRef<T>` | 链接到 get/set 委托 |
| `StorageRef` | 自身持有值 |

---

## 九、常见陷阱速查

| # | 陷阱 | 症状 | 修复 |
|---|------|------|------|
| 1 | `FormatType` 未重写 | 设置不持久化，重启丢失 | `FormatType => "json2"` 或 `"xml"` |
| 2 | `HintText` 写成位置参数 | 提示文本不显示 | 使用 `HintText = "..."` 命名参数 |
| 3 | `IncludeAssets="compile"` 缺失 | NuGet 包 DLL 被打包进 Mod | 添加 `IncludeAssets="compile"` |
| 4 | 用整数选枚举 | 滑块不直观 | 改用 `SettingPropertyBool` 多个开关或 `SettingPropertyDropdown` |
| 5 | `Settings.Instance` 用太早 | 返回 null | 在 `OnBeforeInitialModuleScreenSetAsRoot` 之后使用 |
| 6 | PerSave 设置未注册 | 设置丢失 | 确保在战役加载后注册 |
| 7 | `OnPropertyChanged()` 未调用 | UI 不刷新 | 属性 setter 中调用 `OnPropertyChanged()` |
| 8 | 分组名中文硬编码 | 无法本地化 | 使用 `{=KEY}` 格式引用翻译 |

---

## 十、快速起步清单

```
□ 1. csproj 添加 Bannerlord.MCM NuGet 包（IncludeAssets="compile"）
□ 2. 创建 Settings 类，继承 AttributeGlobalSettings<T>
□ 3. 重写 Id / DisplayName / FolderName / FormatType
□ 4. 用 SettingPropertyBool/Integer/... 标注属性
□ 5. 用 SettingPropertyGroup 分组
□ 6. SubModule.xml 声明依赖或内嵌 MCM 子模块
□ 7. 创建翻译 XML（EN + CNs）
□ 8. 业务代码通过 Settings.Instance 读取
□ 9. 如需脱离 MCM，实现独立数据类 + 管理器
□ 10. 测试：游戏内 MCM 菜单可见 + 修改持久化 + 本地化正确
```

---

## 参考链接

- [MCM 官方文档](https://mcm.bannerlord.aragas.org)
- [MCM GitHub 仓库](https://github.com/Aragas/Bannerlord.MBOptionScreen)
- [MCM v5 属性 API](https://mcm.bannerlord.aragas.org/articles/MCMv5/mcmv5-attributes.html)
- [MCM v5 Fluent Builder](https://mcm.bannerlord.aragas.org/articles/MCMv5/mcmv5-fluent-builder.html)
- [示例项目：TrainingTweak](https://github.com/Aragas/TrainingTweak)
- [示例项目：ButterEquipped](https://github.com/jzebedee/ButterEquipped)
