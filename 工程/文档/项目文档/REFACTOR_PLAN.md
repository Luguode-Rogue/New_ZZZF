# 重构计划

> 原则：每步独立可验证，低风险优先，逐步推进。

---

## 第1批：清理死代码（零风险，不改行为）

### 1.1 删除空文件 `UpdateLeadPrediction .cs` [m2]
- 该文件只有空的 namespace 声明，无任何代码
- 删除整个文件

### 1.2 删除未使用的类和数据 [m3, m4]
- 删除 `AgentMissileSpeedData` 类
- 删除 `WoW_AgentMissileSpeedData` 字典
- 删除 `WoW_CustomGameEntity` 列表
- 从 `OnCreated`/`OnEndMission` 中移除相关清理代码

### 1.3 删除未使用的字段 [m7, m8]
- 删除 `_isFirstPoint`, `_lastPoint`, `_collisionPoint`
- 从 `ResetFrameState` 中移除相关重置代码

### 1.4 删除注释掉的代码 [m5]
- 删除 `UpdateTrajectoryRangeWeapon` 中 150-163 行的注释代码

### 1.5 移除 `partial` 关键字 [m1]
- `SkillSystemBehavior` 的 `partial` 改为普通 class

### 1.6 清理调试代码 [m6, m10]
- 将 `debugString1`/`debugString2` 改为 `private`
- 将 `OnAgentShootMissile` 中的 `InformationMessage` 输出用条件包裹
- 将 `UpdateTrajectoryRangeWeapon` 中对 debugString1 的赋值用条件包裹

### 1.7 简化方法签名 [m9]
- `GetWeaponAirFriction` 移除无用的 `out EquipmentIndex` 参数

---

## 第2批：修复 Bug（有行为变化，需验证）

### 2.1 修复 `CalculateLeadPosition` 条件判断 [C1]
- 交换 `if/else` 分支，使有效解走抛物线公式、无效解走简化公式

### 2.2 修复敌人数量上限使用设置值 [M5]
- `MaxTrackedEnemies` 改为从 `ProjectileTrajectorySettingsManager.Settings.MaxTrackedEnemiesLegacy` 读取

### 2.3 删除无效方法调用 [C3]
- 移除 `UpdatePlayerTrajectory` 中 `UpdateTrajectory(player, null)` 的调用

### 2.4 接入碰撞检测 [C2]
- 在 `SimulateTrajectory` 循环中每 N 步调用一次 `HandleCollision`
- 或：如果碰撞检测在实际游戏中表现不佳，则删除该方法

---

## 第3批：结构优化（需谨慎，可能影响其他代码）

### 3.1 统一 Alpha 常量 [M4]
- 在 `ProjectileTrajectorySettingsData` 或单独的 Constants 类中定义
- `SkillSystemBehavior` 和 `NavalDLCTrajectorySupport` 引用同一常量

### 3.2 合并重复的模拟循环 [M2]
- 提取公共循环逻辑到 `SimulateTrajectoryPoints` 方法
- `SimulateTrajectory` 和 `SimulateTrajectoryForMissile` 调用统一方法

### 3.3 分离物理计算和渲染 [M1]
- 新建 `TrajectoryPhysics.cs` — 纯物理计算（`CalculatePosition` 等）
- 新建 `TrajectoryRenderer.cs` — 纯渲染（`PlaceGameEntityMarker`, `RenderDebugLine` 等）
- `ProjectileTrajectorySystem.cs` 保留为门面类

### 3.4 简化设置同步 [M3]
- 使用 T4 模板或反射自动同步 MCM ↔ XML

---

## 执行顺序

```
本次执行: 第1批全部 + 第2批 2.1~2.3（2.4 待确认）
后续:     第3批（需更多时间验证）
```
