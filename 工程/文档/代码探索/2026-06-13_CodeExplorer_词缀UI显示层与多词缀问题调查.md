# 2026-06-13 CodeExplorer：词缀UI显示层与多词缀问题调查

## 调用原因
用户报告两个问题：
1. 同底材装备在UI上显示相同的前后缀名词（可能是模板回退）
2. 装备有多个前后缀时，只显示一个

需要调查显示层代码和数据流确定根因。

## 探索目标
- 所有与词缀显示相关的 UI Harmony Patch 代码
- `GetAffixByBaseItemId` 的所有调用位置
- `AffixInstance.BuildFullName` 的命名逻辑
- 数据层是否支持多词缀
- `AffixTooltipVM` 的显示逻辑

## 发现的文件和目录结构

### 数据层
| 文件 | 路径 |
|------|------|
| AffixDefinition.cs | EquipmentAffixSystem/Data/ |
| AffixInstance.cs | EquipmentAffixSystem/Data/ |
| AffixedItemRecord.cs | EquipmentAffixSystem/Data/ |
| AffixDatabase.cs | EquipmentAffixSystem/Data/ |
| AffixBinding.cs | EquipmentAffixSystem/Data/ |
| AffixGenerator.cs | EquipmentAffixSystem/Logic/ |

### 显示层
| 文件 | 路径 |
|------|------|
| AffixUIPatches.cs | EquipmentAffixSystem/UIPatches/ |
| AffixTooltipVM.cs | EquipmentAffixSystem/GUI/ |

### 行为层
| 文件 | 路径 |
|------|------|
| AffixCampaignBehavior.cs | EquipmentAffixSystem/Campaign/ |
| AffixMissionBehavior.cs | EquipmentAffixSystem/Mission/ |
| AgentAffixContext.cs | EquipmentAffixSystem/Mission/ |

## 关键代码片段位置

### 问题1根因：GetAffixByBaseItemId 模板回退

**定义位置**：`AffixCampaignBehavior.cs` 第253-267行
```csharp
[Obsolete]
public AffixInstance? GetAffixByBaseItemId(string baseItemId)
{
    foreach (var record in ItemRecordMap.Values)
    {
        if (record.BaseItemId == baseItemId)
            return record.Affix;  // 只返回第一个匹配！
    }
    return null;
}
```

**UI调用1**：`AffixUIPatches.cs` 第29行 - 背包物品名称显示
```csharp
var affix = behavior.GetAffixByBaseItemId(item.StringId);
```

**UI调用2**：`AffixUIPatches.cs` 第51行 - 右侧详情面板
```csharp
var affix = behavior.GetAffixByBaseItemId(item.Item.StringId);
```

**其他调用**：
- `AffixCampaignBehavior.cs` 第327行 - GetItemDisplayName(ItemObject) 模板版本
- `AffixCampaignBehavior.cs` 第360行 - GetAffixDamageMultiplier(ItemObject) 模板版本
- `AffixMissionBehavior.cs` 第137行 - ResolveInstanceId 回退
- `AffixDebugHelper.cs` 第151行 - 调试输出

### 正确替代方案已存在
- `GetAffixByInstanceId(string instanceId)` - AffixCampaignBehavior.cs 第246-251行
- `GetItemDisplayName(string instanceId)` - AffixCampaignBehavior.cs 第337-350行

### 问题2根因：BuildFullName 只取 [0]

**位置**：`AffixInstance.cs` 第92-122行
```csharp
public string BuildFullName(string baseItemName)
{
    // 取第一个（主要）前后缀的显示名   ← 第102行注释
    string prefixName = hasPrefix ? prefixes[0].DisplayName : null;  // 第103行
    string suffixName = hasSuffix ? suffixes[0].DisplayName : null;  // 第104行
}
```

### 数据结构完全支持多词缀
- `AffixInstance.cs` 第30-35行：`PrefixIds`/`SuffixIds` 是 `List<string>`
- `AffixGenerator.cs` 第98-99行：Rare品质生成1-3个前后缀
- `AffixTooltipVM.cs` 第155-162行：Tooltip用 `string.Join` 正确显示全部

### Harmony.cs 冲突分析
- `Harmony.cs` 第194-223行：`ItemPreviewVMPatch` Prefix 返回 false
- 与 `AffixItemPreviewPatch` Postfix 不冲突，Postfix 仍会执行

## 功能模块间的依赖关系

```
物品生成 → AffixGenerator.RollAffixes() → AffixInstance (多词缀)
                                          ↓
                                    AffixedItemRecord (ItemRecordMap)
                                          ↓
UI层:                                       战斗层:
ItemVM → Harmony Patch                       Agent → AffixMissionBehavior → AgentAffixContext
  ↓ GetAffixByBaseItemId (❌模板回退)          ↓ GetAgentWeaponInstanceId
  ↓                                            ↓
ItemPreviewVM → Harmony Patch                NewDamageModel → GetAffixDamageMultiplier(instanceId, ...)
  ↓ GetAffixByBaseItemId (❌模板回退)
```

## 可复用的分析结论

1. **问题1**：UI层因 `EquipmentElement` 没有 InstanceId 字段，只能走 `GetAffixByBaseItemId` 模板回退。需要桥接方案（ItemModifier 或额外映射表）。

2. **问题2**：`BuildFullName` 故意只取一个词缀（暗黑2命名风格），数据层和Tooltip层都是正确的。只需修改 BuildFullName 即可。

3. **已有正确替代**：`GetAffixByInstanceId` 和 `GetItemDisplayName(instanceId)` 已就位，只是 UI 层调用不到它们。

## 未解决的问题

1. UI层如何获取 InstanceId —— 需要 ItemModifier 桥接或其他方案
2. 方案A（ItemModifier桥接） vs 方案B（额外映射表）的取舍
