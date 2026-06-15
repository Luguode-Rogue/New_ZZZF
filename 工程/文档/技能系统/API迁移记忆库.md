# 骑砍2 API 迁移记忆库

> 用于记录所有旧版→新版 API 变更，方便后续版本迭代时快速检索和复用。
> 最后更新：2025-06-05

---

## 格式说明

每条记录使用统一表格，字段说明：

| 字段 | 说明 |
|------|------|
| ID | 唯一编号，格式：`CAT-NNN`（CAT=分类缩写，NNN=序号） |
| 旧版 API | 旧版代码中的调用方式 |
| 新版 API | 新版（当前）代码中的调用方式 |
| 变更类型 | 重命名 / 参数变更 / 类型变更 / 修饰符变更 / 接口移除 |
| 来源文件 | 原始定义所在的参考源码路径 |
| 受影响文件 | Mod 中需修改的文件 |
| 修复日期 | 批量修复的日期 |

---

## 一、方法/属性重命名（REN - Rename）

| ID | 旧版 API | 新版 API | 来源 | 受影响文件 | 日期 |
|----|----------|----------|------|-----------|------|
| REN-001 | `SkillType` 枚举 | `SPSkillType` 枚举 | 自定义类型，避免与 `TaleWorlds.Core.SkillType` 冲突 | 48个 .cs 文件 | 2025-06-05 |
| REN-002 | `GetWieldedItemIndex(HandIndex)` | `GetPrimaryWieldedItemIndex()` / `GetOffhandWieldedItemIndex()` | `Agent.cs` | Script.cs, SkillSystemBehavior.cs, ProjectileTrajectorySystem.cs, ZZZF_SandboxAgentStatCalculateModel.cs, ShadowStep.cs, Roll.cs, LingHunDanMu.cs, HuoYanTuXi.cs, missionSetting.cs | 2025-06-05 |
| REN-003 | `Mission.Missiles` | `Mission.MissilesList` | `Mission.cs` | Script.cs | 2025-06-05 |
| REN-004 | `TextObject.Empty` 静态属性 | `TextObject.GetEmpty()` 静态方法 | `TextObject.cs` | SPSkillVM.cs (New_ZZZF), SPSkillVM.cs (PST) | 2025-06-05 |
| REN-005 | `UIResourceManager.UIResourceDepot` | `UIResourceManager.ResourceDepot` | `UIResourceManager.cs` | GauntletSkillScreen.cs (PST) | 2025-06-05 |
| REN-006 | `InventoryCapacity` 属性 | `CalculateInventoryCapacity()` 方法 | `InventoryCapacityModel.cs` | SPSkillVM.cs (PST, 2处) | 2025-06-05 |
| REN-007 | `InitializeLogic(...)` | `SetInventoryLogic(...)` | `SkillInventoryState.cs` | SkillInventoryState.cs, SkillInventoryManager.cs | 2025-06-05 |
| REN-008 | `InventoryLogic` setter (`= value`) | `SetInventoryLogic(value)` 方法 | `SkillInventoryState.cs`（setter 变为 private） | SkillInventoryManager.cs | 2025-06-05 |
| REN-009 | `ActionIndexCache.Name` 属性 | `ActionIndexCache.GetName()` 方法 | `ActionIndexCache.cs` | SkillSystemBehavior.cs | 2025-06-05 |
| REN-010 | `InventoryScreenHelper` 可继承类 | `InventoryScreenHelper` 静态类 | `InventoryScreenHelper.cs` | SkillInventoryManager.cs, SkillInventoryScreenHelper.cs | 2025-06-05 |
| REN-011 | `InventoryManager` 类 | `InventoryScreenHelper` 静态类 | `InventoryScreenHelper.cs` | SkillInventoryManager.cs | 2025-06-05 |
| REN-012 | `ItemVM.TypeId` setter | `ItemVM.OnItemTypeUpdated()` 方法 | `ItemVM.cs` | SPSkillItemVM.cs | 2025-06-05 |
| REN-013 | `InventorySide.Equipment` 单值 | `BattleEquipment` / `CivilianEquipment` / `StealthEquipment` | `InventoryLogic.InventorySide` | SPSkillVM.cs | 2025-06-05 |
| REN-014 | `ImageIdentifierVM` | `ItemImageIdentifierVM` | `SPItemVM.cs` | SPSkillItemVM.cs | 2025-06-05 |

---

## 二、参数签名变更（SIG - Signature）

### SIG-001: CalculateInventoryCapacity（物品栏容量）

| 参数位置 | 旧版类型 | 新版类型 | 说明 |
|----------|---------|---------|------|
| 1 | `MobileParty mobileParty` | `MobileParty mobileParty` | 无变化 |
| 2 | `bool isCurrentlyAtSea` | `bool isCurrentlyAtSea` | 无变化 |
| 3 | — | `bool includeDescriptions = false` | **新增**：是否包含描述性计算说明 |
| 4 | `int additionalManOnFoot = 0` | `int additionalManOnFoot = 0` | 位置后移一位 |
| 5 | `int additionalSpareMounts = 0` | `int additionalSpareMounts = 0` | 位置后移一位 |
| 6 | `int additionalPackAnimals = 0` | `int additionalPackAnimals = 0` | 位置后移一位 |
| 7 | — | `bool includeFollowers = false` | **新增**：是否包含随从 |

**完整新版签名**：
```csharp
public abstract ExplainedNumber CalculateInventoryCapacity(
    MobileParty mobileParty,
    bool isCurrentlyAtSea,
    bool includeDescriptions = false,      // 新增
    int additionalManOnFoot = 0,
    int additionalSpareMounts = 0,
    int additionalPackAnimals = 0,
    bool includeFollowers = false)         // 新增
```

**修复原则**：在原 `isCurrentlyAtSea` 后插入 `false`（不生成描述），末尾追加 `false`（不含随从）。

| 受影响文件 | 行号 | 日期 |
|-----------|------|------|
| SPSkillVM.cs (New_ZZZF) | 981, 1980 | 2025-06-05 |
| WoW_DefaultPartySpeedCalculatingModel.cs (New_ZZZF) | — | 2025-06-05 |
| WoW_DefaultPartySpeedCalculatingModel.cs (WoW) | — | 2025-06-05 |
| SPSkillVM.cs (PST) | — | 2025-06-05 |

---

### SIG-002: SetItem（物品菜单设置）

| 参数位置 | 旧版类型 | 新版类型 | 说明 |
|----------|---------|---------|------|
| 1 | `SPItemVM item` | `SPItemVM item` | 无变化 |
| 2 | `ItemVM comparedItem = null` | `InventoryLogic.InventorySide currentEquipmentMode` | **新增中间参数** |
| 3 | `BasicCharacterObject character = null` | `ItemVM comparedItem = null` | 位置后移 |
| 4 | `int alternativeUsageIndex = 0` | `BasicCharacterObject character = null` | 位置后移 |
| 5 | — | `int alternativeUsageIndex = 0` | 位置后移 |

**完整新版签名**：
```csharp
public void SetItem(
    SPItemVM item,
    InventoryLogic.InventorySide currentEquipmentMode,  // 新增
    ItemVM comparedItem = null,
    BasicCharacterObject character = null,
    int alternativeUsageIndex = 0)
```

**修复原则**：在原第1参数后插入 `this._currentEquipmentMode`。

| 受影响文件 | 行号 | 日期 |
|-----------|------|------|
| SPSkillVM.cs (New_ZZZF) | 642, 694, 772 | 2025-06-05 |

---

### SIG-003: SetActionChannel（动作通道设置）

| 参数位置 | 旧版类型 | 新版类型 | 说明 |
|----------|---------|---------|------|
| 1 | `int channelNo` | `int channelNo` | 无变化 |
| 2 | `ActionIndexCache actionIndexCache` | `in ActionIndexCache actionIndexCache` | 增加 `in` 修饰符 |
| 3 | `bool ignorePriority = false` | `bool ignorePriority = false` | 无变化 |
| 4 | `ulong additionalFlags` | `AnimFlags additionalFlags = (AnimFlags)0UL` | **类型从 ulong 变为 AnimFlags 枚举** |

**完整新版签名**：
```csharp
public bool SetActionChannel(
    int channelNo,
    in ActionIndexCache actionIndexCache,
    bool ignorePriority = false,
    AnimFlags additionalFlags = (AnimFlags)0UL,
    float blendWithNextActionFactor = 0f,
    float actionSpeed = 1f,
    float blendInPeriod = -0.2f,
    float blendOutPeriodToNoAnim = 0.4f,
    float startProgress = 0f,
    bool useLinearSmoothing = false,
    float blendOutPeriod = -0.2f,
    int actionShift = 0,
    bool forceFaceMorphRestart = true)
```

**修复原则**：所有 `ulong` 字面量强制转换为 `(AnimFlags)xxxUL`。

| 受影响文件 | 原值 | 日期 |
|-----------|------|------|
| Harmony.cs | `272UL` → `(AnimFlags)272UL` | 2025-06-05 |
| HouYueSheJi.cs | `999UL` → `(AnimFlags)999UL` (4处) | 2025-06-05 |
| TianFaZhiJian.cs | `512UL` → `(AnimFlags)512UL` (2处) | 2025-06-05 |
| AgentSkillComponent.cs | `172UL` → `(AnimFlags)172UL` | 2025-06-05 |

---

### SIG-004: OpenTroopSelection（部队选择界面）

| 参数位置 | 旧版类型 | 新版类型 | 说明 |
|----------|---------|---------|------|
| 1 | `TroopRoster fullRoster` | `TroopRoster fullRoster` | 无变化 |
| 2 | `TroopRoster initialSelections` | `TroopRoster initialSelections` | 无变化 |
| 3 | `Func<CharacterObject, bool> canChange...` | `List<Ship> eligibleShips` | **新增 Naval DLC 参数** |
| 4 | `Action<TroopRoster> onDone` | `Func<CharacterObject, bool> canChange...` | 位置后移 |
| 5 | `int maxSelectableTroopCount` | `Action<TroopRoster> onDone` | 位置后移 |
| 6 | `int minSelectableTroopCount` | `int maxSelectableTroopCount` | 位置后移 |
| 7 | — | `int minSelectableTroopCount = 1` | 位置后移 |
| 8 | — | `bool isNavalRaid = false` | **新增** |

**修复原则**：在第2参数后插入 `null`。

| 受影响文件 | 行号 | 日期 |
|-----------|------|------|
| HeroChangeCampaignBehavior.cs | 73-75 | 2025-06-05 |

---

### SIG-005: RayCastForClosestAgent（射线检测）

| 参数位置 | 旧版 | 新版 | 说明 |
|----------|------|------|------|
| 原有参数 + 新增 | — | `int excludedAgentIndex` | **新增**：排除的 agent 索引 |
| 原有参数 + 新增 | — | `float rayThickness` | **新增**：射线厚度 |

| 受影响文件 | 日期 |
|-----------|------|
| SkillSystemBehavior.cs | 2025-06-05 |

---

### SIG-006: CalculateDamage（伤害计算）

| 参数位置 | 旧版 | 新版 | 说明 |
|----------|------|------|------|
| 4参数签名 | `virtual` 虚方法 | 非虚方法 | 子类需使用 `new` 关键字隐藏 |

| 受影响文件 | 日期 |
|-----------|------|
| NewDamageModel.cs (2处), Scripts.cs (WoW, 2处) | 2025-06-05 |

---

### SIG-007: SPSkillItemVM 构造函数

| 旧版参数数 | 新版参数数 | 说明 |
|-----------|-----------|------|
| 11 参数 | 8 参数 | 构造函数精简，移除部分可选参数 |

| 受影响文件 | 修改处数 | 日期 |
|-----------|---------|------|
| SPSkillVM.cs (New_ZZZF) | 6处 | 2025-06-05 |

---

## 三、类型变更（TYP - Type Change）

| ID | 旧版类型 | 新版类型 | 说明 | 受影响文件 | 日期 |
|----|---------|---------|------|-----------|------|
| TYP-001 | `GameEntity` | `WeakGameEntity` | 强引用→弱引用，但 `Parent`/`HasScriptOfType<T>`/`GetFirstScriptOfType<T>`/`GetChildren()` 等成员相同 | SkillSystemBehavior.cs, ProjectileTrajectorySystem.cs | 2025-06-05 |
| TYP-002 | `ImageIdentifierType` 枚举 | 不再需要，空图用 `null` | ImageIdentifierVM → ItemImageIdentifierVM | SPSkillItemVM.cs | 2025-06-05 |
| TYP-003 | `InventoryMode`（独立枚举） | `InventoryScreenHelper.InventoryMode`（嵌套类型） | 移入静态类内部 | SPSkillVM.cs | 2025-06-05 |
| TYP-004 | `InventoryItemType`（独立枚举） | `InventoryScreenHelper.InventoryItemType`（嵌套类型） | 移入静态类内部 | SPSkillVM.cs | 2025-06-05 |

---

## 四、修饰符/继承变更（MOD - Modifier Change）

| ID | 旧版 | 新版 | 说明 | 受影响文件 | 日期 |
|----|------|------|------|-----------|------|
| MOD-001 | `InventoryScreenHelper` 可实例化 | `static class InventoryScreenHelper` | 不可被继承 | SkillInventoryManager.cs, SkillInventoryScreenHelper.cs | 2025-06-05 |
| MOD-002 | `InventoryLogic` 属性 `{ get; set; }` | `InventoryLogic` 属性 `{ get; private set; }` + `SetInventoryLogic()` 方法 | setter 私有化 | SkillInventoryManager.cs | 2025-06-05 |
| MOD-003 | `CalculateDamage` 为 `virtual` | `CalculateDamage` 为非虚方法 | 子类需 `new` 关键字 | NewDamageModel.cs, Scripts.cs (WoW) | 2025-06-05 |

---

## 五、接口/方法移除（REM - Removed）

| ID | 已移除的 API | 处理方式 | 受影响文件 | 日期 |
|----|------------|---------|-----------|------|
| REM-001 | `FilterInventoryAtOpening` 接口方法 | 添加注释保留方法体，不再被框架调用 | GauntletSkillScreen.cs | 2025-06-05 |

---

## 六、快速检索索引

### 按模块检索

| 模块 | 涉及 API |
|------|---------|
| **物品/库存界面** | REN-004~014, SIG-001~002/007, TYP-002~004, MOD-001~002 |
| **战斗/Agent** | REN-002/003/009, SIG-003/005/006, TYP-001 |
| **UI/界面** | REN-005, MOD-001, REM-001 |
| **部队/选择** | SIG-004 |
| **技能系统** | REN-001, SIG-003 |

### 按修复策略检索

| 策略 | 涉及的 API |
|------|-----------|
| **插入新参数** | SIG-001 (CalcInvCapacity), SIG-002 (SetItem), SIG-004 (OpenTroop), SIG-005 (RayCast) |
| **类型转换** | SIG-003 (AnimFlags), TYP-001 (WeakGameEntity), TYP-003/004 (嵌套类型) |
| **属性→方法** | REN-004, REN-005, REN-008, REN-009, REN-012 |
| **继承改为组合** | MOD-001 (静态类), MOD-002 (private setter) |
| **virtual→new** | SIG-006, MOD-003 |

---

## 七、重构案例与工具方法

### 7.1 静态类继承→组合模式

**场景**：`InventoryScreenHelper` 从可继承类变为 `static class`。

**旧代码**：
```csharp
public class SkillInventoryScreenHelper : InventoryScreenHelper
{
    // 重写父类方法
}
```

**新代码**（组合模式）：
```csharp
public static class SkillInventoryUtils
{
    // 直接调用 InventoryScreenHelper.xxx() 静态方法
    // 不再继承，改为工具方法封装
}
```

### 7.2 SetActionChannel 的 AnimFlags 常量表

方便查表而不必每次搜源码：

| 原 ulong 值 | 含义推测 | 使用位置 |
|------------|---------|---------|
| `272UL` | 上马动作标记 | Harmony.cs |
| `999UL` | 技能强制切换动作 | HouYueSheJi.cs |
| `512UL` | 技能蓄力动作 | TianFaZhiJian.cs |
| `172UL` | 法术命令动作 | AgentSkillComponent.cs |

### 7.3 快速检测 API 变更的命令模板

在 `骑砍2源码` 目录中快速定位方法签名：
```
search_content pattern="方法名(" glob="*.cs" path="骑砍2源码" contextAround=5
```

优先搜索顺序：
1. `ComponentInterfaces/` → 抽象接口定义
2. `GameComponents/` → 默认实现
3. `ViewModelCollection/` → UI ViewModel
4. `SandBox.xxx/` → 沙盒层实现

### 7.4 GameEntity → WeakGameEntity 过渡模式

新旧 API 中 `WeakGameEntity` 保留了与旧 `GameEntity` 完全相同的操作成员：
- `Parent` → `WeakGameEntity`
- `HasScriptOfType<T>()` → `bool`
- `GetFirstScriptOfType<T>()` → `T`
- `GetChildren()` → `IEnumerable<WeakGameEntity>`
- `GlobalPosition` → `MatrixFrame`

**迁移模板**：
```csharp
// 旧代码
GameEntity entity = someMethod();

// 新代码（仅改类型名）
WeakGameEntity entity = someMethod();
```

**注意**：`WeakGameEntity` 在新版中已启用可空引用类型(NRT)，赋值时需注意：
```csharp
// 旧写法
GameEntity entity = someObj?.GameEntity;

// 新写法（需用 ? 声明可变空）
WeakGameEntity? entity = someObj?.GameEntity;
```

---

## 八、新增记录 (2025-06-05 第三轮)

### REN-015 | MobileParty.Position2D → MobileParty.GetPosition2D
| 文件 | 行 |
|------|-----|
| WoW_DefaultPartySpeedCalculatingModel.cs | 85, 247 |

```csharp
// 旧
mobileParty.Position2D
// 新
mobileParty.GetPosition2D
```
返回类型 `Vec2` 不变。

### REN-016 | ItemRoster.TotalWeight → MobileParty.TotalWeightCarried
| 文件 | 行 |
|------|-----|
| WoW_DefaultPartySpeedCalculatingModel.cs | 49, 62, 189 |

```csharp
// 旧
mobileParty.ItemRoster.TotalWeight
// 新
mobileParty.TotalWeightCarried
```
`ItemRoster` 移除了 `TotalWeight` 属性，总重量现在直接从 `MobileParty.TotalWeightCarried` (float) 获取。

### SIG-008 | SPSkillVM — _currentEquipmentMode 字段不存在
| 文件 | 行 |
|------|-----|
| SPSkillVM.cs | 642, 694, 772 |

`SPSkillVM` 不继承 `SPInventoryVM`，没有 `EquipmentMode` 转换机制。SetItem 第二参数需直接传 `InventoryLogic.InventorySide.None`。

### TYP-004 | WeakGameEntity: class → struct（值类型），需适配 NRT 可空处理
| 文件 | 行 |
|------|-----|
| SkillSystemBehavior.cs | 612-623 |

`WeakGameEntity` 在新版中从引用类型变为**值类型(struct)**，因此：
- `WeakGameEntity?` = `Nullable<WeakGameEntity>`，不能直接 `.` 调用成员
- `== null` 对 struct 始终为 false，需用 `WeakGameEntity.Invalid.Equals()`
- `.Parent` 可能返回 null，需 `?? WeakGameEntity.Invalid` 兜底

```csharp
// 旧写法（class 时代可用）
WeakGameEntity? ge = GetSteppedEntity();
while (ge != null && !ge.HasScriptOfType<T>())
    ge = ge.Parent;

// 新写法（struct 时代，参考 PTS AlphaBlurSystem.GetHighestParent 模式）
WeakGameEntity ge = currentlyUsedGameObject?.GameEntity
    ?? GetSteppedEntity()
    ?? WeakGameEntity.Invalid;
while (!WeakGameEntity.Invalid.Equals(ge) && !ge.HasScriptOfType<T>())
    ge = ge.Parent ?? WeakGameEntity.Invalid;
```

---

## 九、新增记录 (2025-06-06)

### SIG-009 | Harmonic Patch 目标方法迁移 — MissionCombatMechanicsHelper

两个 Harmony patch 的目标方法从 `Mission` 类移至 `MissionCombatMechanicsHelper` 静态类。

#### 1. UpdateMomentumRemaining

| 项目 | 旧版 | 新版 |
|------|------|------|
| 所在类 | `Mission` | `MissionCombatMechanicsHelper` |
| 方法签名 | 实例方法 | `public static` 静态方法 |
| `Blow` 参数 | `Blow b` | `in Blow b` |

| 受影响文件 | 日期 |
|-----------|------|
| Harmony.cs | 2025-06-06 |

#### 2. DecideWeaponCollisionReaction

| 项目 | 旧版 patch | 新版目标 |
|------|-----------|----------|
| 所在类 | `Mission` | `MissionCombatMechanicsHelper` |
| `Blow` 参数 | `Blow registeredBlow` | `in Blow registeredBlow` |
| momentumRemaining | 无 | `float momentumRemaining` (新增参数) |
| colReaction | `ref MeleeCollisionReaction` | `out MeleeCollisionReaction` |

| 受影响文件 | 日期 |
|-----------|------|
| Harmony.cs | 2025-06-06 |

---

## 十、新增记录 (2025-06-06 第二轮)

### TYP-005 | LoadMovie 返回类型 — IGauntletMovie → GauntletMovieIdentifier

| 项目 | 旧版 | 新版 |
|------|------|------|
| 返回类型 | `IGauntletMovie` | `GauntletMovieIdentifier` |
| 命名空间 | `TaleWorlds.GauntletUI.Data` | `TaleWorlds.Engine.GauntletUI` |
| 是否需要强制转换 | 需要 `(IGauntletMovie)` 转换 | 直接赋值，无需转换 |

**修复原则**：
1. 字段类型：`private IGauntletMovie _gauntletMovie` → `private GauntletMovieIdentifier _gauntletMovie`
2. LoadMovie 调用：去掉 `(IGauntletMovie)` 强制转换，直接赋值
3. 可移除 `using TaleWorlds.GauntletUI.Data;`（如果不再使用 IGauntletMovie）
4. OnFinalize 中 `= null` 即可，无需额外 ReleaseMovie

| 受影响文件 | 日期 |
|-----------|------|
| GauntletSkillScreen.cs (New_ZZZF) | 2025-06-06 |
| GauntletSkillScreen.cs (PST) | 2025-06-06 |

---

### REN-017 | Clan.Lords → Clan.Heroes 或 Clan.AliveLords

**旧版**: `clan.Lords` — 获取氏族领主列表
**新版**: `Clan` 类不再有 `Lords` 属性。替代：
- `clan.Heroes` — 氏族所有英雄（含同伴）
- `clan.AliveLords` — 仅有存活领主
- `clan.DeadLords` — 仅有死亡领主
- `clan.Companions` — 仅有同伴

| 受影响文件 | 行号 | 日期 |
|-----------|------|------|
| CustomSkillScreenVM.cs (New_ZZZF) | 751 | 2026-06-13 |

### REN-019 | CultureObject.BasicTroops/EliteBasicTroops → BasicTroop/EliteBasicTroop（单数单对象）

**旧版**: `culture.BasicTroops` (可枚举集合)、`culture.EliteBasicTroops` (可枚举集合)
**新版**: `culture.BasicTroop` (单个 `CharacterObject`)、`culture.EliteBasicTroop` (单个 `CharacterObject`)

| 受影响文件 | 行号 | 日期 |
|-----------|------|------|
| CustomSkillScreenVM.cs (New_ZZZF) | 728, 733 | 2026-06-13 |

### REN-018 | 缺少 using TaleWorlds.ObjectSystem 导致 MBObjectManager 无法识别

`MBObjectManager` 类位于 `TaleWorlds.ObjectSystem` 命名空间，使用前需 `using TaleWorlds.ObjectSystem;`。

| 受影响文件 | 日期 |
|-----------|------|
| CustomSkillScreenVM.cs (New_ZZZF) | 2026-06-13 |

---

*维护说明：每次完成 API 修复后，按上述格式追加新记录，确保记忆库持续更新。*

---

## 十一、新增记录 (2026-06-12)

### RUL-001 | 禁止使用 Debug.Print

**规则**: 严格禁止在 Mod 代码中使用 `Debug.Print` 输出日志。

**替代方案**:
1. `InformationManager.DisplayMessage(new InformationMessage("消息"))` — 游戏内消息显示
2. `File.AppendAllText("日志路径.txt", "消息")` — 写入日志文件

| 受影响文件 | 替换处数 | 日期 |
|-----------|---------|------|
| AffixCampaignBehavior.cs | 4处 → DisplayMessage | 2026-06-12 |
| AffixDatabase.cs | 1处 → DisplayMessage | 2026-06-12 |
| AffixDebugHelper.cs | 5处 → 删除（已有ShowDebugMsg） | 2026-06-12 |
| SubModule.cs | 6处 → DisplayMessage | 2026-06-12 |

### SIG-010 | SyncData 存档序列化修复（自定义类型 ≠ 原生类型）

**问题**: `AffixCampaignBehavior.SyncData` 为空实现，导致词缀数据存档读档丢失。

**第一次尝试（失败）**: 直接 `dataStore.SyncData<Dictionary<string, AffixInstance>>`
→ **游戏保存崩溃**。原因：`AffixInstance` 是自定义 class，需要 `SaveableTypeDefiner` + 自动生成序列化代码，Mod 无法提供。

**第二次尝试（成功）**: 用 `List<string>` 文本序列化作为中间格式。

**正确模式**（自定义类型存档）:
```csharp
// 存档字段：只用 Bannerlord 原生支持的类型（string, bool, int, List<string> 等）
[SaveableField(1)] private List<string> _serializedAffixData = new List<string>();
[SaveableField(2)] private bool _serializedIsInitialized;

// 运行时字典：不直接存档
public Dictionary<string, AffixInstance> ItemAffixMap = new Dictionary<string, AffixInstance>();

public override void SyncData(IDataStore dataStore)
{
    if (dataStore.IsSaving)
    {
        _serializedAffixData = SerializeAffixMap();  // 字典 → 文本列表
        _serializedIsInitialized = IsInitialized;
    }
    dataStore.SyncData<List<string>>("_serializedAffixData", ref _serializedAffixData);
    dataStore.SyncData<bool>("_serializedIsInitialized", ref _serializedIsInitialized);
    if (dataStore.IsLoading)
    {
        IsInitialized = _serializedIsInitialized;
        ItemAffixMap = DeserializeAffixMap(_serializedAffixData);  // 文本列表 → 字典
    }
}
```

**教训**: `[SaveableProperty]`/`[SaveableField]` 只是标记，Bannerlord 的 SaveSystem 对**自定义 class** 需要编译时代码生成。Mod 中存自定义类型 → 必须序列化为原生类型（string/int/List&lt;string&gt;等）再存档。

| 受影响文件 | 日期 |
|-----------|------|
| AffixCampaignBehavior.cs | 2026-06-12 |
