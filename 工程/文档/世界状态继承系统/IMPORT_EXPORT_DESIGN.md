# 世界继承机制设计

## 1. 导出机制 (Export)

### 1.1 触发条件

| 触发方式 | 条件 | 代码路径 |
|----------|------|----------|
| 自动导出 | 存档时（`OnBeforeSaveEvent`），且 `Enabled && AutoExportOnSave` 均为 true | `LegacyBehavior.OnBeforeSave()` → `LegacyService.Export()` |
| 手动导出 | MCM 按钮«手动导出»拨动触发，无条件 | `HourlyTick` 消费 `ManualExportTrigger` → `LegacyService.Export()` |

### 1.2 导出流程

```
LegacyService.Export()
  ├── 检查 Enable 主开关（自动导出时）
  ├── AffixLogger.Info("SERVICE", "开始导出...")
  ├── LegacyExporter.Export(adapter)
  │   ├── 遍历所有 Kingdom → KingdomState
  │   ├── 遍历所有 Clan → ClanState
  │   ├── 遍历所有 Settlement → SettlementState
  │   └── 组装 LegacyData（含版本号、WorldId、时间戳）
  ├── LegacySerializer.Serialize(legacyData) → JSON
  └── LegacyStorage.Write(json)
```

### 1.3 导出内容

- 所有王国（含统治者）
- 所有非隐藏家族（含所属王国、等级、金币、声望、影响力）
- 所有定居点（含拥有者、繁荣度）

## 2. 导入机制 (Import)

### 2.1 触发条件

| 触发方式 | 条件 | 代码路径 |
|----------|------|----------|
| 自动导入 | **仅新游戏**（`OnNewGameCreatedEvent`），且 `Enabled` 为 true | `LegacyBehavior.OnNewGameCreated()` → `LegacyService.Import()` |
| 手动应用 | MCM 按钮«手动应用»拨动触发，跳过同世界检测 | `HourlyTick` 消费 `ManualApplyTrigger` → `LegacyService.ForceImport()` |

> **重要**：载入已有存档（`OnGameLoadedEvent`）**不会**触发导入。
> 每个存档通过 `SyncData` 中的 `_applied` 标志保证整局只执行一次。

### 2.2 导入流程

```
LegacyService.Import(worldId)
  ├── 检查 Legacy.json 是否存在 → 否: 退出
  ├── LegacySerializer.Deserialize() → LegacyData
  ├── 同世界检测: currentWorldId == legacyData.WorldId?
  │   └── 是: 退出（ForceImport 跳过此步）
  ├── LegacyService.RefreshSettings()  ← 从 MCM 读取分类开关
  ├── LegacyImporter.Apply(adapter, legacyData, settings)
  │   ├── Phase 1: KingdomImporter.Restore()
  │   │   ├── 遍历 legacyData.Kingdoms
  │   │   ├── 根据 RestoreKingdoms 开关过滤
  │   │   ├── adapter.FindKingdom(id) → 设置统治者
  │   │   └── KingdomRestoredCount++
  │   ├── Phase 2: ClanImporter.Restore()
  │   │   ├── 遍历 legacyData.Clans
  │   │   ├── 根据 RestoreClans/CreateMissingClans/RestoreClanEconomy 开关
  │   │   ├── adapter.FindClan(id) / CreateClan(id)
  │   │   ├── 设置所属王国、金币、声望、影响力
  │   │   └── ClanRestoredCount++
  │   └── Phase 3: SettlementImporter.Restore()
  │       ├── 遍历 legacyData.Settlements
  │       ├── 根据 RestoreSettlements 开关过滤
  │       ├── adapter.FindSettlement(id)
  │       ├── ChangeSettlementOwner() / SetProsperity()
  │       └── SettlementRestoredCount++
  └── 标记 _applied = true（防重复）
```

### 2.3 导入顺序依赖

```
Phase 1: Kingdom 恢复
  │ 设置统治者家族引用
  ▼
Phase 2: Clan 恢复
  │ 家族需存在才能分配领地
  ▼
Phase 3: Settlement 恢复
  │ 拥有者家族必须已存在
```

## 3. 同世界检测

```csharp
public bool Import(string worldId = null)
{
    var legacyData = LoadLegacy();
    if (legacyData == null) return false;

    string currentWorldId = _adapter.GetWorldId();
    if (currentWorldId == legacyData.WorldId)
    {
        AffixLogger.Warn("SERVICE", "同世界遗产，跳过导入");
        return false; // 禁止用本世界的导出覆盖本世界
    }
    return ImportCore(legacyData);
}
```

- **自动导入**：执行同世界检测，禁止自我覆盖
- **手动应用**：调用 `ForceImport()` 跳过同世界检测，允许强制导入

## 4. 防重复导入

```csharp
// LegacyBehavior — 通过 IDataStore 随存档序列化
private bool _applied;
private string _appliedWorldId;

// Save: ISerializable 随存档保存
// Load: 从存档恢复 _applied=true → 不再触发导入
// OnNewGameCreated: _applied=false → 可以导入
```

## 5. 定居点所有权变更

使用 `SettlementChangeFactory` 确保正确的游戏内通知：

```csharp
public static void ChangeOwner(ISettlementInfo settlement, IClanInfo newOwner)
{
    // 使用 Town.OwnerClan setter
    // 触发: Clan.OnFortificationRemoved() (旧)
    // 触发: Clan.OnFortificationAdded() (新)
    // 绑定村庄: Village.Bound.Town 间接变更
}
```

## 6. MCM 操作按钮消费流程

```
MCM 面板 «手动导出» → 设为 true
  │  OnPropertyChanged
  ▼
LegacyWorldSettingsManager.RequestManualExport()
  │  设置 _manualExportRequested = true
  ▼
HourlyTickEvent
  │  LegacyWorldSettingsManager.TryConsumeManualExport()
  │  → 如果为 true, 执行 LegacyService.Export(), 设回 false
  ▼
导出完成
```

> 这种设计确保设置变更在主线程（Campaign 线程）上安全处理，避免跨线程问题。
