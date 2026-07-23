# LegacyWorld 核心功能技术参考手册

> **概要**：本文档记录 LegacyWorld 世界状态继承系统的核心功能技术实现，包括适配器模式、三阶段导入编排、所有权变更、防重复机制等，以及开发过程中遇到的 Bug 和修复经验。适用于需要实现类似"跨存档数据继承"功能的参考。
>
> **来源**：New_ZZZF → LegacyWorld (v0.1.0 ~ v0.4.0)

---

## 一、架构总览

LegacyWorld 采用**四层架构**，用适配器模式彻底隔离游戏 API 依赖。

```
┌──────────────────────────────────────────────┐
│        Bannerlord Layer (运行时集成)           │
│  LegacyBehavior / LegacyService / SubModule   │
├──────────────────────────────────────────────┤
│     BannerlordAdapter Layer (适配器实现)       │
│  BannerlordGameAdapter / ObjectFinder         │
│  SettlementChangeFactory                     │
├──────────────────────────────────────────────┤
│       Adapter Interface (抽象隔离层)           │
│           IGameAdapter                       │
├──────────────────────────────────────────────┤
│          Core Layer (纯业务逻辑)              │
│  Export / Import / Models / Serialization    │
│  Settings / Storage / AffixLogger            │
└──────────────────────────────────────────────┘
```

### 核心依赖规则

```
BannerlordLayer → BannerlordAdapterLayer → AdapterInterface → CoreLayer
```

- **Core 层**：零依赖 `TaleWorlds.*`，只引用标准库 + Newtonsoft.Json
- **Adapter 接口**：定义在 Core 层的 `Adapter/` 命名空间
- **BannerlordAdapter**：实现接口，引用 `TaleWorlds.CampaignSystem`
- **Bannerlord**：注册行为、监听事件，引用所有下层

---

## 二、适配器模式 (IGameAdapter)

### 2.1 为什么需要适配器

Bannerlord 的 API 在不同版本（v1.1.x / v1.2.x / v1.3.x）之间常有破坏性变更。将游戏 API 调用集中到适配器中，Core 层完全不知道 `Kingdom`、`Clan`、`Settlement` 类的存在，版本升级时只需改适配器。

### 2.2 接口设计

```csharp
// ── Core/Adapter/IGameAdapter.cs ──
public interface IGameAdapter
{
    // 世界信息
    string GetWorldId();
    string GetCurrentGameTime();
    string GetDominantCulture();
    string GetGameVersion();

    // 王国操作
    IEnumerable<IKingdomInfo> GetAllKingdoms();
    IKingdomInfo FindKingdom(string id);
    void SetKingdomRuler(IKingdomInfo kingdom, IClanInfo ruler);

    // 家族操作
    IEnumerable<IClanInfo> GetAllClans();
    IClanInfo FindClan(string id);
    IClanInfo CreateClan(string id, string name, string culture);
    void SetClanKingdom(IClanInfo clan, IKingdomInfo kingdom);
    void SetClanGold(IClanInfo clan, long gold);
    void SetClanRenown(IClanInfo clan, float renown);
    void SetClanInfluence(IClanInfo clan, float influence);

    // 定居点操作
    IEnumerable<ISettlementInfo> GetAllSettlements();
    ISettlementInfo FindSettlement(string id);
    void ChangeSettlementOwner(ISettlementInfo settlement, IClanInfo newOwner);
    void SetSettlementProsperity(ISettlementInfo settlement, float prosperity);
}
```

### 2.3 数据传输对象接口

适配器同时定义了三个信息接口，返回给 Core 层使用：

```csharp
public interface IKingdomInfo
{
    string Id { get; }
    string Name { get; }
    IClanInfo RulerClan { get; }
    string Culture { get; }
}

public interface IClanInfo
{
    string Id { get; }
    string Name { get; }
    IKingdomInfo Kingdom { get; }
    int Tier { get; }
    long Gold { get; }
    float Renown { get; }
    float Influence { get; }
    bool IsDestroyed { get; }
}

public interface ISettlementInfo
{
    string Id { get; }
    string Name { get; }
    string Type { get; }  // "Town" / "Castle" / "Village"
    IClanInfo OwnerClan { get; }
    IKingdomInfo OwnerKingdom { get; }
    string Culture { get; }
    float Prosperity { get; }
}
```

### 2.4 Bannerlord 实现

```csharp
// ── BannerlordAdapter/BannerlordGameAdapter.cs ──
public class BannerlordGameAdapter : IGameAdapter
{
    public string GetWorldId()
    {
        // 用游戏版本 + 时间戳生成唯一世界 ID
        var campaign = Campaign.Current;
        return $"{campaign.GameVersion}_{campaign.CampaignStartTime.Ticks}";
    }

    public IClanInfo FindClan(string id)
    {
        var clan = Clan.All.FirstOrDefault(c => c.StringId == id);
        return clan != null ? new ClanInfoWrapper(clan) : null;
    }

    public void ChangeSettlementOwner(ISettlementInfo settlement, IClanInfo newOwner)
    {
        // 委托给 SettlementChangeFactory
        SettlementChangeFactory.ChangeOwner(settlement, newOwner);
    }

    // ── 内部包装类 ──
    private class ClanInfoWrapper : IClanInfo
    {
        private readonly Clan _clan;
        public ClanInfoWrapper(Clan clan) => _clan = clan;

        public string Id => _clan.StringId;
        public string Name => _clan.Name.ToString();
        public long Gold => _clan.Gold;
        // ...
    }
}
```

> **经验**：包装类模式（Wrapper）比复制数据更安全，始终拿到最新状态，但需要确保 `_clan` 引用在包装类生命周期内有效。

---

## 三、三阶段导入编排

### 3.1 为什么是三个阶段

导入必须按依赖顺序执行，否则会因引用缺失而失败：

```
Phase 1: Kingdom
  │ 设置王国统治者 → 引用 Clan
  ▼
Phase 2: Clan
  │ 设置所属 Kingdom → 引用 Kingdom（Phase 1 已恢复）
  │ 设置金币/声望/影响力
  ▼
Phase 3: Settlement
  │ 变更拥有者 → 引用 Clan（Phase 2 已恢复）
  │ 设置繁荣度
```

### 3.2 LegacyImporter 编排器

```csharp
// ── Core/Import/LegacyImporter.cs ──
public class ImportResult
{
    public int KingdomsRestored { get; set; }
    public int ClansRestored { get; set; }
    public int SettlementsRestored { get; set; }
}

public static class LegacyImporter
{
    public static ImportResult Apply(
        IGameAdapter adapter,
        LegacyData data,
        LegacySettings settings)
    {
        var result = new ImportResult();

        // Phase 1: 王国
        result.KingdomsRestored = KingdomImporter.Restore(adapter, data, settings);

        // Phase 2: 家族
        result.ClansRestored = ClanImporter.Restore(adapter, data, settings);

        // Phase 3: 定居点
        result.SettlementsRestored = SettlementImporter.Restore(adapter, data, settings);

        return result;
    }
}
```

### 3.3 分类开关过滤

每个 Importer 在进入恢复循环前检查 MCM 分类开关：

```csharp
// ── KingdomImporter 片段 ──
public static int Restore(IGameAdapter adapter, LegacyData data, LegacySettings settings)
{
    if (!settings.RestoreKingdoms)
    {
        AffixLogger.Info("KINGDOMIMP", "【恢复王国结构】已关闭，跳过");
        return 0;
    }
    int count = 0;
    foreach (var ks in data.Kingdoms)
    {
        var kingdom = adapter.FindKingdom(ks.Id);
        if (kingdom == null) continue;
        var ruler = adapter.FindClan(ks.RulerClanId);
        if (ruler == null) continue;
        adapter.SetKingdomRuler(kingdom, ruler);
        count++;
    }
    return count;
}
```

---

## 四、SettlementChangeFactory 所有权变更

### 4.1 为什么需要专用工厂

直接设置 `Settlement.OwnerClan` 不会触发游戏内的 `OnFortificationAdded` / `OnFortificationRemoved` 回调，导致 UI 不刷新、绑定村庄不同步。

### 4.2 实现

```csharp
// ── BannerlordAdapter/Factories/SettlementChangeFactory.cs ──
public static class SettlementChangeFactory
{
    public static void ChangeOwner(ISettlementInfo settlement, IClanInfo newOwner)
    {
        // 通过适配器获取游戏原生对象
        var gameSettlement = FindGameSettlement(settlement.Id);
        if (gameSettlement == null) return;

        var gameClan = FindGameClan(newOwner.Id);
        if (gameClan == null) return;

        // 使用 Town.OwnerClan setter（会触发内置回调）
        if (gameSettlement.IsTown || gameSettlement.IsCastle)
        {
            gameSettlement.Town.OwnerClan = gameClan;
        }
        else if (gameSettlement.IsVillage)
        {
            // 村庄：通过绑定城镇间接变更
            var boundTown = gameSettlement.Village.Bound.Town;
            boundTown.OwnerClan = gameClan;
        }
    }
}
```

> **经验**：`Town.OwnerClan` setter 内部调用了 `Clan.OnFortificationRemoved()` 和 `Clan.OnFortificationAdded()`，确保：
> - 旧 Clan 的领地计数减少
> - 新 Clan 的领地计数增加
> - 地图图标和颜色立即刷新
> - 绑定村庄的视觉跟随更新

---

## 五、防重复导入机制

### 5.1 问题

每个存档只应导入一次。如果玩家在导入后手动导出再保存/加载，不应该再次触发导入。

### 5.2 实现：IDataStore 序列化标志

```csharp
// ── Bannerlord/LegacyBehavior.cs ──
public class LegacyBehavior : CampaignBehaviorBase
{
    private bool _applied;          // 当前存档是否已导入
    private string _appliedWorldId; // 已应用的世界 ID（备份用）

    // ── 存档时保存标志 ──
    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_applied", ref _applied);
        dataStore.SyncData("_appliedWorldId", ref _appliedWorldId);
    }

    // ── 新游戏创建 → 首次导入 ──
    protected override void OnNewGameCreated(CampaignGameStarter starter)
    {
        // 新游戏时必定 _applied == false
        if (LegacyWorldSettingsManager.Settings.Enabled)
            LegacyService.Import();
    }

    // ── 存档加载后 → 防止重复 ──
    protected override void OnGameLoaded(CampaignGameStarter starter)
    {
        // 从 SyncData 恢复 _applied 状态
        // _applied = true → 本存档已导入过，不再触发
    }
}
```

### 5.3 数据流

```
[新游戏]
  → OnNewGameCreated → SyncData 尚未调用，_applied = false
  → 执行导入
  → _applied = true

[保存游戏]
  → OnBeforeSave 触发导出
  → SyncData 将 _applied = true 序列化到存档

[再次加载该存档]
  → OnGameLoaded → SyncData 恢复 _applied = true
  → 条件判断 _applied == true → 跳过导入

[开始另一个全新游戏]
  → OnNewGameCreated → _applied = false（新实例）
  → 可以导入
```

---

## 六、同世界检测

### 6.1 问题

如果玩家在存档 A 中手动导出，然后立即在同一个存档 A 中手动应用，不应该允许 —— 用本世界的遗产覆盖本世界没有意义，可能导致状态混乱。

### 6.2 实现

```csharp
// ── Bannerlord/LegacyService.cs ──
public static bool Import(string worldId = null)
{
    var legacyData = LoadLegacyData();
    if (legacyData == null) return false;

    // 同世界检测
    string currentWorldId = _adapter.GetWorldId();
    if (currentWorldId == legacyData.WorldId)
    {
        AffixLogger.Warn("SERVICE",
            $"同世界遗产（当前世界={currentWorldId}），跳过导入");
        return false;
    }

    return ApplyImport(legacyData);
}

// 强制导入（跳过同世界检测，给 MCM«手动应用»用）
public static void ForceImport(string worldId = null)
{
    var legacyData = LoadLegacyData();
    if (legacyData == null) return;
    ApplyImport(legacyData);
}
```

### 6.3 两个 public 方法的对比

| 方法 | 同世界检测 | 触发来源 |
|------|-----------|----------|
| `Import()` | ✅ 拒绝自我覆盖 | 自动：`OnNewGameCreated` |
| `ForceImport()` | ❌ 跳过检测 | 手动：MCM «手动应用» |

---

## 七、JSON 序列化 (LegacySerializer)

### 7.1 使用 Newtonsoft.Json

```csharp
// ── Core/Serialization/LegacySerializer.cs ──
public static class LegacySerializer
{
    private static readonly JsonSerializerSettings _settings = new()
    {
        Formatting = Formatting.Indented,           // 格式化输出，方便调试
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()  // 小写蛇形
        }
    };

    public static string Serialize(LegacyData data)
    {
        return JsonConvert.SerializeObject(data, _settings);
    }

    public static LegacyData Deserialize(string json)
    {
        return JsonConvert.DeserializeObject<LegacyData>(json, _settings);
    }
}
```

### 7.2 JSON 示例

```json
{
  "version": "0.4.0",
  "world_id": "v1.2.10.164250_1689977024",
  "created_at": "2026-07-23T12:00:00",
  "kingdoms": [
    {
      "id": "kingdom_empire",
      "name": "Southern Empire",
      "ruler_clan_id": "clan_rhagaea"
    }
  ],
  "clans": [
    {
      "id": "clan_rhagaea",
      "name": "Rhagaea",
      "kingdom_id": "kingdom_empire",
      "tier": 6,
      "gold": 50000,
      "renown": 3500.0,
      "influence": 1200.0,
      "is_destroyed": false
    }
  ],
  "settlements": [
    {
      "id": "town_EP1",
      "name": "Epicrotea",
      "type": "Town",
      "owner_clan_id": "clan_rhagaea",
      "owner_kingdom_id": "kingdom_empire",
      "prosperity": 4500.0
    }
  ]
}
```

> **经验**：使用 `SnakeCaseNamingStrategy` 使 JSON 字段名与游戏内部约定一致，便于手动调试。

---

## 八、AffixLogger 日志系统

### 8.1 设计要点

```csharp
// ── Core/AffixLogger.cs ──
public static class AffixLogger
{
    private static readonly string _logPath;
    private static readonly object _lock = new();

    static AffixLogger()
    {
        // 日志位置：模块根目录（与 SubModule.xml 同目录）
        string modulePath = Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location);
        _logPath = Path.Combine(modulePath, "affix_debug.log");
    }

    // 运行时日志开关（从 MCM 数据层读取）
    public static bool LogEnabled
    {
        get => LegacyWorldSettingsManager.Settings.LogEnabled;
    }

    public static void Info(string tag, string message)
    {
        if (!LogEnabled) return;
        WriteLog("INFO", tag, message);
    }

    public static void Error(string tag, string message, Exception ex = null)
    {
        WriteLog("ERROR", tag, $"{message} | {ex?.Message}");
    }

    private static void WriteLog(string level, string tag, string message)
    {
        lock (_lock)  // 多线程安全
        {
            File.AppendAllText(_logPath,
                $"[{DateTime.Now:HH:mm:ss}][{level}][{tag}] {message}{Environment.NewLine}");
        }
    }
}
```

### 8.2 日志标签规范

| 标签 | 用途 |
|------|------|
| `BEHAVIOR` | LegacyBehavior 生命周期事件 |
| `SERVICE` | LegacyService 导入/导出流程 |
| `KINGDOMIMP` | KingdomImporter 恢复详情 |
| `CLANIMP` | ClanImporter 恢复详情 |
| `SETTLEIMPORT` | SettlementImporter 恢复详情 |
| `MCM` | MCM 按钮触发/消费 |
| `EXPORT` | 导出过程详情 |

### 8.3 运行时开关

日志开关通过 MCM 面板控制，`LogEnabled` 每次调用时实时读取，**无需重启生效**。

---

## 九、Bug 修复记录

### Bug 1: CampaignTickEvent 不存在

| 项目 | 内容 |
|------|------|
| **症状** | 编译错误: `CampaignTickEvent` 找不到 |
| **原因** | Bannerlord v1.2.x 中此事件已被移除或替换 |
| **解决** | 改为 `HourlyTickEvent`（每小时触发一次，足够消费 MCM 按钮） |
| **教训** | 开发前应确认目标游戏版本支持的 API。不确定时用 `CampaignEvents` 静态类中最保守的选择 |

### Bug 2: SettingPropertyButton 不支持方法

| 项目 | 内容 |
|------|------|
| **症状** | 编译错误: `SettingPropertyButton` 不能放在方法上 |
| **原因** | MCMv5 的 `AttributeGlobalSettings` 不支持方法级属性，只支持属性 |
| **解决** | 改用 **bool 触发标志**模式（如 `ManualExportTrigger`），设值时触发操作后立即复位 |
| **教训** | 使用第三方库前应阅读其 API 兼容性说明。MCMv5 只能做属性绑定，不能做方法绑定 |

### Bug 3: LegacySettings 被误删除

| 项目 | 内容 |
|------|------|
| **症状** | 编译错误: 导入模块引用 `LegacySettings` 但类不存在 |
| **原因** | 重构时以为配置文件已完全由 MCM 替代，删除了 `LegacySettings.cs`，但 Import 模块仍引用它作为参数 |
| **解决** | 恢复 `LegacySettings` 为 Pure POCO，保留字段定义，数据来源改为 MCM 数据层 |
| **教训** | 删除类前应搜索所有引用。即使是"废弃"代码，如果仍有调用者就不能直接删 |

### Bug 4: 旧 JSON 配置文件遗留

| 项目 | 内容 |
|------|------|
| **症状** | 配置逻辑分散：部分在 `LegacyWorldConfig.cs` + `LegacyWorldConfig.json`，部分在 MCM |
| **原因** | 项目最初使用 JSON 配置文件，后期迁移到 MCM 时未清理 |
| **解决** | 删除 `LegacyWorldConfig.cs` 和 `LegacyWorldConfig.json`，统一为 MCM + XML 持久化 |
| **教训** | 配置方案只能选一种。迁移旧方案时应彻底清理，避免两套并存 |

### Bug 5: Ctrl+F10/F11 快捷键与游戏冲突

| 项目 | 内容 |
|------|------|
| **症状** | 快捷键可能与玩家其他 Mod 或游戏内置按键冲突，且用户无法自定义 |
| **原因** | v0.3.0 在 `SubModule.cs` 中硬编码了 `Ctrl+F10`(导出) 和 `Ctrl+F11`(导入) |
| **解决** | 移除快捷键，全部改为 MCM 菜单操作按钮 |
| **教训** | Mod 功能操作应该通过 MCM 或游戏内置 UI，避免硬编码快捷键 |

### Bug 6: 载入存档也触发导入

| 项目 | 内容 |
|------|------|
| **症状** | 玩家载入一个已有存档时，`Legacy.json` 被自动导入，污染已有进度 |
| **原因** | `OnGameLoadedEvent` 中也挂了导入逻辑，与 `OnNewGameCreatedEvent` 行为相同 |
| **解决** | 导入仅绑定 `OnNewGameCreatedEvent`，载入存档只恢复 `_applied` 标志状态 |
| **教训** | `OnGameLoaded` 和 `OnNewGameCreated` 的语义完全不同，不能混用。后者用于初始化新世界，前者用于恢复已存世界 |

---

## 十、关键经验总结

### 10.1 适配器模式的价值

- Core 层 0 依赖 TaleWorlds → 可以独立单元测试
- 游戏版本升级只需改一个 `BannerlordGameAdapter`
- 将来支持其他游戏（如果有）只需实现 `IGameAdapter`

### 10.2 事件驱动优于轮询

- 从 v0.3.0 的 `DailyTick` 改为事件驱动后，代码更清晰
- 导出/导入只在需要时触发，不影响游戏性能
- `HourlyTickEvent` 用于消费 MCM 按钮请求，频率低但足够

### 10.3 存档兼容性

- 使用 `IDataStore.SyncData()` 序列化运行时状态（`_applied` 标志）
- 新版本 Mod 加载旧存档时，旧存档没有该数据字段 → `SyncData` 会给默认值（false）
- 保证了**向前兼容**：旧存档加载后默认允许导入一次

### 10.4 防御性编程

```csharp
// 所有 Importer 都做空值检查，单个失败不影响其余
var kingdom = adapter.FindKingdom(ks.Id);
if (kingdom == null)
{
    AffixLogger.Warn("KINGDOMIMP", $"找不到王国 {ks.Id}，跳过");
    continue;  // 单个跳过，继续下一个
}
```

### 10.5 文件组织规范

```
LegacyWorld/
├── Adapter/                       # 接口定义（Core 层）
│   └── IGameAdapter.cs
├── Bannerlord/                    # 运行时集成
│   ├── LegacyBehavior.cs          # CampaignBehavior
│   ├── LegacyService.cs           # 静态入口
│   └── LegacySubModule.cs         # 独立 MBSubModule
├── BannerlordAdapter/             # 适配器实现
│   ├── BannerlordGameAdapter.cs
│   ├── ObjectFinder.cs
│   └── Factories/
│       └── SettlementChangeFactory.cs
└── Core/                          # 纯业务逻辑
    ├── AffixLogger.cs
    ├── Export/
    │   └── LegacyExporter.cs
    ├── Import/
    │   ├── LegacyImporter.cs
    │   ├── KingdomImporter.cs
    │   ├── ClanImporter.cs
    │   └── SettlementImporter.cs
    ├── Models/
    │   ├── LegacyData.cs
    │   ├── KingdomState.cs
    │   ├── ClanState.cs
    │   └── SettlementState.cs
    ├── Serialization/
    │   └── LegacySerializer.cs
    ├── Settings/                   # 三层设置系统
    │   ├── LegacySettings.cs
    │   ├── LegacyWorldMCMSettings.cs
    │   ├── LegacyWorldSettingsData.cs
    │   └── LegacyWorldSettingsManager.cs
    └── Storage/
        └── LegacyStorage.cs
```

> 共计 23 个文件，每层有明确目录，一目了然。
