# CodeExplorer 工作记录：物品生成入口追踪 & Modifier 接线

## 调用原因
同底材物品无法区分不同前后缀（多个同类型词缀物品在背包里堆叠到一起），需要追踪物品生成的所有入口，在入口处插入 `RegisterModifierBinding` + 唯一 `ItemModifier` 赋值。

## 探索目标
1. 找到所有物品获得/生成的入口点
2. 确认 `RegisterModifierBinding` 的所有调用位置
3. 理解 `ItemModifier` 的构造和注册机制
4. 确定接线方案

## 发现的文件与入口点

### 核心文件
| 文件 | 路径 |
|------|------|
| AffixCampaignBehavior.cs | `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\New_ZZZF\工程\New_ZZZF\EquipmentAffixSystem\Campaign\AffixCampaignBehavior.cs` |
| ItemModifier.cs (源码) | `c:\Users\42029\CodeBuddy\骑砍2源码\TaleWorlds.Core\ItemModifier.cs` |
| MBObjectBase.cs (源码) | `c:\Users\42029\CodeBuddy\骑砍2源码\TaleWorlds.ObjectSystem\MBObjectBase.cs` |
| MBObjectManager.cs (源码) | `c:\Users\42029\CodeBuddy\骑砍2源码\TaleWorlds.ObjectSystem\MBObjectManager.cs` |

### 物品获得入口
仅 **1 个事件入口**，无 Harmony Patch：

1. **`OnPlayerInventoryExchange`** (行 ~155-163)
   - 事件：`CampaignEvents.PlayerInventoryExchangeEvent`
   - 触发时机：购买、拾取、掉落获得
   - 调用 `CreateAffixedRecord(item, amount, "PlayerInventory")`
   - **这是唯一需要接线的地方**

2. **`ForceAffixItem`** — Debug/测试用，不接线

3. **无 Harmony Patch** — 词缀系统当前不使用任何 Harmony 补丁

### ItemModifier 构造机制 (源码分析)

```csharp
// ItemModifier 是 sealed class，继承 MBObjectBase
public sealed class ItemModifier : MBObjectBase
{
    public float PriceMultiplier { get; private set; }  // 私有 setter！
    
    public ItemModifier()  // 无参构造函数
    {
        this.Name = TextObject.GetEmpty();
    }
}
```

关键发现：
- `PriceMultiplier` 为 `private set`，无法直接赋值 → 需反射
- `MBObjectBase.AfterInitialized()` 是 **public** → 可调用
- `MBObjectManager.RegisterPresumedObject<T>()` 是 **public** → 可注册到对象管理器
- `TryRegisterObjectWithoutInitialization()` 是 **internal** → 不可直接调用
- 存档/读档流程：`BeforeLoad()` (LoadInitializationCallback) 自动重新注册

## 修改方案

### 修改文件
`AffixCampaignBehavior.cs`

### 具体改动

1. **添加 `using System.Reflection;`** — 用于反射设置 `PriceMultiplier`

2. **添加 `_isProcessingInventory` 递归锁** — 防止设置 ItemModifier 时触发二次 `OnPlayerInventoryExchange`

3. **改造 `OnPlayerInventoryExchange`**：
   - 添加递归锁
   - 调用 `CreateAffixedRecord` 后，若返回非 null，调用 `ApplyAffixModifier`

4. **新增 `ApplyAffixModifier` 方法**：
   - 创建 modifierId: `"zzzf_affix_" + InstanceId`
   - 调用 `RegisterModifierBinding(modifierId, instanceId)`
   - 创建 `ItemModifier`：new → 设 StringId → Initialize() → 反射设 PriceMultiplier=1.0 → RegisterPresumedObject
   - 调用 `element.EquipmentElement.SetModifier(modifier)`

5. **新增 `RebuildModifiers` 方法**：
   - 遍历 `ModifierToInstanceMap`，重建所有 ItemModifier 对象
   - 在 `OnGameLoaded` 中调用

6. **修改 `OnGameLoaded`** — 添加 `RebuildModifiers()` 调用

## 功能模块依赖关系
```
OnPlayerInventoryExchange
    → CreateAffixedRecord (生成词缀)
        → ApplyAffixModifier (生成唯一 ItemModifier)
            → RegisterModifierBinding (注册映射)
            → ItemModifier 创建 + MBObjectManager 注册
            → EquipmentElement.SetModifier (应用修饰符)

UI 层查询：
GetAffixForEquipmentElement(element)
    → element.ItemModifier.StringId → ModifierToInstanceMap → InstanceId → ItemRecordMap → AffixInstance
```

## 可复用的分析结论
1. Bannerlord 的物品堆叠依据是 `(Item.StringId, ItemModifier.StringId)` 的组合
2. 给每个词缀实例分配唯一 ItemModifier 可完美解决堆叠问题
3. `RegisterPresumedObject<T>` 是安全注册方式（已有同 ID 时返回已有对象）
4. ItemModifier 的 `SaveableClass` 属性保证其可被存档系统序列化

## 未解决的问题
- 若 MBObjectManager 在非预期状态下拒绝注册（如初始化未完成），需要 fallback
- 读档后 ItemRoster 中的 ItemModifier 引用是否能正确恢复（理论上 `BeforeLoad` 回调会处理，需实测验证）
