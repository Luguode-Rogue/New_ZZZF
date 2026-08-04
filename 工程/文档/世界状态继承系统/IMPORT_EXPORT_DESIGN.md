# 世界继承机制设计

## 1. 导出机制 (Export)

### 1.1 触发条件

| 触发方式 | 条件 | 代码路径 |
|----------|------|----------|
| 自动导出 | 存档时（`OnBeforeSaveEvent`），且 `Enabled && AutoExportOnSave` 均为 true | `LegacyBehavior.OnBeforeSave()` → `LegacyService.Export()` |
| 手动导出 | MCM 按钮«手动导出»拨动触发，无条件 | `HourlyTick` 消费 `ManualExportTrigger` → `LegacyService.Export()` |

### 1.2 导出流程

导出分两部分写入两个文件（均位于模块根目录）：

```
LegacyService.Export()
  ├── 检查 Enable 主开关（自动导出时）
  ├── LegacyExporter.ExportWorld(adapter)        → 写 Legacy.json（覆盖）
  │   ├── 遍历所有 Kingdom → KingdomState
  │   ├── 遍历所有 Clan → ClanState
  │   ├── 遍历所有 Settlement → SettlementState
  │   └── 组装 LegacyData（含版本号、WorldId、时间戳）
  └── LegacyExporter.ExportHeroes(adapter)       → 写 LegacyHeroes.json（累积）
      ├── 读取已有 LegacyHeroes.json（保留 applied_world_ids）
      ├── 遍历 GetHeroProfiles() → HeroProfile
      ├── 按 (WorldId + Name + Source) 去重追加
      └── 写回 LegacyHeroes.json
```

### 1.3 导出内容

**Legacy.json（世界状态，覆盖写）：**
- 所有王国（含统治者）
- 所有非隐藏家族（含所属王国、等级、金币、声望、影响力）
- 所有定居点（含拥有者、繁荣度）

**LegacyHeroes.json（玩家人物，累积写）：**
- 英雄档案（仅玩家本体 + 招募过且存活的 companion + 现有游荡英雄可选）
  - 来源标记：`player`（`Hero.MainHero`）/ `companion`（`Clan.PlayerClan.Companions` 且 `IsAlive`）
  - 字段：姓名、所属 `world_id`、文化（取自 `CharacterObject.Culture`）、等级、技能、特性、职业、性别、`StaticBodyProperties`/`Weight`/`Build`
  - 每个模板带 `world_id`，用于跨世界区分与防二重身
  - 日志：`[HERO] 导出英雄: xxx (player/companion, LvN)`

### 1.4 英雄导出流程

```
BannerlordGameAdapter.GetHeroProfiles()
  ├── Hero.MainHero → HeroProfile(source=player, world_id=当前)
  ├── Clan.PlayerClan.Companions
  │   └── 过滤 IsAlive == true
  │       └── HeroProfile(source=companion, world_id=当前)
  └── 逐条写入 [HERO] 日志

LegacyExporter.ExportHeroes(adapter)
  └── 累积写 LegacyHeroes.json（按 WorldId+Name+Source 去重）
```

## 2. 导入机制 (Import)

### 2.1 触发条件

| 触发方式 | 条件 | 代码路径 |
|----------|------|----------|
| 自动导入 | **仅新游戏**（`OnNewGameCreatedEvent`），且 `Enabled` 为 true | `LegacyBehavior.OnNewGameCreated()` → `LegacyService.Import()` |
| 手动应用 | MCM 按钮«手动应用»拨动触发，跳过同世界检测 | `HourlyTick` 消费 `ManualApplyTrigger` → `LegacyService.ForceImport()` |

> **重要**：载入已有存档（`OnGameLoadedEvent`）**不会**触发导入。
> 已复刻过的遗产世界记录在 `LegacyHeroes.json` 的 `applied_world_ids`（持久化），重开游戏也不重复复刻。

### 2.2 导入流程

```
LegacyService.LoadCombined()
  ├── 读取 Legacy.json → LegacyData（世界状态）
  ├── 读取 LegacyHeroes.json → HeroProfileList（玩家人物，跨世界累积）
  └── 将 heroes.Profiles 合并进 legacyData.HeroProfiles

LegacyService.Import()
  ├── currentWorldId = adapter.GetWorldId()
  ├── 计算 foreignWorlds = heroes 中 WorldId != currentWorldId 的来源世界集合
  ├── 若 foreignWorlds 为空（遗产只含当前世界）：跳过英雄导入
  ├── 若 foreignWorlds ⊆ applied_world_ids（已复刻过）：跳过，避免重复
  ├── LegacyService.RefreshSettings()  ← 从 MCM 读取分类开关
  ├── LegacyImporter.Apply(adapter, legacyData, settings)
  │   ├── Phase 1: KingdomImporter.Restore()
  │   ├── Phase 2: ClanImporter.Restore()
  │   ├── Phase 3: SettlementImporter.Restore()
  │   └── Phase 4: HeroResurrectionFactory.Resurrect(profile, currentWorldId)
  │       ├── 防本存档二重身：当前 WorldId == 遗产 WorldId 且命中存活的自己/队友 → 跳过
  │       ├── 游荡英雄查重：同名同文化游荡英雄已存在 → 跳过
  │       ├── HeroCreator.CreateSpecialHero(template, settlement, clan, clan, age)
  │       ├── SetName / SetNewOccupation / SetSkillValue / SetTraitLevel
  │       └── 登记到 ResurrectedHeroTracker
  └── 成功后 SaveAppliedWorlds() → 将 foreignWorlds 写入 LegacyHeroes.json.applied_world_ids
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

## 3. 本存档保护与防二重身

设计上**禁止在本存档内出现"自己/队友"的二重身**，但**允许跨新档遇到"原来的自己"**：

```csharp
// HeroResurrectionFactory.Resurrect(profile, currentWorldId)
// 仅当「当前 WorldId == 遗产 WorldId」（同一存档）时，才判断原 Hero 是否仍存活
if (currentWorldId == profile.WorldId && IsStillAliveInCurrentSave(profile))
{
    // player/companion 命中当前存活的自己/队友 → 跳过，避免本存档二重身
    return;
}
// 跨存档（WorldId 不同，如 A 导出、B 导入）：即使姓名雷同也放行复刻
```

- `player` 模板比对 `Hero.MainHero` 是否同名且存活；
- `companion` 模板在 `Clan.PlayerClan.Companions` 中比对；
- 跨世界（WorldId 不同）的同名主角/队友视为不同生命，正常复刻为游荡英雄（"遇到原来的自己"）。

## 4. 防重复复刻（持久化）

已复刻过的遗产来源世界持久化在 `LegacyHeroes.json` 的 `applied_world_ids`，跨进程生效：

```csharp
// LegacyService.Import()
var applied = new HashSet<string>(heroes?.AppliedWorldIds ?? new List<string>());
var foreignWorlds = heroes.Profiles.Where(p => p.WorldId != currentWorldId).Select(p => p.WorldId).ToHashSet();
if (foreignWorlds.Any(w => applied.Contains(w)))
{
    AffixLogger.Warn("SERVICE", "这些遗产世界已导入过，跳过以避免重复复刻");
    return;
}
// ... 导入成功后 ...
SaveAppliedWorlds(heroes, foreignWorlds); // 写回 applied_world_ids
```

- 同进程反复导入：立即命中 `applied_world_ids` 跳过；
- 重开游戏后再导入：从文件读到已导入记录，仍跳过，避免酒馆堆积重复 NPC；
- 新世界（不在记录中）正常复刻并写入记录。

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
