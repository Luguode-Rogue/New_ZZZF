# ContinuousCollision

基于 Bannerlord 原版反编译源码实现的连续近战碰撞层。

## 目标

让武器在没有原版攻击动画碰撞事件时，仍能通过逐帧轨迹检测命中 Agent，并将命中结果送回原版伤害链路。

## 分层

1. `ContinuousCollisionMissionLogic`
   - 每个 Mission Tick 采样攻击者当前武器姿态。
   - 维护上一帧/当前帧 WeaponPose。
   - 使用 2D spatial hash 做 broad phase。
   - 对候选 Agent 执行 narrow phase。
   - 使用命中冷却避免同一挥击重复结算。

2. `ContinuousCollisionGeometry`
   - 武器端使用手部骨骼姿态 + `WeaponComponentData.WeaponLength` 建立可扫掠线段。
   - 防守方使用 `Monster.BodyCapsule*` / `CrouchedBodyCapsule*` 建立实际 Agent 身体胶囊。
   - 以线段间最短距离近似 swept segment-vs-capsule。

3. `ContinuousCombatBridge`
   - 生成 `AttackCollisionData`。
   - 调用 `MissionCombatMechanicsHelper.GetAttackCollisionResults`。
   - 调用 `AgentApplyDamageModel.CalculateDamage`。
   - 构建 `Blow` / `BlowWeaponRecord`。
   - 最终调用 `victim.RegisterBlow(...)`。
   - 禁止直接修改 `Agent.Health`。

4. `ContinuousCollisionBootstrap`
   - 通过现有 `SubModule` Harmony `PatchAll` 在 Mission 初始化后自动注入 MissionLogic。
   - 不修改原有大型 `SubModule.cs`。

## 当前限制

当前第一版的武器几何是“手部骨骼 + 武器长度”的近似，而不是读取武器实体真实刀刃/枪尖网格。
因此它已经是真正的连续轨迹检测，但还不是逐三角形精确武器碰撞。

当前 `AttackCollisionData` 使用公开 Debug 工厂构造，因为其完整构造函数是私有的；这允许复用原版碰撞后的伤害/护甲/反应计算，而无需重新实现整套战斗系统。

## 源码依据

实现对应 `Qika2-Source-Decompiled` 1.5.0 原版源码中的：

- `Monster.BodyCapsule*`
- `CapsuleData`
- `MBAgentVisuals.GetGlobalFrame`
- `MBAgentVisuals.GetBoneEntitialFrame`
- `MBAgentVisuals.GetRealBoneIndex`
- `AttackCollisionData.GetAttackCollisionDataForDebugPurpose`
- `AttackInformation`
- `MissionCombatMechanicsHelper.GetAttackCollisionResults`
- `Agent.RegisterBlow`
- `BlowWeaponRecord.FillAsMeleeBlow`
