# 技术架构设计

## 1. 整体架构概览

LegacyWorld 采用**分层架构**，将游戏 API 依赖、业务逻辑和数据模型严格分离。

```
┌──────────────────────────────────────────────┐
│            Bannerlord Layer                   │
│  LegacyBehavior / LegacyService / SubModule   │
│  (CampaignBehavior / 静态入口 / MBSubModule)   │
├──────────────────────────────────────────────┤
│           BannerlordAdapter Layer             │
│  BannerlordGameAdapter / ObjectFinder         │
│  SettlementChangeFactory                     │
├──────────────────────────────────────────────┤
│              Adapter Interface               │
│          IGameAdapter (抽象隔离层)            │
├──────────────────────────────────────────────┤
│                Core Layer                    │
│  Export / Import / Models / Serialization     │
│  Settings / Storage / AffixLogger            │
└──────────────────────────────────────────────┘
```

### 依赖规则

- **严格单向**：Bannerlord → BannerlordAdapter → Adapter → Core
- Core 层**零依赖**于 TaleWorlds.\* 程序集
- 所有跨层调用通过 `IGameAdapter` 接口进行

## 2. 设置系统架构（三层模型）

参照 `ProjectileTrajectorySystem/Settings/` 的三层架构。

```
┌───────────────────────────────────────────────────┐
│ 第3层: LegacyWorldMCMSettings                      │
│ (AttributeGlobalSettings) MCM GUI 模型             │
│ ─ MCM 属性变更 → LegacyWorldSettingsManager        │
└────────────────────┬──────────────────────────────┘
                     │ OnPropertyChanged
                     ▼
┌───────────────────────────────────────────────────┐
│ 第2层: LegacyWorldSettingsManager                  │
│ (静态管理器)                                        │
│ ─ SyncFromMCM(): MCM → Data → XML                  │
│ ─ Load()/Save(): XML 读写                          │
│ ─ RequestManualExport/Apply 操作标志               │
└────┬──────────────────────────┬───────────────────┘
     │ Load/Save               │ TryConsume
     ▼                          ▼
┌───────────────────────────────────────────────────┐
│ 第1层: LegacyWorldSettingsData                     │
│ (Pure POCO, XmlRoot)                               │
│ ─ XML 序列化/反序列化                               │
│ ─ 运行时访问: LegacyWorldSettingsManager.Settings  │
└───────────────────────────────────────────────────┘
```

**数据流**：
1. MCM 面板修改 → `OnPropertyChanged` → `SyncFromMCM()` → 更新 Data → 保存 XML
2. 运行时读取 → `LegacyWorldSettingsManager.Settings` (Data 对象)
3. 构造时 → 从 XML 加载 → MCM 读取 Data 值初始化 UI

## 3. 核心运行时架构

### 3.1 事件驱动生命周期

```
SubModule (OnSubModuleLoad)
  └─ Init SettingsManager.Load()
  └─ Register LegacyBehavior → campaignGameStarter.AddBehavior()

LegacyBehavior (CampaignBehaviorBase)
  ├─ OnBeforeSave ─► LegacyService.Export()
  ├─ OnNewGameCreated ─► LegacyService.Import()  (仅新游戏)
  └─ HourlyTick ─► TryConsumeManualExport/Apply  (MCM 按钮)

LegacyService (静态入口)
  ├─ Initialize() → 创建 IGameAdapter
  ├─ Export() → LegacyExporter.ExportWorld + ExportHeroes（双文件）
  ├─ Import() → 合并双文件 + applied_world_ids 去重 → LegacyImporter
  └─ ForceImport() → 跳过检测 → LegacyImporter

LegacyImporter (编排器)
  ├─ Phase 1: KingdomImporter
  ├─ Phase 2: ClanImporter    (依赖 Phase 1)
  └─ Phase 3: SettlementImporter (依赖 Phase 1+2)
```

### 3.2 防重复导入机制

已复刻过的遗产来源世界持久化在 `LegacyHeroes.json` 的 `applied_world_ids`，**跨进程生效**：

```csharp
// LegacyHeroes.json
{
  "profiles": [ ... ],
  "applied_world_ids": ["worldA", "worldB"]   // 已复刻过的遗产世界，重开游戏仍生效
}

// LegacyService.Import()
// - 仅当 foreignWorlds（遗产中非当前世界的来源）不在 applied_world_ids 时才复刻
// - 成功后把 foreignWorlds 写回 applied_world_ids
```

> 与玩家人物遗产共用同一文件，避免额外 sidecar；同进程与跨进程均不会重复复刻同一世界的遗留 NPC。

### 3.3 MCM 按钮触发机制

MCMv5 不支持方法按钮，采用**布尔触发标志**模式：

```csharp
[SettingPropertyBool("手动导出（拨动即触发）")]
public bool ManualExportTrigger { /* set { if(value) Manager.RequestManualExport() } */ }

// HourlyTickEvent 中消费:
if (LegacyWorldSettingsManager.TryConsumeManualExport())
    LegacyService.Export();
```

## 4. 命名空间布局

| 命名空间 | 职责 |
|----------|------|
| `New_ZZZF.LegacyWorld.Adapter` | IGameAdapter 接口定义 |
| `New_ZZZF.LegacyWorld.Bannerlord` | Campaign 集成层 |
| `New_ZZZF.LegacyWorld.BannerlordAdapter` | 适配器实现层 |
| `New_ZZZF.LegacyWorld.Core` | 核心日志工具 |
| `New_ZZZF.LegacyWorld.Core.Export` | 导出引擎 |
| `New_ZZZF.LegacyWorld.Core.Import` | 导入引擎 |
| `New_ZZZF.LegacyWorld.Core.Models` | 数据模型 |
| `New_ZZZF.LegacyWorld.Core.Serialization` | 序列化 |
| `New_ZZZF.LegacyWorld.Core.Settings` | 三层设置系统 |
| `New_ZZZF.LegacyWorld.Core.Storage` | 文件存储 |

## 5. 关键技术决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 配置方式 | MCM + XML 持久化 | 用户友好，无需手动编辑文件 |
| 导出格式 | JSON (Newtonsoft.Json) | 可读性好，易于调试 |
| 存储位置 | 模块根目录（`Legacy.json` 世界状态 + `LegacyHeroes.json` 玩家人物） | 与 `SubModule.xml` 同级，易于排查 |
| 适配器模式 | `IGameAdapter` 接口 | 隔离游戏版本 API 变更 |
| 导入顺序 | Kingdom → Clan → Settlement → Hero | 满足依赖约束（领地需要所属家族存在） |
| 所有权变更 | `Town.OwnerClan` setter | 触发游戏内 OnFortificationAdded/Removed |
