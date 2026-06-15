# 代码审查报告 - ProjectileTrajectorySystem

> 审查日期：2026-05-28 | 审查范围：全部 8 个 .cs 源文件

---

## 🔴 Critical（逻辑错误 / 功能缺失）

### C1. `CalculateLeadPosition` 条件判断反了

**文件**: `ProjectileTrajectorySystem.cs` 行 773-787

```csharp
Vec3 shotDir = CalculateProjectileFiringSolution(...);

if (shotDir!=Vec3.Invalid && shotDir!=Vec3.Zero)  // ← 这里：shotDir 有效时
{
    // 注释说 "如果无解...回退到直线计算"
    Vec3 simpleDelta = predictedTargetPos - shooterPos;
    currentTime = simpleDelta.AsVec2.Length / projectileSpeed;  // 用了简化计算
}
else  // ← shotDir 无效时
{
    // 用了不安全的 shotDir 计算抛物线时间
    Vec3 velocityVector = shotDir * projectileSpeed;  // shotDir 可能是 Invalid!
    float airTime = ...;
    currentTime = airTime;
}
```

**问题**: 抛物线有解时用了简化直线估算，无解时反而用了抛物线公式（且 shotDir 无效会导致异常）。

**影响**: 移动目标预瞄功能计算不准确。

---

### C2. `HandleCollision` 从未被调用

**文件**: `ProjectileTrajectorySystem.cs` 行 345

```csharp
private static void HandleCollision(Vec3 pos, float timeKey) { ... }
```

全代码搜索，**没有任何地方调用它**。碰撞检测逻辑完全未接入 `SimulateTrajectory` 主循环。

**影响**: 弹道线不会在碰到地形时截断，穿墙显示。

---

### C3. `UpdateTrajectory(player, null)` 冗余调用

**文件**: `SkillSystemBehavior.cs` 行 562

```csharp
ProjectileTrajectorySystem.UpdateTrajectory(player, null);  // 传入 null
```

而 `UpdateTrajectory` 方法：
```csharp
if (siegeWeapon == null) { return; }  // 立即返回
```

**影响**: 每帧一次无效方法调用。

---

## 🟡 Major（架构 / 可维护性）

### M1. 物理计算和渲染耦合在一个 800+ 行静态类

**文件**: `ProjectileTrajectorySystem.cs`

该文件混合了：
- 物理模拟：`CalculatePosition`, `CalculateLeadPosition`, `CalculateProjectileFiringSolution`
- 渲染：`PlaceGameEntityMarker`, `DrawDebugLineSegment`, `RenderDebugLine`, `GenerateImpactCircle`
- 反射工具：`GetSiegeShootingSpeed`, `GetSiegeShootingDirection`
- 数据管理：`ClearNormalTrajectory`, `ClearEnemyTrajectory`

**影响**: 难以单元测试，无法独立修改渲染方式。

---

### M2. `SimulateTrajectory` 和 `SimulateTrajectoryForMissile` 90% 重复

两个方法有几乎相同的循环结构：
```csharp
// 相同的检查
if (!UseGameEntityDisplay && !UseDebugLineDisplay) return;
direction.Normalize();

for (float t = ...) {
    currentPos = CalculatePosition(...);
    if (UseGameEntityDisplay) PlaceGameEntityMarker(...);
    if (UseDebugLineDisplay) DrawDebugLineSegment(...);
    previousPos = currentPos;
}
```

区别仅在于：`SimulateTrajectoryForMissile` 额外存储轨迹点列表并操作 `_airFriction`。

**影响**: 修改物理逻辑需要改两处。

---

### M3. 设置同步硬编码字段复制

**文件**: `ProjectileTrajectorySettingsData.cs` (14 字段)
**文件**: `ProjectileTrajectorySettingsManager.cs` `SyncFromMCM` (14 行手动赋值)
**文件**: `ProjectileTrajectorySettings.cs` 构造函数 (14 行手动赋值)

```csharp
// SyncFromMCM 中：
Data.EnableTrajectory = mcm.EnableTrajectory;
Data.PlayerTrajectory = mcm.PlayerTrajectory;
Data.EnemyTrajectory = mcm.EnemyTrajectory;
// ... 连续 14 行

// 构造函数中同样 14 行
EnableTrajectory = data.EnableTrajectory;
PlayerTrajectory = data.PlayerTrajectory;
// ...
```

**影响**: 新增一个设置需要在 3 处添加代码，极易遗漏。

---

### M4. Alpha 常量在 2 处重复定义

**文件**: `SkillSystemBehavior.cs` 行 80-82
```csharp
private const float LOOK_UP_THRESHOLD = 0.2f;
private const float BLUR_ALPHA = 0.03f;
```

**文件**: `NavalDLCTrajectorySupport.cs` 行 32-34
```csharp
private const float LOOK_UP_THRESHOLD = 0.2f;
private const float BLUR_ALPHA = 0.03f;
```

**影响**: 调整虚化效果需要改两处。

---

### M5. `MaxTrackedEnemies` 硬编码，未使用设置

**文件**: `SkillSystemBehavior.cs` 行 71
```csharp
private const int MaxTrackedEnemies = 10; // 与设置同步
```

但用户设置中有 `MaxTrackedEnemiesLegacy` (0-50 可调)，代码却使用硬编码常量。

**影响**: 用户调整最大追踪敌人数不会生效。

---

## 🟠 Minor（清洁性 / 技术债务）

| ID | 问题 | 文件 | 行号 |
|----|------|------|------|
| m1 | `partial class SkillSystemBehavior` — 没有其他 partial 文件 | SkillSystemBehavior.cs | 13 |
| m2 | `UpdateLeadPrediction .cs` — 空文件（文件名含空格） | UpdateLeadPrediction .cs | 1-10 |
| m3 | `AgentMissileSpeedData` + `WoW_AgentMissileSpeedData` — 从未被使用 | SkillSystemBehavior.cs | 36-43 |
| m4 | `WoW_CustomGameEntity` — 从未被使用，注释说"可能不再需要" | SkillSystemBehavior.cs | 45 |
| m5 | 注释掉的代码块 — 骨骼位置弹道计算(15行) | ProjectileTrajectorySystem.cs | 150-163 |
| m6 | `debugString1`/`debugString2` — public static 调试变量，每帧赋值+输出 | 多处 | - |
| m7 | `_isFirstPoint`/`_lastPoint` — 从未被读取 | ProjectileTrajectorySystem.cs | 26-27 |
| m8 | `_collisionPoint` — 设置后从未被读取 | ProjectileTrajectorySystem.cs | 25 |
| m9 | `GetWeaponAirFriction` 的 `out EquipmentIndex _` — 从未使用 | ProjectileTrajectorySystem.cs | 581 |
| m10 | `OnAgentShootMissile` 前 7 行（debugString 赋值+输出）必须在所有判断之前执行 | SkillSystemBehavior.cs | 322-331 |

---

## 重构优先级建议

```
第1批（低风险清理，不改逻辑）:  m1~m10  — 预计删除 ~60 行无用代码
第2批（bug 修复，有行为变化）:  C1, C2, C3, M5  — 修复 4 个问题
第3批（结构优化，需谨慎）:      M1, M2, M3, M4  — 拆分类/去重/统一常量
```
