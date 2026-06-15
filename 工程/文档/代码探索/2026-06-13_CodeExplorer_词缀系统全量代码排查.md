# 2026-06-13 CodeExplorer: 词缀系统全量代码排查

## 调用原因
用户要求实施"物品实例词缀"架构重构，将词缀从 ItemObject 模板层移至 AffixedItemRecord 实例层。

## 探索目标
1. 找到所有词缀相关 .cs 文件的完整路径和结构
2. 确认 `GetAffixByItemId` / `ItemAffixMap` 的当前使用方式
3. 确认 `AffixInstance` 当前定义
4. 确认是否已有 `AffixedItemRecord`
5. 找到所有调用 `GetAffixByItemId` 的地方

## 探索范围
- `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\New_ZZZF\工程\New_ZZZF\EquipmentAffixSystem\`

---

## 发现的文件列表（共10个 .cs 文件）

### 核心目录 (8个)
| # | 文件 | 行数 |
|---|------|------|
| 1 | `EquipmentAffixSystem\Campaign\AffixCampaignBehavior.cs` | ~430 |
| 2 | `EquipmentAffixSystem\Data\AffixInstance.cs` | ~139 |
| 3 | `EquipmentAffixSystem\Data\AffixDatabase.cs` | - |
| 4 | `EquipmentAffixSystem\Data\AffixDefinition.cs` | - |
| 5 | `EquipmentAffixSystem\Logic\AffixGenerator.cs` | - |
| 6 | `EquipmentAffixSystem\GUI\AffixTooltipVM.cs` | - |
| 7 | `EquipmentAffixSystem\UIPatches\AffixUIPatches.cs` | - |
| 8 | `EquipmentAffixSystem\Debug\AffixDebugHelper.cs` | - |

### 外部引用 (2个)
| 9 | `SubModule.cs` |
| 10 | `Systems\NewDamageModel.cs` |

---

## 关键发现

### 1. `ItemAffixMap` 的 Key 已经是 InstanceId
`AffixCampaignBehavior.cs` 第31行：
```csharp
Dictionary<string, AffixInstance> ItemAffixMap  // Key=InstanceId (已部分修复)
```

### 2. `AffixInstance` 已有 InstanceId + BaseItemId
```csharp
[SaveableProperty(1)] string InstanceId { get; set; }
[SaveableProperty(2)] string BaseItemId { get; set; }
```
还包含 PrefixIds、SuffixIds、FinalStatModifiers、Rarity 等。

### 3. 仍存在 `GetAffixByItemId(string itemId)` 方法
按 ItemId（模板）查找，需删除/替换为 `GetAffixByInstanceId`。

### 4. 没有 `AffixedItemRecord` 类
需新建该类：包装 InstanceId + BaseItemId + StackCount + Source + IsEquipped + AffixInstance。

### 5. `AffixItemIfNeeded(ItemObject, string)` 仍用模板参数
需改为 `CreateAffixedRecord(ItemObject, int stackCount, string source)` 返回 AffixedItemRecord。

---

## 需修改的文件清单

| 文件 | 改动 |
|------|------|
| 新建 `AffixedItemRecord.cs` | 新建实例记录类 |
| `AffixCampaignBehavior.cs` | 删除 `GetAffixByItemId`；改 `AffixItemIfNeeded`→`CreateAffixedRecord`；词典改为 `ItemRecordMap` |
| `AffixUIPatches.cs` | UI 显示改用 InstanceId |
| `NewDamageModel.cs` | 战斗效果改用 InstanceId |

## 未解决问题
需读取完整文件内容后确定所有 `GetAffixByItemId` 调用点。
