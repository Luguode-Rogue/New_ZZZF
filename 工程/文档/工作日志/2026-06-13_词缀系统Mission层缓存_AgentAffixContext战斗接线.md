# 2026-06-13 词缀系统 Mission 层缓存 + 战斗接线

## 任务描述

补齐词缀系统功能闭环的"战斗结算层"：建立 Mission 层 `Agent → EquipmentSlot → InstanceId` 缓存，替换 NewDamageModel 中 6 处模板回退调用。

## 背景（修复前状态）

在本次改动之前：
- `BindEquipment()` / `UnbindEquipment()` / `GetEquippedInstanceId()` 三个方法**零调用者**（已定义但从未被调用）
- NewDamageModel 中 6 处 `GetAffixDamageMultiplier(ItemObject, statKey)` 全部走模板回退
- 没有 Mission 层词缀缓存，没有装备变化事件监听
- 数据结构重构已完成，但功能闭环未完成

## 修改文件列表

| 文件 | 操作 | 说明 |
|------|------|------|
| `EquipmentAffixSystem/Mission/AgentAffixContext.cs` | **新建** | Mission层Agent装备槽→InstanceId缓存数据类 |
| `EquipmentAffixSystem/Mission/AffixMissionBehavior.cs` | **新建** | MissionLogic，在Agent创建时填充缓存，提供静态查询接口 |
| `SubModule.cs` | 修改 | 注册 `AffixMissionBehavior` 到 `OnMissionBehaviorInitialize` |
| `Systems/NewDamageModel.cs` | 修改 | 6处调用改为从Mission缓存读取InstanceId |

## 关键改动详解

### 1. AgentAffixContext（数据类）

```csharp
// EquipmentAffixSystem/Mission/AgentAffixContext.cs
public sealed class AgentAffixContext
{
    public readonly Dictionary<EquipmentIndex, string> SlotToInstanceId
        = new Dictionary<EquipmentIndex, string>();

    public string? GetInstanceId(EquipmentIndex slot)
    {
        return SlotToInstanceId.TryGetValue(slot, out var id) ? id : null;
    }
}
```

### 2. AffixMissionBehavior（MissionLogic）

```csharp
// EquipmentAffixSystem/Mission/AffixMissionBehavior.cs
public class AffixMissionBehavior : MissionLogic
```

**核心生命周期：**

| 阶段 | 行为 |
|------|------|
| `OnAgentCreated` | Hero→遍历4个武器槽→BindingMap精确查→ItemRecordMap模板回退→写入缓存 |
| `OnAgentRemoved` | 清理该Agent的缓存条目 |
| `OnEndMission` | 清空全部缓存 |

**静态查询接口：**
```csharp
public static string? GetAgentWeaponInstanceId(Agent? agent, EquipmentIndex slot)
```

设计要点：
- 使用 `agent.SpawnEquipment[slot]` 获取初始装备
- 非 Hero Agent（强盗/NPC）注册空上下文 → 查询返回 null → 模板回退
- 使用 `lock(_agentAffixCache)` 保证线程安全

### 3. SubModule 注册

```csharp
// SubModule.cs OnMissionBehaviorInitialize
+ mission.AddMissionBehavior(new AffixMissionBehavior());
```

### 4. NewDamageModel 6处改动

| # | 类 | 方法 | 伤害类型 | 改动行 |
|---|-----|------|---------|--------|
| 1 | Sandbox | CalculateStrikeMagnitudeForMissile | MissileDamage | 168-170 |
| 2 | Sandbox | CalculateStrikeMagnitudeForSwing | SwingDamage | 220-222 |
| 3 | Sandbox | CalculateStrikeMagnitudeForThrust | ThrustDamage | 274-276 |
| 4 | Default | CalculateStrikeMagnitudeForMissile | MissileDamage | 310-312 |
| 5 | Default | CalculateStrikeMagnitudeForSwing | SwingDamage | 324-326 |
| 6 | Default | CalculateStrikeMagnitudeForThrust | ThrustDamage | 335-337 |

**改动模式（所有6处一致）：**
```csharp
// 旧（模板回退 - 已废弃）
result *= AffixCampaignBehavior.GetAffixDamageMultiplier(weapon.Item, "MissileDamage");

// 新（实例优先 - 从Mission缓存查InstanceId）
string? affixInstId = AffixMissionBehavior.GetAgentWeaponInstanceId(
    attackInformation.AttackerAgent, collisionData.AffectorWeaponSlotOrMissileIndex);
result *= AffixCampaignBehavior.GetAffixDamageMultiplier(affixInstId, weapon.Item, "MissileDamage");
```

**降级保障**：`GetAffixDamageMultiplier(string?, ItemObject, string)` 在 InstanceId 为 null 时**自动回退模板查找**，不会崩溃。

## 架构层次（改动后）

```
Campaign 层
  AffixCampaignBehavior
    ├── ItemRecordMap: InstanceId → AffixedItemRecord
    ├── BindingMap: "HeroId:SlotIndex" → AffixBinding  (待接线)
    └── 存档/读档

Mission 层  ← ★ 本次新增
  AffixMissionBehavior
    └── _agentAffixCache: Agent.Index → AgentAffixContext
          └── SlotToInstanceId: EquipmentIndex → InstanceId

DamageModel 层  ← ★ 本次修复
  NewDamageModel
    ├── attackInformation.AttackerAgent → Agent
    ├── AffixMissionBehavior.GetAgentWeaponInstanceId() → InstanceId
    └── AffixCampaignBehavior.GetAffixDamageMultiplier(instanceId, ...) → 倍率
```

## 已知限制（本次未解决）

| 限制 | 说明 | 优先级 |
|------|------|--------|
| BindEquipment 仍无调用者 | 无装备变化事件监听，BindingMap 写入链缺失 | 高 - 下一步 |
| Mission缓存仅初始化快照 | 战斗中换装/拾取不刷新缓存 | 中 |
| collisionData 索引语义 | 导弹场景可能非严格 EquipmentIndex | 低 - 有降级 |
| 非Hero Agent | 绑匪/竞技场NPC等无词缀绑定 | 低 - 设计如此 |

## 下一步

1. **装备变化事件接线**：在 `AffixCampaignBehavior.RegisterEvents()` 中监听 `CampaignEvents.EquipmentChanged` 或 `PlayerInventoryExchangeEvent`（已有），自动调用 BindEquipment/UnbindEquipment
2. **物品生成入口接线**：掉落/商店/任务奖励时调用 `CreateAffixedRecord` 并传 InstanceId
3. **UI 层实例优先**：AffixUIPatches 从模板查询改为实例查询
