# 架构设计文档 - ProjectileTrajectorySystem

> **概要**：本文档描述ProjectileTrajectorySystem Mod重构后的四层架构（Settings/Core/Systems/DLC），涵盖系统概览、设计原则、各层详解（文件清单+职责+关键实现）、公共API签名、子系统交互时序图。适合架构师和开发者理解整体设计。
> 
> 重构后的四层架构，v1.4.0.0

---

## 一、系统概览

本 Mod 采用 **四层 + 委托** 架构，从底向上分为：

```
┌─────────────────────────────────────────────┐
│              设置层 (Settings)               │
│  ProjectileTrajectorySettings (MCM UI)      │
│  ProjectileTrajectorySettingsData (XML模型)  │
│  ProjectileTrajectorySettingsManager (管理)  │
└────────────────────┬────────────────────────┘
                     │ 读取配置
┌────────────────────▼────────────────────────┐
│            Core 层 (纯计算/渲染)             │
│  TrajectoryPhysics    (弹道物理)            │
│  TrajectoryRenderer   (双管线渲染)          │
│  SiegeWeaponHelper    (攻城反射接口)        │
│  LeadPredictionMath   (预瞄算法)            │
└────────────────────┬────────────────────────┘
                     │ 调用
┌────────────────────▼────────────────────────┐
│            Systems 层 (有状态子系统)          │
│  PlayerTrajectorySystem (玩家弹道)          │
│  EnemyTrajectorySystem  (敌人弹道)          │
│  MissileTrajectorySystem(投射物弹道)        │
│  EnemyOutlineSystem     (敌人描边)          │
│  AlphaBlurSystem        (抬头虚化)          │
│  LeadPredictionSystem   (预瞄)              │
│  SlowMotionSystem       (慢动作)            │
│  GameEntityPool         (对象池)            │
└────────────────────┬────────────────────────┘
                     │ 编排
┌────────────────────▼────────────────────────┐
│            DLC 层 (扩展适配)                 │
│  NavalDLCTrajectorySupport (海战DLC)        │
│  NavalDLCmBehavior (海战MissionLogic)       │
└─────────────────────────────────────────────┘
```

### 设计原则

- **Core 层无状态**：所有方法为纯函数或静态工具，不持有实例字段（除 `GameEntityPool` 的池状态）
- **Systems 层有状态**：每个子系统管理自己的内部状态（追踪列表、缓存字典等）
- **薄编排层**：`SkillSystemBehavior` 仅 ~240 行，`OnMissionTick` 委托给各子系统
- **薄外观层**：`ProjectileTrajectorySystem` 仅 ~180 行，public API 委托给 Core/Systems

---

## 二、类关系图

```
                    MBSubModuleBase
                          │
                    ┌─────┴─────┐
                    │  SubModule  │
                    └─────┬──────┘
                          │ OnMissionBehaviorInitialize()
              ┌───────────┼───────────┐
              │           │           │
              ▼           ▼           ▼
    ┌─────────────┐ ┌──────────┐ ┌────────────────────┐
    │SkillSystem- │ │NavalDLC- │ │ProjectileTrajectory│
    │Behavior     │ │mBehavior │ │SettingsManager     │
    │(编排层)     │ │(DLC入口) │ │(static 配置管理)   │
    └──────┬──────┘ └────┬─────┘ └────────────────────┘
           │             │
     ┌─────┤             │
     │     │             │
     ▼     ▼             ▼
  Systems层           ProjectileTrajectorySystem
  ┌────────────┐      (外观层 → 委托到 Core/Systems)
  │Player...   │
  │Enemy...    │
  │Missile...  │      Core层
  │Alpha...    │      ┌────────────┐
  │Outline...  │─────▶│Physics     │
  │Lead...     │      │Renderer    │
  │SlowMo...   │      │SiegeHelper │
  │Pool        │      │PredMath    │
  └────────────┘      └────────────┘
```

---

## 三、数据流

### 每帧流程 (OnMissionTick)

```
每帧开始
  │
  ├─ 0. ProcessPendingReload()         ← 热加载XML配置
  │
  ├─ 1. EnemyTrajectorySystem.Update() ← 扫描+追踪+渲染敌人弹道
  │    ├─ UpdateTracking(player)        ← 扫描/清理追踪列表
  │    └─ UpdateSingle(enemy)           ← 调用 PlayerTrajectorySystem.SimulateAndRender
  │       └─ TrajectoryPhysics.CalculatePosition → TrajectoryRenderer.PlaceGameEntityMarker
  │
  ├─ 2. PlayerTrajectorySystem.Update(player)
  │    ├─ UpdateRangeWeapon()           ← 远程武器
  │    │   └─ SimulateAndRender()       ← 核心模拟+渲染循环
  │    └─ UpdateSiegeWeapon()           ← 攻城武器
  │        ├─ AlphaBlurSystem.SetSiegeTargetAlpha  ← 抬头虚化
  │        └─ SimulateAndRender()
  │
  ├─ 3. EnemyOutlineSystem.Update()     ← 敌人描边
  │
  ├─ 4. MissileTrajectorySystem.DrawAllStoredTrajectories() ← 投射物DebugLine持续绘制
  │
  ├─ 5. AlphaBlurSystem.UpdateSmoothing() ← Alpha平滑过渡
  │
  ├─ 6. LeadPredictionSystem.Update(player) ← 移动目标预瞄
  │    ├─ LeadPredictionMath.CalculateLeadPosition
  │    └─ ProjectileTrajectorySystem.DrawLeadTrajectory
  │
  ├─ 7. UpdateEnemyMotionData(dt)       ← 敌人运动数据更新
  │
  └─ 8. SlowMotionSystem.Update()       ← 近敌慢动作
```

### 事件驱动流程

```
OnAgentShootMissile()
  │
  └─ MissileTrajectorySystem.OnAgentShootMissile()
       ├─ 缓存初速 → ProjectileSpeedCache
       └─ ProjectileTrajectorySystem.UpdateMissileTrajectory()
            └─ SimulateMissileTrajectory()  ← 一次性计算完整弹道
                 ├─ TrajectoryPhysics.CalculatePosition
                 ├─ TrajectoryRenderer.PlaceGameEntityMarker
                 └─ TrajectoryRenderer.DrawDebugLineSegment

OnMissileHit()
  │
  └─ MissileTrajectorySystem.OnMissileHit()
       ├─ ProjectileTrajectorySystem.ClearMissileTrajectory()  ← GameEntity归还池
       └─ StoredMissileTrajectoryPoints.Remove()              ← DebugLine清理
```

---

## 四、Core 层详解

### 4.1 `TrajectoryPhysics` — 弹道物理 (internal static)

```
职责: 纯弹道计算，无任何引擎交互
常量:
  Gravity = 9.81f
  IntegrationDt = 0.01f

方法:
  CalculatePosition(origin, direction, speed, time, airFriction, override?)
    → 欧拉积分法计算 t 秒后的弹道位置

  CalculateFiringSolution(start, end, speed, gravity)
    → 抛物线发射解算，返回速度矢量（无解返回 Vec3.Invalid）

  CalculateFlightTime(velocityVector, shooterZ, targetZ)
    → 根据抛物线方程计算飞行时间
```

### 4.2 `TrajectoryRenderer` — 双管线渲染 (internal static)

```
职责: 弹道可视化，GameEntity + DebugLine 双管线
依赖: GameEntityPool, SkillSystemBehavior (WoW_Line)

GameEntity 管线:
  PlaceGameEntityMarker(pos, timeKey, line, color)
    → 从池获取实体、设置位置、遮挡检测+颜色缓存
  ClearTrajectoryAfterCollision(collisionKey)
    → 碰撞后清理后续实体
  GenerateImpactCircle(center)
    → 落点圆环标记

DebugLine 管线:
  DrawDebugLineSegment(position, direction, color, time)
  RenderDebugLine(position, direction, color, depthCheck, time)
    → 反射调用引擎 IDebug.RenderDebugLine
  DrawDebugLineTrajectory(points, color)
    → 批量绘制轨迹点连线

碰撞检测:
  TryDetectCollision(pos, timeKey) → bool
    → 射线检测地形碰撞
```

### 4.3 `SiegeWeaponHelper` — 攻城武器反射 (internal static)

```
职责: 通过反射访问引擎私有 API
方法:
  GetShootingSpeed(RangedSiegeWeapon) → float
  GetShootingDirection(RangedSiegeWeapon) → Vec3
  GetProjectileStartPosition(RangedSiegeWeapon) → Vec3
    → Ballista: ProjectileEntityCurrentGlobalPosition
    → Mangonel/Trebuchet: "projectile_leaving_position" 节点
  GetAirFriction(Agent, out EquipmentIndex) → float
    → ItemObject.GetAirFrictionConstant()
```

### 4.4 `LeadPredictionMath` — 预瞄算法 (internal static)

```
职责: 移动目标提前量迭代拟合
常量:
  MaxIterations = 5
  ConvergenceThreshold = 0.001f

算法流程:
  1. 初始估算: time = horizontalDistance / projectileSpeed
  2. 迭代:
     a. predictedPos = targetPos + velocity * time
     b. shotDir = CalculateFiringSolution(shooter, predicted, speed, gravity)
     c. 有解: time = CalculateFlightTime(shotDir * speed, ...)
     d. 无解: time = horizontalDistance / speed (简化估算)
  3. 返回 targetPos + velocity * finalTime
```

---

## 五、Systems 层详解

### 5.1 `PlayerTrajectorySystem` — 玩家弹道 (internal static)

```
职责: 远程武器 + 攻城武器的弹道更新
内部状态:
  _hasCollided, _airFriction

入口:
  Update(Agent player)
    ├─ 远程武器: UpdateRangeWeapon(player)
    └─ 攻城武器: UpdateSiegeWeapon(player, enableBlur)

核心方法:
  SimulateAndRender(startPos, direction, speed, timeStart, timeEnd,
                    timeStep, timeKeyOffset, customLine, baseColor)
    → 调用 TrajectoryPhysics.CalculatePosition
    → 调用 TrajectoryRenderer.PlaceGameEntityMarker / DrawDebugLineSegment
```

### 5.2 `EnemyTrajectorySystem` — 敌人弹道 (internal static)

```
职责: 扫描瞄准玩家的敌人，显示红色预测射击线
内部状态:
  _tracked: List<Agent>  ← 当前追踪的敌人列表
  _hasCollided, _airFriction

入口:
  Update()           ← 扫描+追踪+渲染
  UpdateSingle(enemy) ← 单个敌人弹道
  ClearTrajectory(agentIndex)
  IsAimingPlayer(agent, player) → bool
```

### 5.3 `MissileTrajectorySystem` — 投射物弹道 (internal static)

```
职责: 基于 OnAgentShootMissile 的一次性弹道计算与持续显示
入口:
  OnAgentShootMissile(...)  ← 事件回调
  OnMissileHit(...)         ← 事件回调
  DrawAllStoredTrajectories() ← 每帧持续绘制 DebugLine
```

### 5.4 `AlphaBlurSystem` — 抬头虚化 (internal static)

```
职责: 高角度仰射时虚化模型
常量:
  LookUpThreshold = 0.2f
  DefaultAlpha = 1.0f
  BlurAlpha = 0.03f

内部状态:
  _entityById: Dictionary<int, WeakGameEntity>
  _currentAlphaById: Dictionary<int, float>
  _targetAlphaById: Dictionary<int, float>
  _siegeChildEntities: Dictionary<RangedSiegeWeapon, List<WeakGameEntity>>

方法:
  SetAgentTargetAlpha(agent, targetAlpha)
  SetSiegeTargetAlpha(siege, targetAlpha)    ← 递归收集所有子实体
  UpdateSmoothing()                           ← 每帧平滑插值
  RestoreAll()                                ← 恢复所有实体 alpha
  IsLookingUp(lookDir) → bool                ← lookDir.z > 0.2
```

### 5.5 `EnemyOutlineSystem` — 敌人描边 (internal static)

```
职责: 瞄准玩家的敌方远程单位红色轮廓描边
内部状态:
  _cache: Dictionary<Agent, uint?>  ← 描边颜色缓存

方法:
  Update()  ← 扫描+描边+清理
  Clear()   ← 移除所有描边
```

### 5.6 `LeadPredictionSystem` — 预瞄 (internal static)

```
职责: 迭代拟合计算提前量，绘制预测线和标记
方法:
  Update(player)     ← 主入口
  GetNearestEnemy()  ← 最近敌人
  DrawMarker(pos)    ← 预瞄标记 GameEntity
  Clear()            ← 清理预测线+标记
```

### 5.7 `SlowMotionSystem` — 慢动作 (internal class)

```
职责: 近敌拉弓瞄准时降低时间流速
内部状态:
  _slowMoActive: bool
  _currentTimeScale: float

方法:
  Update()            ← 主入口（每帧调用）
  RestoreTime()       ← 恢复正常速度
  HasNearbyEnemy()    ← 半径内敌人检测
  ApplyTimeScale()    ← 申请时间缩放
```

### 5.8 `GameEntityPool` — 对象池 (internal static)

```
职责: GameEntity 复用，避免 GC 压力
内部状态:
  _pool: Stack<GameEntity>
  ColorCache: Dictionary<GameEntity, uint>  ← 描边颜色缓存

方法:
  GetOrCreate() → GameEntity
  Return(entity)
  Clear()
```

---

## 六、关键设计模式

### 6.1 薄编排层模式

```csharp
// SkillSystemBehavior.OnMissionTick — 仅 ~30 行
public override void OnMissionTick(float dt)
{
    ProjectileTrajectorySettingsManager.ProcessPendingReload();
    if (!settings.EnableTrajectory) return;

    if (settings.EnemyTrajectory) EnemyTrajectorySystem.Update();
    if (settings.PlayerTrajectory) PlayerTrajectorySystem.Update(player);
    if (settings.EnemyHighlight) EnemyOutlineSystem.Update();
    if (settings.MissileTrajectory) MissileTrajectorySystem.DrawAllStoredTrajectories();
    AlphaBlurSystem.UpdateSmoothing();
    if (settings.EnableLeadPrediction) LeadPredictionSystem.Update(player);
    UpdateEnemyMotionData(dt);
    _slowMotion.Update();
}
```

### 6.2 薄外观层模式

```csharp
// ProjectileTrajectorySystem — public API 委托到 Core/Systems
public static Vec3 CalculateLeadPosition(Agent player, Agent target, float speed)
    => LeadPredictionMath.CalculateLeadPosition(player, target, speed);

public static float GetSiegeShootingSpeed(RangedSiegeWeapon weapon)
    => SiegeWeaponHelper.GetShootingSpeed(weapon);
```

### 6.3 对象池模式

```
获取: GameEntityPool.GetOrCreate()
  → pool.Count > 0 ? Pop + 重置 : CreateEmpty

归还: GameEntityPool.Return(entity)
  → 归位 + 清除颜色缓存 + Push
```

### 6.4 颜色缓存优化

```csharp
// 避免每帧重复调用 SetContourColor
if (!ColorCache.TryGetValue(entity, out var cached) || cached != effectiveColor)
{
    entity.SetContourColor(color, true);
    ColorCache[entity] = effectiveColor;
}
```

### 6.5 Alpha 平滑过渡

```
每帧: next = current + (target - current) * 0.15
到达后: 清理缓存（减少维护开销）
```

---

## 七、依赖关系图

```
SubModule.cs
  └─ ProjectileTrajectorySettingsManager (static)

SkillSystemBehavior.cs (编排层)
  ├─ ProjectileTrajectorySettingsManager.Settings
  ├─ PlayerTrajectorySystem
  ├─ EnemyTrajectorySystem
  ├─ MissileTrajectorySystem
  ├─ EnemyOutlineSystem
  ├─ AlphaBlurSystem
  ├─ LeadPredictionSystem
  ├─ SlowMotionSystem
  ├─ GameEntityPool
  └─ SkillSystemBehavior (自身的 static 字典)

ProjectileTrajectorySystem.cs (外观层)
  ├─ PlayerTrajectorySystem
  ├─ EnemyTrajectorySystem
  ├─ MissileTrajectorySystem
  ├─ LeadPredictionMath
  ├─ TrajectoryPhysics
  ├─ TrajectoryRenderer
  ├─ SiegeWeaponHelper
  └─ SkillSystemBehavior (static 字典)

PlayerTrajectorySystem.cs
  ├─ TrajectoryPhysics
  ├─ TrajectoryRenderer
  ├─ SiegeWeaponHelper
  ├─ AlphaBlurSystem
  ├─ GameEntityPool
  └─ SkillSystemBehavior (ProjectileSpeedCache, WoW_Line)

EnemyTrajectorySystem.cs
  ├─ PlayerTrajectorySystem (SimulateAndRender)
  ├─ SiegeWeaponHelper
  ├─ GameEntityPool
  └─ SkillSystemBehavior (EnemyTrajectoryLines)

MissileTrajectorySystem.cs
  ├─ ProjectileTrajectorySystem (UpdateMissileTrajectory, ClearMissileTrajectory)
  ├─ SiegeWeaponHelper
  ├─ TrajectoryRenderer
  └─ SkillSystemBehavior (ProjectileSpeedCache, StoredMissileTrajectoryPoints)

LeadPredictionSystem.cs
  ├─ LeadPredictionMath
  ├─ ProjectileTrajectorySystem (DrawLeadTrajectory)
  └─ SkillSystemBehavior (LeadPredictionLine, PredictionMarker)

NavalDLCTrajectorySupport.cs
  ├─ SiegeWeaponHelper
  ├─ AlphaBlurSystem
  └─ ProjectileTrajectorySystem (UpdateTrajectory)

Settings/ 三层
  ├─ ProjectileTrajectorySettings → SyncFromMCM → ProjectileTrajectorySettingsManager
  └─ ProjectileTrajectorySettingsManager → Load/Save → ProjectileTrajectorySettingsData
```

---

## 八、性能优化策略

| 策略 | 位置 | 效果 |
|------|------|------|
| GameEntity 对象池 | `GameEntityPool` | 避免 GC 压力 |
| 描边颜色缓存 | `GameEntityPool.ColorCache` | 跳过重复 SetContourColor |
| 遮挡检测+透明化 | `TrajectoryRenderer.PlaceGameEntityMarker` | 被遮挡的弹道点不显示 |
| 投射物初速缓存 | `SkillSystemBehavior.ProjectileSpeedCache` | 避免重复计算弹速 |
| Alpha 到达后清理缓存 | `AlphaBlurSystem.UpdateSmoothing` | 减少维护开销 |
| DebugLine 直接绘制 | `TrajectoryRenderer` | 零 GameEntity 开销 |
| 最大追踪数限制 | `EnemyTrajectorySystem.MaxTracked` | 控制敌人弹道计算量 |
