---
name: siege-weapon-shoot-debug-mcm
overview: 添加MCM菜单"攻城器射击debug"，通过Harmony补丁攻城器射击散布函数，开启时消除随机散布
todos:
  - id: add-mcm-settings
    content: 在Settings三层（SettingsData/Settings/SettingsManager）中添加SiegeWeaponShootDebug开关属性和同步逻辑
    status: completed
  - id: create-harmony-patch
    content: 新建SiegeWeaponShootDebugPatch.cs，实现Harmony Prefix补丁拦截GetBallisticErrorAppliedDirection
    status: completed
    dependencies:
      - add-mcm-settings
  - id: init-harmony
    content: 在SubModule.cs中初始化Harmony实例并调用PatchAll()
    status: completed
    dependencies:
      - create-harmony-patch
---

## 产品概述

在现有的骑砍2弹道轨迹Mod中，新增"攻城器射击debug"功能，通过MCM菜单开关控制，当开启时使用Harmony补丁移除攻城器投射物的随机散布和速度随机化，使攻城武器完全精准射击。

## 核心功能

- MCM菜单新增"攻城器射击debug"分组，包含一个布尔开关，附带说明文字
- 开关关闭时：攻城器射击行为与原版完全一致（有随机弹道散布和速度随机）
- 开关开启时：攻城器投射物发射方向不施加随机散布，射击速度不施加随机化，直接沿原始射击方向以固定速度发射
- 设置实时生效，无需重启游戏
- 设置通过XML持久化，与现有MCM双向同步机制一致

## 技术栈

- 语言：C# (.NET Framework 4.72 / .NET 6 双目标)
- 框架：Mount & Blade II: Bannerlord Modding API
- 补丁框架：HarmonyLib（项目已引用 0Harmony.dll + Lib.Harmony NuGet 2.3.3）
- UI框架：MCM v5（Mod Configuration Menu，项目已集成）
- 持久化：XML序列化（沿用现有模式）

## 实现方案

### 核心策略

使用Harmony Prefix补丁拦截 `RangedSiegeWeapon.SetupProjectileToShoot(bool, out Vec3, out Mat3, out float, out float)` 受保护方法。当MCM开关开启时，Prefix方法完全接管射击参数计算：方向使用原始 `ShootingDirection`（无散布），速度使用固定 `ShootingSpeed`（无随机化），加上 `GetGlobalVelocity()`，返回 `false` 跳过原方法。

### 关键技术决策

**补丁目标选择**：补丁 `SetupProjectileToShoot` 而非 `GetBallisticErrorAppliedDirection`

- 原因：用户要求同时禁用方向散布和速度随机化，`SetupProjectileToShoot` 是唯一同时处理这两个随机源的方法
- `GetBallisticErrorAppliedDirection` 只处理方向散布，无法覆盖散弹模式的速度 ±10% 随机
- 补丁 `SetupProjectileToShoot` 可以一次性替换所有 out 参数，彻底消除所有随机因素
- 原方法逻辑简单（仅计算方向+速度+全局速度合成），完全接管无副作用

**获取非公开成员的方式**：

- `ShootingDirection`：复用 `SiegeWeaponHelper.GetShootingDirection()`
- `ShootingSpeed`：复用 `SiegeWeaponHelper.GetShootingSpeed()`
- `GetGlobalVelocity()`：通过 `AccessTools.Method(typeof(RangedSiegeWeapon), "GetGlobalVelocity")` 反射调用

**Harmony生命周期**：在 `SubModule.OnSubModuleLoad()` 中调用 `new Harmony("com.projectiletrajectory.siegedebug").PatchAll()`

- 使用 `PatchAll()` 自动发现程序集中所有 `[HarmonyPatch]` 类
- 补丁在运行时根据MCM开关条件决定是否生效（Prefix中检查开关状态），不需要动态patch/unpatch

## 实现要点

### 补丁条件判断

Prefix方法内部检查 `ProjectileTrajectorySettings.Instance?.SiegeWeaponShootDebug == true`，只有开启时才替换所有out参数并跳过原方法。关闭时返回 `true` 让原方法正常执行。

### 无随机化的完整射击逻辑

当开关开启时，等价于原方法中去掉所有随机因素后的逻辑：

```
orientation = Mat3.Identity;
Vec3 shootingDir = SiegeWeaponHelper.GetShootingDirection(__instance);  // 无散布
float shootingSpd = SiegeWeaponHelper.GetShootingSpeed(__instance);     // 无随机速度
orientation.f = shootingDir;
orientation.Orthonormalize();
Vec3 globalVel = (Vec3)accessGetGlobalVelocity.Invoke(__instance, null);
direction = shootingSpd * orientation.f + globalVel;
missileShootingSpeed = direction.Normalize();
missileBaseSpeed = shootingSpd;
return false;  // 跳过原方法
```

### 散弹模式兼容

散弹模式（`randomizeMissileSpeed=true`）原本会速度 ±10% 随机 + 2.5度散布，开启debug后也全部消除，行为一致且符合预期。

### 反射性能

`SiegeWeaponHelper` 的反射调用和 `GetGlobalVelocity()` 反射调用，每次攻城器射击时触发。攻城器射击频率极低（每次攻击数秒），性能影响可忽略。

### MCM说明文字

`[SettingPropertyBool]` 使用 `HintText` 参数添加说明："开启后，攻城器（弩炮/投石机/回回炮）射击将消除所有随机散布和速度随机化，投射物将沿精确方向以固定速度发射。"

## 目录结构

```
ProjectileTrajectorySystem/
├── Core/
│   ├── SiegeWeaponHelper.cs              # [现有] 反射辅助，已有GetShootingDirection()和GetShootingSpeed()
│   └── SiegeWeaponShootDebugPatch.cs     # [新建] Harmony Prefix补丁类，拦截SetupProjectileToShoot
├── Settings/
│   ├── ProjectileTrajectorySettings.cs   # [修改] 新增SiegeWeaponShootDebug属性、MCM分组和说明
│   ├── ProjectileTrajectorySettingsData.cs # [修改] 新增SiegeWeaponShootDebug字段
│   └── ProjectileTrajectorySettingsManager.cs # [修改] 新增SiegeWeaponShootDebug同步逻辑
└── SubModule.cs                          # [修改] 初始化Harmony实例并PatchAll()
```

### 文件详细说明

**Core/SiegeWeaponShootDebugPatch.cs [新建]**

- Harmony Prefix补丁，目标方法：`RangedSiegeWeapon.SetupProjectileToShoot`
- 由于目标方法是protected，需使用 `[HarmonyPatch(typeof(RangedSiegeWeapon), "SetupProjectileToShoot")]` 指定方法名
- Prefix签名：`static bool Prefix(RangedSiegeWeapon __instance, bool randomizeMissileSpeed, ref Vec3 direction, ref Mat3 orientation, ref float missileBaseSpeed, ref float missileShootingSpeed)`
- 逻辑：若开关开启，计算无随机的射击参数（ShootingDirection + ShootingSpeed + GetGlobalVelocity），设到out参数，返回 `false`；否则返回 `true`
- 缓存 `GetGlobalVelocity` 的 MethodInfo 为静态字段，避免每次反射查找

**Settings/ProjectileTrajectorySettings.cs [修改]**

- 新增 `[SettingPropertyGroup("{=PTS_G006}攻城器射击debug")]` 分组
- 新增 `[SettingPropertyBool("{=PTS_S031}攻城器射击Debug", HintText = "开启后，攻城器（弩炮/投石机/回回炮）射击将消除所有随机散布和速度随机化，投射物将沿精确方向以固定速度发射。")]` 属性 `SiegeWeaponShootDebug`，默认 `false`，Order = 30
- 构造函数中从 `data.SiegeWeaponShootDebug` 初始化

**Settings/ProjectileTrajectorySettingsData.cs [修改]**

- 新增 `public bool SiegeWeaponShootDebug = false;`

**Settings/ProjectileTrajectorySettingsManager.cs [修改]**

- `SyncFromMCM` 方法新增 `Data.SiegeWeaponShootDebug = mcm.SiegeWeaponShootDebug;`

**SubModule.cs [修改]**

- 在 `OnSubModuleLoad()` 中新增 Harmony 实例创建和 `PatchAll()` 调用
- 保存 Harmony 实例以便未来 `UnpatchAll`（如模块卸载时）