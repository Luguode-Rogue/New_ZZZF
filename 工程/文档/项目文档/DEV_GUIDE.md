# 开发指南 - ProjectileTrajectorySystem

> 面向想要理解或修改本 Mod 代码的开发者。重构后版本。

---

## 目录

1. [技术栈](#技术栈)
2. [文件组织与职责](#文件组织与职责)
3. [Public API 签名](#public-api-签名)
4. [Core 层详解](#core-层详解)
5. [Systems 层详解](#systems-层详解)
6. [如何添加新功能](#如何添加新功能)
7. [调试技巧](#调试技巧)
8. [常见问题](#常见问题)

---

## 技术栈

| 层面 | 技术 | 用途 |
|------|------|------|
| 语言 | C# 10.0 | 主要开发语言 |
| 框架 | .NET Framework 4.7.2 / .NET 6 | 双目标框架 |
| 游戏引擎 | TaleWorlds Engine | 渲染、物理、场景 |
| 反射 | System.Reflection | 访问引擎非公开 API |
| XML 序列化 | System.Xml.Serialization | 配置持久化 |
| MCM v5 | MCM.Abstractions | 设置 UI |
| 数学库 | TaleWorlds.Library.MathF | 三角函数、Clamp 等（必须全限定，避免与 System.MathF 歧义） |

---

## 文件组织与职责

### 入口文件

| 文件 | 行数 | 职责 |
|------|------|------|
| `SubModule.cs` | ~30 | Mod 入口，注册 MissionBehavior |
| `SkillSystemBehavior.cs` | ~240 | **薄编排层**，OnMissionTick 委托给子系统 |
| `ProjectileTrajectorySystem.cs` | ~180 | **薄外观层**，public API 委托给 Core/Systems |

### Core 层（纯计算/渲染，无状态）

| 文件 | 职责 |
|------|------|
| `Core/TrajectoryPhysics.cs` | 欧拉积分弹道计算、抛物线解算、飞行时间计算 |
| `Core/TrajectoryRenderer.cs` | GameEntity 图标渲染（含遮挡检测+颜色缓存）、DebugLine 渲染、碰撞检测 |
| `Core/SiegeWeaponHelper.cs` | 攻城武器反射接口（ShootingSpeed/Direction/StartPosition/AirFriction） |
| `Core/LeadPredictionMath.cs` | 移动目标提前量迭代拟合算法 |

### Systems 层（有状态子系统）

| 文件 | 职责 |
|------|------|
| `Systems/GameEntityPool.cs` | 对象池 + 颜色缓存 |
| `Systems/AlphaBlurSystem.cs` | 抬头虚化（Alpha 平滑插值） |
| `Systems/PlayerTrajectorySystem.cs` | 玩家弹道（远程武器 + 攻城武器） |
| `Systems/EnemyTrajectorySystem.cs` | 敌人弹道追踪（扫描+渲染+清理） |
| `Systems/EnemyOutlineSystem.cs` | 敌人红色描边 |
| `Systems/LeadPredictionSystem.cs` | 移动目标预瞄 |
| `Systems/SlowMotionSystem.cs` | 近敌慢动作 |
| `Systems/MissileTrajectorySystem.cs` | 投射物弹道（OnShoot + OnHit + 持续绘制） |

### Settings 层（三层架构）

| 文件 | 职责 |
|------|------|
| `Settings/ProjectileTrajectorySettings.cs` | MCM UI 层（AttributeGlobalSettings） |
| `Settings/ProjectileTrajectorySettingsData.cs` | XML 数据模型（[XmlRoot]） |
| `Settings/ProjectileTrajectorySettingsManager.cs` | 管理层（Load/Save/热重载/SyncFromMCM） |

### DLC 层

| 文件 | 职责 |
|------|------|
| `DLC/NavalDLCTrajectorySupport.cs` | 海战 DLC 支持（纯反射，零硬依赖） |

---

## Public API 签名

> **重要**：以下所有签名必须保持不变，外部 Mod 可能依赖它们。

### SkillSystemBehavior（实例类，继承 MissionLogic）

```csharp
// 静态字段
public static readonly Dictionary<float, GameEntity> LeadPredictionLine;
public static GameEntity PredictionMarker;
public static readonly Dictionary<Agent, AgentMotionData> TrackedAgents;
public class AgentMotionData { Vec3 LastPosition; Vec3 Velocity; float LastUpdateTime; }
public class AgentMissileSpeedData { Agent Agent; MissionWeapon Weapon; float MissileSpeed; }
public static readonly Dictionary<int, List<AgentMissileSpeedData>> WoW_AgentMissileSpeedData;
public static readonly Dictionary<float, GameEntity> WoW_Line;
public static readonly List<GameEntity> WoW_CustomGameEntity;
public static readonly Dictionary<int, Dictionary<float, GameEntity>> EnemyTrajectoryLines;
public static readonly Dictionary<int, List<Vec3>> StoredMissileTrajectoryPoints;
public static readonly Dictionary<int, Dictionary<float, GameEntity>> MissileTrajectoryLines;
public static readonly Dictionary<GameEntity, uint> GameEntityColorCache;
public static readonly Dictionary<(int, string, string), float> ProjectileSpeedCache;
public static string debugString1;
public static string debugString2;

// 方法
public static void SetSiegeTargetAlpha(RangedSiegeWeapon siege, float targetAlpha);
public bool TryGetCachedMissileSpeed(Agent agent, string weapon, string ammo, out float speed);
```

### ProjectileTrajectorySystem（静态类）

```csharp
// 弹道更新
public static void UpdateTrajectory(Agent agent, RangedSiegeWeapon siegeWeapon = null);
public static void UpdateTrajectoryRangeWeapon(Agent agent);
public static void UpdateEnemyTrajectory(Agent enemy);
public static void UpdateMissileTrajectory(int missileIndex, Vec3 startPos, Vec3 direction,
    float speed, float airFriction, float maxTime = 3.0f, float timeStep = 0.1f);

// 清理
public static void ClearNormalTrajectory();
public static void ClearEnemyTrajectory(int agentIndex);
public static void ClearMissileTrajectory(int missileIndex);

// 预瞄
public static void DrawLeadTrajectory(Agent player, Vec3 targetPredictedPos, float speed);
public static Vec3 CalculateLeadPosition(Agent player, Agent target, float projectileSpeed);
public static Vec3 CalculateProjectileFiringSolution(Vec3 start, Vec3 end, float speed, float gravity);

// 攻城武器反射
public static float GetSiegeShootingSpeed(RangedSiegeWeapon weapon);
public static Vec3 GetSiegeShootingDirection(RangedSiegeWeapon weapon);
public static Vec3 GetSiegeProjectileStartPosition(RangedSiegeWeapon weapon);
public static float GetWeaponAirFriction(Agent agent, out EquipmentIndex _);

// 渲染
public static void RenderDebugLine(Vec3 position, Vec3 direction, uint color, bool depthCheck, float time);
public static void DrawDebugLineTrajectory(List<Vec3> points, uint color);
```

---

## Core 层详解

### TrajectoryPhysics

```csharp
internal static class TrajectoryPhysics
{
    public const float Gravity = 9.81f;
    public const float IntegrationDt = 0.01f;

    // 欧拉积分：velocity += friction*v²*dt + gravity*dt, position += velocity*dt
    public static Vec3 CalculatePosition(
        Vec3 origin, Vec3 direction, float speed, float time,
        float airFriction, float airFrictionOverride = -1f);

    // 抛物线发射解算：给定起终点和初速，求发射速度矢量
    public static Vec3 CalculateFiringSolution(
        Vec3 start, Vec3 end, float speed, float gravity);

    // 飞行时间：根据抛物线方程
    public static float CalculateFlightTime(
        Vec3 velocityVector, float shooterZ, float targetZ);
}
```

**注意事项**：
- `CalculatePosition` 的 `airFrictionOverride` 参数：传入 >= 0 时覆盖 `airFriction`（投射物弹道使用）
- 内部使用 `System.Math.Sqrt/Atan2/Cos/Sin`（非 `MathF`，避免歧义）

### TrajectoryRenderer

```csharp
internal static class TrajectoryRenderer
{
    // GameEntity 管线
    public static void PlaceGameEntityMarker(
        Vec3 pos, float timeKey, Dictionary<float, GameEntity> line, uint color);
    public static void ClearTrajectoryAfterCollision(float collisionKey);
    public static void GenerateImpactCircle(Vec3 center);

    // DebugLine 管线（反射调用引擎 IDebug）
    public static void DrawDebugLineSegment(Vec3 position, Vec3 direction, uint color, float time);
    public static void RenderDebugLine(Vec3 position, Vec3 direction, uint color,
        bool depthCheck, float time);
    public static void DrawDebugLineTrajectory(List<Vec3> points, uint color);

    // 碰撞检测
    public static bool TryDetectCollision(Vec3 pos, float timeKey);
}
```

### SiegeWeaponHelper

```csharp
internal static class SiegeWeaponHelper
{
    public static float GetShootingSpeed(RangedSiegeWeapon weapon);
    public static Vec3 GetShootingDirection(RangedSiegeWeapon weapon);
    public static Vec3 GetProjectileStartPosition(RangedSiegeWeapon weapon);
    public static float GetAirFriction(Agent agent, out EquipmentIndex _);
}
```

**反射路径**：
- `ShootingSpeed` / `ShootingDirection`：`RangedSiegeWeapon` 的 `non-public instance` 属性
- `ProjectileStartPosition`：Ballista 用 `ProjectileEntityCurrentGlobalPosition`；其他用 `"projectile_leaving_position"` 节点
- `GetAirFriction`：调用 `ItemObject.GetAirFrictionConstant(WeaponClass, WeaponFlags)`

### LeadPredictionMath

```csharp
internal static class LeadPredictionMath
{
    // 迭代拟合：5次迭代，收敛阈值0.001s
    public static Vec3 CalculateLeadPosition(Agent player, Agent target, float projectileSpeed);
}
```

---

## Systems 层详解

### PlayerTrajectorySystem

```csharp
internal static class PlayerTrajectorySystem
{
    public static void Update(Agent player);
    public static void SimulateAndRender(
        Vec3 startPos, Vec3 direction, float speed,
        float timeStart, float timeEnd, float timeStep, float timeKeyOffset,
        Dictionary<float, GameEntity> customLine, uint baseColor);
    public static void ResetFrameState();
}
```

**渲染参数差异**：

| 参数 | 远程武器 | 攻城武器 |
|------|---------|---------|
| timeStart | 0f | 0.3f |
| timeEnd | 3.0f | 4.8f |
| timeStep | 0.15f | 0.15f |
| baseColor | 0xFF00FFFFu (青) | 0xFF00FFFFu (青) |

### EnemyTrajectorySystem

```csharp
internal static class EnemyTrajectorySystem
{
    public static void Update();
    public static void UpdateSingle(Agent enemy);
    public static void ClearTrajectory(int agentIndex);
    public static bool IsAimingPlayer(Agent agent, Agent player);
}
```

**追踪逻辑**：每帧清理不再瞄准的敌人 → 扫描新敌人（上限 `MaxTrackedEnemiesLegacy`）→ 渲染弹道

### AlphaBlurSystem

```csharp
internal static class AlphaBlurSystem
{
    public const float LookUpThreshold = 0.2f;
    public const float DefaultAlpha = 1.0f;
    public const float BlurAlpha = 0.03f;

    public static void SetAgentTargetAlpha(Agent agent, float targetAlpha);
    public static void SetSiegeTargetAlpha(RangedSiegeWeapon siege, float targetAlpha);
    public static void UpdateSmoothing();
    public static void RestoreAll();
    public static bool IsLookingUp(Vec3 lookDir);
}
```

---

## 如何添加新功能

### 示例：添加"弹道颜色自定义"

1. **数据层** (`Settings/ProjectileTrajectorySettingsData.cs`)：
```csharp
public uint PlayerTrajectoryColor = 0xFF00FFFFu;
```

2. **MCM UI 层** (`Settings/ProjectileTrajectorySettings.cs`)：
```csharp
[SettingPropertyGroup("{=PTS_G001}Trajectory Display")]
[SettingPropertyColor("{=PTS_020}Player Trajectory Color", ...)]
public uint PlayerTrajectoryColor { get; set; } = 0xFF00FFFFu;
```

3. **同步层** (`Settings/ProjectileTrajectorySettingsManager.cs`)：
   - `SyncFromMCM` 中添加 `Data.PlayerTrajectoryColor = mcm.PlayerTrajectoryColor;`
   - 构造函数中添加 `PlayerTrajectoryColor = data.PlayerTrajectoryColor;`

4. **使用** (`Systems/PlayerTrajectorySystem.cs`)：
```csharp
uint color = ProjectileTrajectorySettingsManager.Settings.PlayerTrajectoryColor;
```

### 示例：添加新的子系统

1. 在 `Systems/` 下创建新文件 `XxxSystem.cs`
2. 声明为 `internal static class XxxSystem` 或 `internal class XxxSystem`
3. 在 `SkillSystemBehavior.OnMissionTick` 中添加调用
4. 如需 public API，在 `ProjectileTrajectorySystem` 中添加委托方法

### 示例：添加新的事件响应

```csharp
// 在 SkillSystemBehavior 中重写事件方法
public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent,
                                  int damage, ...)
{
    // 委托给子系统
    XxxSystem.OnAgentHit(affectedAgent, affectorAgent, damage);
}
```

Bannerlord 可用生命周期回调：
- `OnCreated()` / `OnEndMission()` — 创建和销毁
- `OnMissionTick(float dt)` — 每帧
- `OnAgentShootMissile(...)` — 发射投射物
- `OnMissileHit(...)` — 投射物命中
- `OnAgentHit(...)` — 角色受伤
- `OnAgentKilled(...)` — 角色死亡

---

## 调试技巧

### 1. 使用 InformationMessage 输出日志

```csharp
InformationManager.DisplayMessage(
    new InformationMessage($"弹速: {speed}, 位置: {position}"));
```

### 2. 查看 DebugLine 颜色

| 颜色 | 含义 |
|------|------|
| 青→红→绿→蓝→紫→黄... | 玩家弹道（彩虹色，按距离分段） |
| 红色 (0xFFFF4444) | 敌人弹道 |
| 绿色 (0xFF00FF00) | 飞行中投射物轨迹 |
| 红色 (0xFFFF0000) | 移动目标预瞄线 |

### 3. 检查 XML 配置

配置文件：`Modules/ProjectileTrajectorySystem/ProjectileTrajectorySettings.xml`
修改后 300ms 内自动热重载。

### 4. .NET 兼容性注意事项

| 陷阱 | 正确做法 |
|------|---------|
| `dict.GetValueOrDefault(key)` | `dict.ContainsKey(key) ? dict[key] : default` (.NET 4.7.2 无此方法) |
| `MathF.Abs(x)` | `TaleWorlds.Library.MathF.Abs(x)` (避免与 System.MathF 歧义) |
| `MathF.Sqrt/PI/Cos/Sin` | `TaleWorlds.Library.MathF.xxx` 或 `System.Math.xxx` |

---

## 常见问题

### Q: 为什么 DebugLine 在开启 DLSS 时显示异常？

A: DLSS 在低分辨率渲染后上采样，DebugLine 在原生分辨率绘制导致错位。切换到 GameEntity 显示即可。

### Q: 为什么 GameEntity 显示效果不好？

A: GameEntity 使用 `mangonel_mapicon_projectile` 模型（投石车小图标），只是标记点不是线段。

### Q: NavalDLC 弹道不显示？

A: 确认安装了 Naval DLC 且玩家在操作战舰攻城武器。`Init()` 在 AppDomain 中搜索 `NavalDLC.Missions.AgentNavalComponent`，找不到则自动禁用。

### Q: 配置文件不生效？

A: 确保 XML 格式正确，`FileSystemWatcher` 正常工作。也可重启游戏让 `Load()` 重新读取。

### Q: 如何关闭某个功能？

A: 通过 MCM 设置菜单或直接编辑 XML：
```xml
<EnableTrajectory>false</EnableTrajectory>  <!-- 总开关 -->
<EnemyTrajectory>false</EnemyTrajectory>    <!-- 关闭敌人弹道 -->
```

### Q: 外部 Mod 依赖的 API 签名会变吗？

A: **不会**。所有 `SkillSystemBehavior` 的 public static 字段和 `ProjectileTrajectorySystem` 的 public 方法签名保持不变。内部实现改为委托到 Core/Systems 层，但外部调用者无感知。
