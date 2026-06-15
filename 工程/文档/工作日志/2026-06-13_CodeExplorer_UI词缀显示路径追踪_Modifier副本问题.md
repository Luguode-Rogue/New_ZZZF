# CodeExplorer 工作记录：UI 词缀显示路径追踪 & Modifier 副本问题

## 调用原因
用户反馈：同底材两个装备在背包里显示相同词缀名。"在 OnPlayerInventoryExchange 中设置 ItemModifier"的修复无效。

## 探索目标
1. 追踪背包 UI 显示词缀名的完整调用链
2. 确定为何 `GetAffixForEquipmentElement` 走不到 ItemModifier 桥接
3. 定位根因

## 发现的文件和关键代码

### 核心文件
| 文件 | 路径 |
|------|------|
| AffixUIPatches.cs | `New_ZZZF\工程\New_ZZZF\EquipmentAffixSystem\UIPatches\AffixUIPatches.cs` |
| AffixCampaignBehavior.cs | `New_ZZZF\工程\New_ZZZF\EquipmentAffixSystem\Campaign\AffixCampaignBehavior.cs` |
| ItemVM.cs (源码) | `骑砍2源码\TaleWorlds.Core.ViewModelCollection\ItemVM.cs` |
| InventoryLogic.cs (源码) | `骑砍2源码\TaleWorlds.CampaignSystem\Inventory\InventoryLogic.cs` |
| ItemRoster.cs (源码) | `骑砍2源码\TaleWorlds.CampaignSystem\Roster\ItemRoster.cs` |

### UI 显示路径（完整调用链）

```
背包 UI 渲染
  → Harmony Postfix: AffixItemDescriptionGetterPatch (AffixUIPatches.cs:16-35)
    → __instance.ItemRosterElement.EquipmentElement (ItemVM.cs:514, public field)
    → GetAffixForEquipmentElement(element)
      ├── 优先级1: element.ItemModifier != null → ModifierToInstanceMap → GetAffixByInstanceId
      └── 优先级2 (回退): GetAffixByBaseItemId → 第一个 BaseItemId 匹配的记录
    → affix.BuildFullName(baseItemName)
```

### 🔴 根因：`GetTransferredItems` 创建临时副本

**文件**: `InventoryLogic.cs`, 行 1381-1393

```cs
internal List<ValueTuple<ItemRosterElement, int>> GetTransferredItems(bool isSelling)
{
    List<ValueTuple<ItemRosterElement, int>> list = new List<...>();
    foreach (KeyValuePair<EquipmentElement, ItemLog> kv in _transactionLogs)
    {
        if (kv.Value.Count > 0 && !kv.Value.IsSelling == isSelling)
        {
            int item = kv.Value.Sum();
            // ★ 创建了全新的 ItemRosterElement，不是玩家背包中的实际元素！
            list.Add(new ValueTuple<ItemRosterElement, int>(
                new ItemRosterElement(kv.Value.Count, kv.Key.ItemModifier), item));
        }
    }
    return list;
}
```

**流程**:
1. `InventoryLogic.FinalizeTransaction()` 在交易完成后触发 `OnPlayerInventoryExchange` (L411)
2. `_transactionHistory.GetBoughtItems()` 调用 `GetTransferredItems(true)`
3. `GetTransferredItems` 为每个交易项**创建全新的 `ItemRosterElement` 临时对象**
4. `OnPlayerInventoryExchange` 收到的 `purchasedItems` 都是临时副本
5. 我们的 `ApplyAffixModifier` 修改了临时副本的 `EquipmentElement` → **对玩家实际背包无任何影响**
6. 玩家背包中的实际 `ItemRosterElement` 的 `ItemModifier` 始终为 `null`
7. UI 读取到 `ItemModifier == null` → `GetAffixForEquipmentElement` 走优先级2（模板回退）
8. `GetAffixByBaseItemId` 返回 ItemRecordMap 中第一个匹配的记录 → 所有同底材物品显示相同词缀

### ItemModifier 对象注册（这部分正确）

- `ItemRosterElement` 是 class，`this._data[index]` 返回引用（ItemRoster.cs:39-44）
- `roster[i].EquipmentElement.SetModifier(mod)` 可正确修改实际背包元素
- `ItemVM.ItemRosterElement` 是 public field（ItemVM.cs:514），引用的是实际背包元素

### Harmony Patch 汇总

| # | 目标 | 方法 | 文件 | 与背包UI |
|---|------|------|------|----------|
| 1 | ItemVM | get_ItemDescription | AffixUIPatches.cs:16 | ✅ 主入口 |
| 2 | ItemPreviewVM | Open | AffixUIPatches.cs:41 | ✅ 详情面板 |
| 3 | ItemPreviewVM | Open | Harmonys/Harmony.cs:194 | ⚠️ 冲突（Prefix returns false） |

## 修复方案

### 修改1：`OnPlayerInventoryExchange`
- 不再通过 `purchasedItems` 设置 Modifier（临时副本）
- 改为：先创建词缀记录 → 再调用 `SyncAffixModifiersToPlayerRoster` 同步到实际背包

### 修改2：新增 `SyncAffixModifiersToPlayerRoster`
- 遍历 `MobileParty.MainParty.ItemRoster`（**实际背包**）
- 对于 `ItemModifier == null` 的元素，在 `ItemRecordMap` 中查找**尚未分配修饰符**的记录
- 配对：BaseItemId 匹配 + record.InstanceId 不在 ModifierToInstanceMap 的 values 中
- 调用 `ApplyAffixModifier` 设置到实际元素上

### 修改3：`OnGameLoaded` 增加调用
- 读档后调用 `SyncAffixModifiersToPlayerRoster` → 修复旧存档中未设 Modifier 的物品

### 关键改进
- **之前**: 修改临时副本 → 无效
- **之后**: `roster[i].EquipmentElement.SetModifier(mod)` → 修改实际背包元素（class 引用 + struct field 直接访问）

## 未解决的问题
- `PlayerInventoryExchangeEvent` 只在交易界面关闭时触发，不覆盖战后拾取等场景
- 可能需要额外的事件覆盖（如 `OnPlayerBattleEnd`）来确保所有物品获得场景都被处理
