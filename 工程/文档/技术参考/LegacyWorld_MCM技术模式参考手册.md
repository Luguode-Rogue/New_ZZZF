# LegacyWorld MCM 技术模式参考手册

> **概要**：本文档记录在 LegacyWorld 子系统中实现的 MCM 集成技术模式，源自 `ProjectileTrajectorySystem` 的成熟架构并针对 MCMv5 的局限做了适配。可作为 New_ZZZF 后续子系统和其它 Bannerlord Mod 开发的可复用技术参考。
>
> **来源**：New_ZZZF → LegacyWorld (v0.4.0)
> **参照**：ProjectileTrajectorySystem → Settings/ 三层架构

---

## 一、三层设置架构

将设置系统拆分为三个独立层，职责分离、各司其职。这是**核心模式**，所有需要 MCM 菜单的地方都应遵循。

```
┌──────────────────────────────────────────────────────┐
│  第3层: XXXMCMSettings                               │
│  (AttributeGlobalSettings<T>) — MCM GUI 模型          │
│  职责: 提供 MCM 面板显示和交互                         │
│  特性: [SettingPropertyBool] 等属性标注                │
│  流向: OnPropertyChanged → Manager.SyncFromMCM()      │
├──────────────────────────────────────────────────────┤
│  第2层: XXXSettingsManager                           │
│  (静态类) — 持久化管理器                               │
│  职责: XML 加载/保存、MCM↔Data 同步                    │
│  特性: static Settings 属性暴露数据层                   │
│  流向: Load() / Save() / SyncFromMCM()                 │
├──────────────────────────────────────────────────────┤
│  第1层: XXXSettingsData                              │
│  (Pure POCO, [XmlRoot]) — 纯数据层                    │
│  职责: XML 序列化/反序列化载体                         │
│  特性: public 字段, 默认值, 无业务逻辑                  │
│  流向: 运行时全部通过 Manager.Settings 访问            │
└──────────────────────────────────────────────────────┘
```

### 1.1 第1层：数据层 (SettingsData)

纯数据容器，没有任何逻辑。直接用 `public` 字段（非属性）以简化 XML 序列化。

```csharp
[XmlRoot("LegacyWorldSettings")]
public class LegacyWorldSettingsData
{
    // 基础控制
    public bool Enabled = true;
    public bool AutoExportOnSave = true;
    public bool LogEnabled = true;

    // 导入类别开关
    public bool RestoreKingdoms = true;
    public bool RestoreClans = true;
    public bool RestoreSettlements = true;
    public bool RestoreClanEconomy = true;
    public bool CreateMissingClans = false;
}
```

**关键规则**：
- 所有字段有合理的**默认值**
- 不使用属性（Property），只用字段 — `XmlSerializer` 对字段和属性都支持，但字段更简洁
- 标记 `[XmlRoot("...")]` 指定 XML 根元素名
- **零业务逻辑**

### 1.2 第2层：管理器 (SettingsManager)

静态类，负责所有同步和持久化操作。

```csharp
public static class LegacyWorldSettingsManager
{
    // 运行时数据（第1层实例）
    public static LegacyWorldSettingsData Settings { get; private set; }

    // 从 XML 加载（不存在则创建默认）
    public static void Load() { ... }

    // 保存到 XML
    public static void Save() { ... }

    // 从 MCM 模型同步到数据层
    public static void SyncFromMCM(LegacyWorldMCMSettings mcm) { ... }

    // 手动操作标志（MCM 按钮支持）
    private static bool _manualExportRequested;
    private static bool _manualApplyRequested;
    public static void RequestManualExport() => _manualExportRequested = true;
    public static bool TryConsumeManualExport() { ... }
    public static void RequestManualApply() => _manualApplyRequested = true;
    public static bool TryConsumeManualApply() { ... }
}
```

**关键职责**：
- `Load()` — 构造时调用，尝试从 XML 反序列化，文件不存在则用默认值
- `Save()` — 将当前 `Settings` 序列化到 XML
- `SyncFromMCM(mcm)` — 从 MCM 属性对象复制值到 `Settings` 字段
- `RequestManualXxx` / `TryConsumeManualXxx` — 消费 MCM 按钮触发的操作请求

### 1.3 第3层：MCM 模型 (MCMSettings)

继承 `AttributeGlobalSettings<T>`，用属性标注提供 MCM GUI。

```csharp
public class LegacyWorldMCMSettings : AttributeGlobalSettings<LegacyWorldMCMSettings>
{
    // ── 标识字段 ──
    public override string Id => "LegacyWorld_v4";
    public override string FolderName => "New_ZZZF";
    public override string DisplayName => "LegacyWorld";

    // ── 基础控制 ──
    [SettingPropertyBool("启用世界状态继承系统")]
    [SettingPropertyGroup("基础控制")]
    public bool Enabled { get; set; } = true;

    // ── 导入类别（分组） ──
    [SettingPropertyBool("恢复王国结构")]
    [SettingPropertyGroup("导入数据类别")]
    public bool RestoreKingdoms { get; set; } = true;

    // ── 操作按钮（布尔触发，见第二节） ──
    private bool _manualExportTrigger;
    [SettingPropertyBool("手动导出（拨动即触发）")]
    [SettingPropertyGroup("操作按钮")]
    public bool ManualExportTrigger
    {
        get => _manualExportTrigger;
        set
        {
            if (value) LegacyWorldSettingsManager.RequestManualExport();
            _manualExportTrigger = value;
        }
    }

    // ── 构造时从 XML 载入初始值 ──
    public override void OnLoad()
    {
        var data = LegacyWorldSettingsManager.Settings;
        Enabled = data.Enabled;
        RestoreKingdoms = data.RestoreKingdoms;
        // ... 其余字段
    }

    // ── 属性变更通知 → 同步到 XML ──
    public override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        LegacyWorldSettingsManager.SyncFromMCM(this);
    }
}
```

---

## 二、MCMv5 按钮触发模式

### 问题背景

MCMv5 不支持在 `AttributeGlobalSettings` 中对方法使用 `[SettingPropertyButton]` 属性。直接标注方法会导致编译错误。

### 解决方案：布尔触发标志

将"按钮"实现为 **bool 属性**，设值时触发操作、立即复位。

```csharp
// ── MCM 模型侧 ──
private bool _manualExportTrigger;

[SettingPropertyBool("手动导出（拨动即触发）")]
public bool ManualExportTrigger
{
    get => _manualExportTrigger;
    set
    {
        if (value)           // 仅当拨动到 true 时触发
            LegacyWorldSettingsManager.RequestManualExport();
        _manualExportTrigger = value;  // 存储当前值（显示上会自动变为 false）
    }
}
```

```csharp
// ── Manager 侧 ──
private static bool _manualExportRequested;

public static void RequestManualExport()
{
    _manualExportRequested = true;
    AffixLogger.Info("MCM", "手动导出请求已注册");
}

public static bool TryConsumeManualExport()
{
    if (!_manualExportRequested) return false;
    _manualExportRequested = false;  // 消费后复位
    return true;
}
```

```csharp
// ── CampaignBehavior（主线程）侧 — 消费请求 ──
public void OnTick(float dt)
{
    if (LegacyWorldSettingsManager.TryConsumeManualExport())
        LegacyService.Export();
    if (LegacyWorldSettingsManager.TryConsumeManualApply())
        LegacyService.ForceImport();
}
```

**数据流时序**：
```
1. 玩家拨动 MCM 按钮 → true
2. OnPropertyChanged → ManualExportTrigger setter → RequestManualExport()
3. HourlyTickEvent → TryConsumeManualExport() → true → 执行 Export()
4. 按钮在 MCM 面板上自动复位为 false
```

### 适用场景

- 手动导出 / 手动导入 / 手动保存 / 手动同步
- 任何需要用户点击触发的**一次性操作**

---

## 三、MCM ↔ XML 双向同步机制

### 初始化时序

```
SubModule.OnSubModuleLoad()
  ├── LegacyWorldSettingsManager.Load()     ← 从 XML 读/创建默认
  │    └── XML 存在 → 反序列化 → Settings
  │    └── XML 不存在 → new SettingsData() 默认值 → Save()
  │
  ├── 注册 LegacyBehavior
  │    └── LegacyWorldMCMSettings 实例由 MCM 自动创建
  │         └── OnLoad() 回调 → 从 Settings 读取值 → 初始化 UI
  │
  └── 行为就绪，等待玩家操作
```

### 修改时序（玩家在 MCM 面板中更改）

```
MCM 面板 → 玩家勾选/改值
  → MCM 框架设置属性值
  → OnPropertyChanged("RestoreKingdoms")
  → LegacyWorldMCMSettings.OnPropertyChanged()
  → LegacyWorldSettingsManager.SyncFromMCM(this)  ← 复制到 Data 层
  → LegacyWorldSettingsManager.Save()              ← 写入 XML 文件
```

### 运行时读取

所有子系统和行为**只通过 Manager 读取**数据层：

```csharp
// ✅ 正确方式
var data = LegacyWorldSettingsManager.Settings;
if (data.Enabled) { ... }

// ❌ 禁止直接引用 MCM 模型
// if (LegacyWorldMCMSettings.Instance.Enabled)  /* 不要这样 */
```

---

## 四、初始化时从 XML 恢复 MCM 默认值

MCM 的 `AttributeGlobalSettings` 在构造时会用属性初始值填充 UI。为了让 UI 显示与 XML 持久化一致，需要在 `OnLoad()` 中重写值。

```csharp
public override void OnLoad()
{
    // 此时 Manager.Settings 已从 XML 加载完毕
    var data = LegacyWorldSettingsManager.Settings;
    this.Enabled = data.Enabled;
    this.AutoExportOnSave = data.AutoExportOnSave;
    this.LogEnabled = data.LogEnabled;
    this.RestoreKingdoms = data.RestoreKingdoms;
    this.RestoreClans = data.RestoreClans;
    // ...
}
```

**注意**：`OnLoad()` 在 MCM 框架初始化时自动调用，
**不会触发** `OnPropertyChanged`（否则会导致循环同步）。

---

## 五、事件选择注意事项

在 Bannerlord 中，不同版本的 API 存在差异。从 PST 和 LegacyWorld 实践中记录：

| 事件 | 可用性 | 替代方案 |
|------|--------|----------|
| `CampaignTickEvent` | v1.2.x **不存在** | `HourlyTickEvent`（每小时触发）或 `DailyTickEvent`（每天触发） |
| `OnBeforeSaveEvent` | ✅ 可用 | — |
| `OnNewGameCreatedEvent` | ✅ 可用 | 新游戏创建时触发 |
| `OnGameLoadedEvent` | ✅ 可用 | 载入存档时触发 |
| `OnMissionTick` | ⚠️ 仅 Mission 中 | 地图场景中不可用 |

**经验**：做 MCM 按钮消费时，`HourlyTickEvent` 是安全的选择，它确保 Campaign 线程已完全初始化。缺点是触发频率低（每小时一次），但对于导出/导入这类操作完全足够。如果需要更及时响应，可以用 `CampaignEvents.TickEvent` 但需注意线程安全。

---

## 六、多 MCM 菜单页

一个 Mod 可以有多个 MCM 菜单页，每个 `AttributeGlobalSettings<T>` 子类对应一个。

```csharp
// ── 菜单页 1 ──
public class SettingsPage1 : AttributeGlobalSettings<SettingsPage1>
{
    public override string Id => "MyMod_Page1";
    public override string FolderName => "MyMod";
    public override string DisplayName => "页面1";
    // ...
}

// ── 菜单页 2 ──
public class SettingsPage2 : AttributeGlobalSettings<SettingsPage2>
{
    public override string Id => "MyMod_Page2";
    public override string FolderName => "MyMod";
    public override string DisplayName => "页面2";
    // ...
}
```

结果：
```
Mod Options
  └─ MyMod
       ├─ 页面1  ← SettingsPage1
       └─ 页面2  ← SettingsPage2
```

**关键规则**：
- `FolderName` 相同 → 归为同一 Mod 分组
- `Id` 必须全局唯一（建议 Mod名 + 版本号 + 页名）
- 每个页可以共享同一个 Manager/Data 层，也可以各自独立
- 如果共享数据，`SyncFromMCM` 中注意不要覆盖其他页管理的字段

---

## 七、文档同步最佳实践

从本次对话中总结的文档同步经验：

### 7.1 文档与代码的对应关系

| 代码变化 | 需要同步的文档 |
|----------|---------------|
| 新增/修改类 | `ARCHITECTURE.md`（架构图、类职责） |
| 新增/修改数据模型 | `DATA_DESIGN.md`（字段、类型、默认值） |
| 新增/修改业务流程 | `IMPORT_EXPORT_DESIGN.md`（流程、时序） |
| 新增/修改功能 | `PRODUCT_SPEC.md` + `README.md` |
| 版本发布 | `CHANGELOG.md` + `VERSION_PLAN.md` |
| 修复 Bug | `KNOWN_ISSUES.md`（转到已解决） |
| 技术模式沉淀 | 本手册这样的 `技术参考/*.md` |

### 7.2 文档同步时机

- **功能完成时**：立即更新 README / CHANGELOG
- **架构变更时**：更新 ARCHITECTURE
- **版本发布时**：更新 CHANGELOG + VERSION_PLAN
- **新技术模式成熟时**：写入 `技术参考/` 供后续复用

---

## 八、快速模板

### 新子系统 MCM 启动模板

```csharp
// === File1: XxxSettingsData.cs ===
[XmlRoot("XxxSettings")]
public class XxxSettingsData
{
    public bool Enabled = true;
    // ... 其他字段，都有默认值
}

// === File2: XxxSettingsManager.cs ===
public static class XxxSettingsManager
{
    private static readonly string _filePath = ...;
    public static XxxSettingsData Settings { get; private set; } = new();

    public static void Load()
    {
        if (File.Exists(_filePath))
            Settings = XxxSerializer.Deserialize(_filePath); // 可选: 反射轮询读XML
        else
            Save(); // 创建默认
    }
    public static void Save() { ... /* XmlSerializer */ }
    public static void SyncFromMCM(XxxMCMSettings mcm) { ... /* 逐字段复制 */ }
}

// === File3: XxxMCMSettings.cs ===
public class XxxMCMSettings : AttributeGlobalSettings<XxxMCMSettings>
{
    public override string Id => "Xxx_v1";
    public override string FolderName => "New_ZZZF";
    public override string DisplayName => "Xxx系统";

    [SettingPropertyBool("启用")]
    public bool Enabled { get; set; } = true;

    public override void OnLoad()
    {
        var data = XxxSettingsManager.Settings;
        Enabled = data.Enabled;
        // ...
    }
    public override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        XxxSettingsManager.SyncFromMCM(this);
    }
}
```

**文件数量**：1 个子系统 = 3 个文件（Data + Manager + MCMSettings），职责清晰，易于维护。
