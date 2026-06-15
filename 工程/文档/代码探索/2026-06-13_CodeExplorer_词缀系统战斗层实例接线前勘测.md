# 2026-06-13 CodeExplorer 词缀系统战斗层实例接线前勘测

## 调用原因
用户判断词缀系统数据结构重构已完成，但功能闭环未完成。需要补上战斗结算层（NewDamageModel）的实例接线，从模板回退改为实例优先查询。

## 探索目标
1. 确认 NewDamageModel.cs 中6处TODO的精确上下文
2. 确认 GetAffixDamageMultiplier 的两个重载签名
3. 确认 BindingMap / GetEquippedInstanceId 的可用性
4. 确认从 Agent 获取 Hero 和 EquipmentIndex 的路径

## 关键文件

| 文件 | 路径 | 行数 |
|------|------|------|
| NewDamageModel.cs | E:\SteamLibrary\...\New_ZZZF\工程\New_ZZZF\Systems\NewDamageModel.cs | 650 |
| AffixCampaignBehavior.cs | E:\SteamLibrary\...\New_ZZZF\工程\New_ZZZF\EquipmentAffixSystem\Campaign\AffixCampaignBehavior.cs | 626 |
| AffixBinding.cs | E:\SteamLibrary\...\New_ZZZF\工程\New_ZZZF\EquipmentAffixSystem\Data\AffixBinding.cs | 40 |
| AffixedItemRecord.cs | E:\SteamLibrary\...\New_ZZZF\工程\New_ZZZF\EquipmentAffixSystem\Data\AffixedItemRecord.cs | 41 |

## 6处TODO详情

所有TODO标记相同: `// TODO: 词缀实例分离 - 需要从 Agent 装备槽获取 instanceId，目前走模板回退`

### WOW_SandboxStrikeMagnitudeModel (行139-276)
| # | 行号 | 方法 | 当前调用 |
|---|------|------|---------|
| 1 | 168 | CalculateStrikeMagnitudeForMissile | `GetAffixDamageMultiplier(weapon.Item, "MissileDamage")` |
| 2 | 219 | CalculateStrikeMagnitudeForSwing | `GetAffixDamageMultiplier(item, "SwingDamage")` |
| 3 | 272 | CalculateStrikeMagnitudeForThrust | `GetAffixDamageMultiplier(item, "ThrustDamage")` |

### WOW_DefaultStrikeMagnitudeModel (行278-332)
| # | 行号 | 方法 | 当前调用 |
|---|------|------|---------|
| 4 | 307 | CalculateStrikeMagnitudeForMissile | `GetAffixDamageMultiplier(weapon.Item, "MissileDamage")` |
| 5 | 320 | CalculateStrikeMagnitudeForSwing | `GetAffixDamageMultiplier(missionWeapon.Item, "SwingDamage")` |
| 6 | 329 | CalculateStrikeMagnitudeForThrust | `GetAffixDamageMultiplier(missionWeapon.Item, "ThrustDamage")` |

## 可用接口

### GetEquippedInstanceId
```csharp
// AffixCampaignBehavior.cs 行311
public string? GetEquippedInstanceId(Hero hero, EquipmentIndex slotIndex)
```

### GetAffixDamageMultiplier 重载
```csharp
// 模板回退（已废弃）- 行357
[Obsolete] public static float GetAffixDamageMultiplier(ItemObject item, string statKey)

// 实例优先（推荐）- 行371
public static float GetAffixDamageMultiplier(string? instanceId, ItemObject item, string statKey)
```

## 可复用的分析结论

1. **BindingMap 已就绪**：Key格式 `"{OwnerId}:{SlotIndex}"`，Value为 `AffixBinding`
2. **GetEquippedInstanceId 已就绪**：通过 Hero + EquipmentIndex 查询
3. **重载2（实例版）已存在但无外部调用者**：需要将6处调用从重载1改为重载2
4. **核心难点**：战斗上下文中，如何从 Agent 获取对应 Hero 和装备的 EquipmentIndex

## 未解决的问题
- Sandbox 模式的 CalculateStrikeMagnitude 方法中，Agent 可以通过 `attackInformation` 参数获取，但 `EquipmentIndex` 的获取方式需要进一步确认
- Default 模式的方法签名更简单，可能无法直接获取 Agent
