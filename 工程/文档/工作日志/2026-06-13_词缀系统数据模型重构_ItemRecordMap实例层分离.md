# 2026-06-13 词缀系统数据模型重构 — ItemRecordMap 实例层分离

## 任务描述

将词缀系统从"模板层绑定"重构为"实例层绑定"：
- 旧模型：`ItemAffixMap` Key=InstanceId，但通过 `GetAffixByItemId(item.StringId)` 按模板ID查找，导致同模板物品共享词缀
- 新模型：`ItemRecordMap` Key=InstanceId，增加 `AffixedItemRecord` 包装类，每个生成实例独立记录

## 修改文件列表

| 文件 | 操作 | 说明 |
|------|------|------|
| `Data/AffixedItemRecord.cs` | **新建** | 运行时物品实例记录包装类 |
| `Campaign/AffixCampaignBehavior.cs` | 重构 | 核心行为类全面改写 |
| `UIPatches/AffixUIPatches.cs` | 修改 | GetAffixByItemId → GetAffixByBaseItemId (×2) |
| `Debug/AffixDebugHelper.cs` | 修改 | ItemAffixMap→ItemRecordMap + GetAffixByItemId→GetAffixByBaseItemId (×7) |
| `Systems/NewDamageModel.cs` | **无改动** | GetAffixDamageMultiplier签名未变，内部实现已自动走新路径 |

## 关键改动详解

### 1. 新增 AffixedItemRecord（数据层）

```csharp
public sealed class AffixedItemRecord
{
    [SaveableProperty(1)] public string InstanceId;
    [SaveableProperty(2)] public string BaseItemId;
    [SaveableProperty(3)] public string Source;
    [SaveableProperty(4)] public int StackCount;
    [SaveableProperty(5)] public AffixInstance Affix;
    [SaveableProperty(6)] public bool IsEquipped;
}
```

**位置**: `EquipmentAffixSystem/Data/AffixedItemRecord.cs`

### 2. AffixCampaignBehavior 核心变更

| 变更项 | 旧 | 新 |
|--------|----|----|
| 主字典 | `Dictionary<string, AffixInstance> ItemAffixMap` | `Dictionary<string, AffixedItemRecord> ItemRecordMap` |
| 生成方法 | `AffixItemIfNeeded`（检查同BaseItemId去重） | `CreateAffixedRecord`（每次调用创建新实例，不去重） |
| 强制生成 | `ForceAffixItem` 返回 `AffixInstance` | 返回 `AffixedItemRecord` |
| 按模板查 | `GetAffixByItemId(string itemId)` | **删除** |
| 按实例查 | `GetAffixByInstanceId` 返回 `AffixInstance` | 返回 `record.Affix` |
| 过渡方法 | — | **新增** `GetAffixByBaseItemId(string)` 公开 |
| 序列化 | `SerializeAffixMap` / `DeserializeAffixMap` | `SerializeRecordMap` / `DeserializeRecordMap`（v2格式10字段） |

### 3. 存档格式变更

**旧格式 (v1)**: `7字段`
```
InstanceId|BaseItemId|ItemLevel|Rarity|prefixIds|suffixIds|statModifiers
```

**新格式 (v2)**: `10字段`（向后兼容旧存档）
```
InstanceId|BaseItemId|ItemLevel|Rarity|Source|StackCount|IsEquipped|prefixIds|suffixIds|statModifiers
```

反序列化时自动检测字段数：`parts.Length >= 10` → v2格式，否则按v1兼容解析。

### 4. 过渡策略

- `GetAffixByBaseItemId(string)` 公开方法 — 用于仅有 ItemObject 的场景（UI/战斗/调试）
- 标记为"过渡方法"，未来逐步迁移到按 InstanceId 查询
- `CreateAffixedRecord` 不再检查同 BaseItemId 去重，允许同一模板多个实例独立记录

## 数据结构层次

```
AffixCampaignBehavior
  └── ItemRecordMap: Dictionary<string, AffixedItemRecord>
        ├── Key: InstanceId (Guid.N)
        └── Value: AffixedItemRecord
              ├── InstanceId, BaseItemId, StackCount, Source, IsEquipped
              └── Affix: AffixInstance
                    ├── PrefixIds, SuffixIds
                    ├── FinalStatModifiers
                    ├── Rarity, ItemLevel
                    └── _cachedPrefixDefs / _cachedSuffixDefs [NonSerialized]
```

## 受影响的外部调用者

| 调用者 | 方法 | 状态 |
|--------|------|------|
| AffixUIPatches (ItemDescription) | `GetAffixByBaseItemId` | ✅ 已更新 |
| AffixUIPatches (ItemPreview) | `GetAffixByBaseItemId` | ✅ 已更新 |
| NewDamageModel (×6处) | `GetAffixDamageMultiplier` (内部走BaseItemId搜索) | ✅ 无改动 |
| AffixDebugHelper (Give/Lists/Reroll) | `GetAffixByBaseItemId` + `ForceAffixItem` | ✅ 已更新 |

## 已知限制

- 战斗系统（NewDamageModel）仍按模板查找首个词缀，同模板不同实例在战斗中共享词缀效果
- 需要在后续添加 Agent装备槽 → InstanceId 映射来彻底解决此问题
