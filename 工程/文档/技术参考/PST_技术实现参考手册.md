# PST 技术实现参考手册

> **概要**：本文档是PST（英雄切换出战+战斗中库存管理）Mod的技术实现参考手册，虽已合入New_ZZZF但保留作为技术参考。涵盖核心功能（英雄切换出战、战斗中按K键打开完整物品库存、粒子特效浏览器）、技术栈（Harmony补丁/反射/自定义GameState/MVVM）、文件结构及职责、关键技术实现详解（Harmony补丁系统、自定义GameState绕过原版InventoryState、反射窃取私有字段、SPSkillVM的5000行ViewModel）。适合需要理解类似功能实现时参考。
> 
> 来源: `C:\Users\42029\CodeBuddy\旧版本更新代码\PST`
> 版本: v0.0.1
> 用途: 已合入 New_ZZZF，保留本文档作为技术实现参考

---

## 一、项目概览

| 项 | 值 |
|---|---|
| **Mod ID** | `PST` |
| **核心功能1** | 英雄切换出战 — 战斗前选择部队中的英雄代替主角出战 |
| **核心功能2** | 战斗中库存管理 — 按K键打开完整物品库存界面 |
| **附带功能** | 粒子特效浏览器 (L键) |
| **技术栈** | Harmony补丁 / 反射 / 自定义GameState / MVVM / UIExtenderEx(未启用) |
| **代码规模** | ~5个核心类，SPSkillVM约5000行 |

---

## 二、文件结构及职责

```
PST/
├── SubModule.xml
├── PST.sln
├── PST/
│   ├── SubModule.cs                     # 入口：Harmony注册 + MissionBehavior注入
│   ├── HeroChangeCampaignBehavior.cs    # 英雄切换（含3个子类）
│   ├── 新文件夹/
│   │   ├── SkillInventoryManager.cs     # 自定义库存管理器（反射窃取原版私有字段）
│   │   ├── SkillInventoryState.cs       # 自定义GameState（替代InventoryState）
│   │   ├── GauntletSkillScreen.cs       # 库存界面Screen（替代GauntletInventoryScreen）
│   │   ├── SPSkillVM.cs                 # 核心ViewModel (~5000行)
│   │   └── HarmonyLib.cs               # [已废弃] 旧的反射代码
└── GUI/Prefabs/ (空目录)
```

---

## 三、技术实现详解

### 3.1 Harmony 补丁系统

#### 注册方式
```csharp
// SubModule.cs
protected override void OnSubModuleLoad()
{
    base.OnSubModuleLoad();
    Harmony harmony = new Harmony("New_ZZZF");  // ID = "New_ZZZF"
    harmony.PatchAll();  // 自动扫描所有 [HarmonyPatch] 类
}
```

#### 补丁目标
```csharp
// HeroChangeCampaignBehavior.cs 内部类
[HarmonyPatch(typeof(GameMenuManager), "GetLeaveMenuOption")]
public class GameMenuManagerPatch
{
    // Prefix: 检查是否需要恢复原始主角
    static void Prefix(ref MenuCallbackArgs ___args)
    {
        if (HeroChangeCampaignBehavior.currecct != null
            && HeroChangeCampaignBehavior.currecct.PlayerCharacter != null)
        {
            Game.Current.PlayerTroop = HeroChangeCampaignBehavior.currecct.PlayerCharacter;
        }
    }
}
```

**关键模式**: 使用 `___args` 前缀（Harmony特殊命名约定）访问私有实例字段 `args`。

---

### 3.2 英雄切换系统

#### 3.2.1 游戏菜单中添加选项

```csharp
// 在 AddGameMenus 回调中为6个菜单添加 "ChooseHero" 选项
campaignGameStarter.AddGameMenuOption(
    "hideout_place",                    // menuId
    "ChooseHero",                       // optionId
    "{=xx}ChooseHero",                  // 显示文本
    new GameMenuOption.OnConditionDelegate(this.retTrue),
    new GameMenuOption.OnConsequenceDelegate(this.ChooseUseAgent),
    false, -1, false, null);

// 另外5个菜单: continue_siege_after_attack, encounter_interrupted,
//              army_encounter, encounter, join_encounter
```

**关键API**:
- `campaignGameStarter.AddGameMenuOption(menuId, optionId, text, condition, consequence, isLeave, index, isPatch, ...)`
- `GameMenuOption.OnConditionDelegate` — 需返回 `(MenuCallbackArgs, bool)`
- `GameMenuOption.OnConsequenceDelegate` — 处理选项点击

#### 3.2.2 弹出部队选择界面
```csharp
private void ChooseUseAgent(MenuCallbackArgs args)
{
    TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
    args.MenuContext.OpenTroopSelection(
        MobileParty.MainParty.MemberRoster,    // 源Roster
        troopRoster,                            // 接收选中的目标Roster
        CanChangeStatusOfTroop,                 // 筛选条件 (Func<CharacterObject, bool>)
        OnTroopRosterManageDone,                // 完成回调
        1, 1                                    // 最多/最少选择人数
    );
}
```

#### 3.2.3 替换玩家角色
```csharp
private void OnTroopRosterManageDone(TroopRoster selectedTroops)
{
    PlayerCharacter = Game.Current.PlayerTroop;              // 保存原角色
    ChooseHero = selectedTroops.GetManAtIndexFromFlattenedRosterWithFilter(0, true, false);
    Game.Current.PlayerTroop = ChooseHero;                   // 替换
}
```

**关键API**:
- `Game.Current.PlayerTroop` — 可读写的玩家角色属性
- `TroopRoster.GetManAtIndexFromFlattenedRosterWithFilter(index, selectedOnly, wounded)` — 获取选中角色

#### 3.2.4 战斗后恢复
```csharp
// HeroChangeMissionBehavior (MissionLogic 子类)
public override void OnEndMission()
{
    if (HeroChangeCampaignBehavior.currecct.PlayerCharacter != null)
    {
        Game.Current.PlayerTroop = HeroChangeCampaignBehavior.currecct.PlayerCharacter;
    }
}
```

**关键API**: `MissionBehavior.OnEndMission()` — 战斗/任务结束时自动调用

---

### 3.3 自定义库存系统

#### 3.3.1 替代原版 InventoryManager

**问题**: 要在战斗中打开库存界面，但不能用原版 `InventoryManager`（它依赖战役市场数据）。

**方案**: `SkillInventoryManager` 完全替代原版，通过反射同步状态。

```csharp
public static SkillInventoryManager Instance
{
    get
    {
        var original = Campaign.Current.InventoryManager;
        if (original != null)
        {
            // 通过反射窃取原版的私有字段
            var currentModeField = typeof(InventoryManager).GetField(
                "_currentMode", BindingFlags.NonPublic | BindingFlags.Instance);
            _currentMode = (InventoryMode)currentModeField.GetValue(original);

            var invLogicField = typeof(InventoryManager).GetField(
                "_inventoryLogic", BindingFlags.NonPublic | BindingFlags.Instance);
            _inventoryLogic = (InventoryLogic)invLogicField.GetValue(original);

            var extrasField = typeof(InventoryManager).GetField(
                "_doneLogicExtrasDelegate", BindingFlags.NonPublic | BindingFlags.Instance);
            _doneLogicExtrasDelegate = (Action)extrasField.GetValue(original);
        }
        return _instance;
    }
}
```

**关键技巧**:
- `typeof(InventoryManager).GetField(name, BindingFlags)` — 反射读取私有字段
- 从原版偷来的 `_inventoryLogic` 可复用其物品操作能力
- `_doneLogicExtrasDelegate` 在关闭界面时触发回调

#### 3.3.2 FakeMarketData — 假市场数据

当不在定居点附近时（如战斗中），提供假市场数据：
```csharp
public class FakeMarketData : IMarketData
{
    public int GetPrice(ItemObject item, ...)
    {
        return item.Value;  // 价格 = 基础价值，无市场波动
    }
    public int GetPrice(EquipmentElement item, ...) { return item.ItemValue; }
}
```

**使用场景**: `OpenScreenAsInventory` 时传入 `new FakeMarketData()`，让界面正常显示价格。

#### 3.3.3 自定义 GameState

```csharp
// SkillInventoryState.cs
[GameStateScreen(typeof(GauntletSkillScreen))]  // 绑定Screen
public class SkillInventoryState : PlayerGameState
{
    public override bool IsMenuState => true;    // 暂停游戏

    public InventoryLogic InventoryLogic;
    public IInventoryStateHandler Handler;

    protected override void OnActivate()
    {
        base.OnActivate();
        InitializeLogic(InventoryLogic);  // 初始化库存逻辑
    }
}
```

**关键模式**:
- 继承 `PlayerGameState` 不是 `GameState` — 保留玩家上下文
- `[GameStateScreen(typeof(X))]` 特性 — 声明Screen-状态绑定
- `IsMenuState = true` — 使游戏暂停（关键！）

#### 3.3.4 打开界面的完整流程

```csharp
// SkillInventoryManager.OpenScreenAsInventory()
public void OpenScreenAsInventory(PartyBase left, Settlement settlement)
{
    _doneLogicExtrasDelegate = null;
    _currentMode = InventoryMode.Default;
    InventoryLogic invLogic = new InventoryLogic(settlement, left, null, false);
    // ... 设置各种InventoryLogic参数 ...

    SkillInventoryState state = Game.Current.GameStateManager.CreateState<SkillInventoryState>();
    state.InventoryLogic = invLogic;
    state.Handler = handler;
    Game.Current.GameStateManager.PushState(state);
}
```

---

### 3.4 库存界面 Screen

#### 3.4.1 加载原版 UI 布局

```csharp
// GauntletSkillScreen.cs
protected override void OnInitialize()
{
    base.OnInitialize();
    _gauntletLayer = new GauntletLayer(100, "GauntletLayer");
    // 加载游戏原版 Inventory 电影剪辑！
    _movie = _gauntletLayer.LoadMovie("Inventory", ViewModel);
    // 但绑定的是自定义 SPSkillVM
    _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("InventoryHotKeyCategory"));
    ScreenManager.AddLayer(_gauntletLayer);
}
```

**关键技巧**: `LoadMovie("Inventory", ...)` — 复用游戏原版 Inventory 电影剪辑布局，只替换 ViewModel！

#### 3.4.2 快捷键处理

```csharp
// OnFrameTick 中处理：
if (_gauntletLayer.Input.IsHotKeyPressed("Confirm")) { ... }    // Enter
if (_gauntletLayer.Input.IsHotKeyReleased("Exit")) { ... }       // ESC
if (_gauntletLayer.Input.IsHotKeyPressed("SwitchToPreviousTab")) { ... }
if (_gauntletLayer.Input.IsHotKeyPressed("SwitchToNextTab")) { ... }
if (_gauntletLayer.Input.IsHotKeyPressed("TakeAll")) { ... }
if (_gauntletLayer.Input.IsHotKeyPressed("GiveAll")) { ... }
if (_gauntletLayer.Input.IsHotKeyPressed("CharacterSwitch")) { ... }
if (_gauntletLayer.Input.IsHotKeyPressed("EquipItem")) { ... }
if (_gauntletLayer.Input.IsHotKeyPressed("UnequipItem")) { ... }
```

**关键API**: 
- `GauntletLayer.Input.RegisterHotKeyCategory()` — 注册热键类别
- `GauntletLayer.Input.IsHotKeyPressed/Released()` — 检测快捷键

#### 3.4.3 FilterInventoryAtOpening 接口

```csharp
public void FilterInventoryAtOpening(InventoryLogic.InventoryCategoryType category)
{
    FilterByCategory(category);
}
```

**注意**: 该接口在新版骑砍2中已被移除，但 PST 中仍有实现。在 New_ZZZF 中保留为注释。

---

### 3.5 SPSkillVM 核心 ViewModel (~5000行)

#### 3.5.1 构造函数初始化
```csharp
public SPSkillVM(InventoryLogic inventoryLogic, bool isInCivilianModeByDefault,
    Func<WeaponComponentData, ItemObject.ItemUsageSetFlags> getItemUsageSetFlags,
    string fiveStackItemID, bool isInWarSet, ...)
{
    // 初始化所有装备槽位
    _headArmorSlot = new SPItemVM(...);
    _cloakSlot = new SPItemVM(...);
    _bodyArmorSlot = new SPItemVM(...);
    _gloveSlot = new SPItemVM(...);
    _bootSlot = new SPItemVM(...);
    _mountSlot = new SPItemVM(...);
    _mountArmorSlot = new SPItemVM(...);
    // 4个武器槽
    _weapon0Slot ~ _weapon3Slot
    _bannerSlot = new SPItemVM(...);

    // 初始化过滤器
    _filterButtons.Add("All", ...);
    _filterButtons.Add("Weapon", ...);
    _filterButtons.Add("Armor", ...);
    _filterButtons.Add("ShieldAndRanged", ...);
    _filterButtons.Add("Mount", ...);
    _filterButtons.Add("Misc", ...);

    // 初始化排序控制器
    _sortController = new SPInventorySortControllerVM();
}
```

#### 3.5.2 装备/卸装操作
```csharp
public void ProcessEquipItem(SPItemVM item, int index)
{
    _inventoryLogic.TransferEquipment(item, index);
    RefreshValues();
}

public void ProcessUnequipItem(SPItemVM targetItem, int index)
{
    _inventoryLogic.TransferEquipment(targetItem, null);
    RefreshValues();
}
```

#### 3.5.3 拖放系统
```csharp
public void ExecuteTransferWithParameters(
    SPItemVM fromItem, SPItemVM toItem, ...
    int targetIndex, TransferType transferType)
{
    // 处理各种拖放场景：
    // - 角色装备区 ↔ 玩家库存
    // - 玩家库存 ↔ 其他库存（交易/储物箱/战利品）
    // - 拖放到指定装备槽
    _inventoryLogic.TransferEquipment(...);
    _inventoryLogic.TransferItem(...);
}
```

#### 3.5.4 交易操作
```csharp
public void ExecuteBuyAllItems() { /* 全部购买 */ }
public void ExecuteSellAllItems() { /* 全部出售 */ }
public void ExecuteCompleteTranstactions()
{
    // 弹出确认对话框
    InformationManager.ShowInquiry(new InquiryData(
        title, text, true, true, "Confirm", "Cancel",
        onConfirm, onCancel));
}
public void ExecuteResetTranstactions() { /* 重置 */ }
public void ExecuteResetAndCompleteTranstactions() { /* 重置并完成 */ }
```

#### 3.5.5 武器比较系统
```csharp
// 当鼠标悬停在武器上时，自动显示与原装备的比较
_comparedItemList  // MBBindingList<SPItemVM> — 比较物品列表
ItemPreview        // 物品预览
```

---

### 3.6 粒子特效浏览器

```csharp
// SkillSystemBehavior (SubModule.cs 内部类)
// L 键触发
if (mission.MainAgent != null)
{
    // 遍历所有 Agent，解冻 AI
    foreach (Agent agent in mission.Agents)
        agent.SetIsAIPaused(false);

    // 在玩家前方10米处生成特效
    Vec3 pos = mission.MainAgent.Position + mission.MainAgent.LookDirection * 10;
    GameEntity entity = GameEntity.CreateEmpty(mission.Scene, true);
    entity.SetLocalPosition(pos);

    // 从硬编码列表中选择粒子名称
    string[] particleNames = { "psys_game_burning_agent", ... };  // ~200个
    entity.AddParticleSystemComponent(particleNames[_particleIndex]);
}
```

**关键API**:
- `GameEntity.CreateEmpty(Scene, isDynamic)` — 创建空实体
- `entity.AddParticleSystemComponent(name)` — 附加粒子效果
- `agent.SetIsAIPaused(bool)` — 冻结/解冻AI逻辑（调试用）

---

## 四、关键技术模式总结

| 模式 | 用途 | 关键API |
|------|------|---------|
| **Harmony Prefix补丁** | 拦截原版方法，在之前执行自定义逻辑 | `[HarmonyPatch(typeof(T), "Method")]` + `static void Prefix(...)` |
| **反射读取私有字段** | 复用原版InventoryManager的状态 | `typeof(T).GetField(name, BindingFlags.NonPublic\|Instance)` |
| **自定义GameState** | 创建独立游戏状态栈 | 继承 `PlayerGameState`，用 `[GameStateScreen]` 绑定 |
| **复用原版Movie** | 使用原版UI布局但替换VM | `GauntletLayer.LoadMovie("Inventory", customVM)` |
| **ImportName特性** | 绑定XML中的Widget | `[DataSourceProperty]` + `[ImportName("WidgetId")]` |
| **OpenTroopSelection** | 弹出部队选择界面 | `MenuContext.OpenTroopSelection(source, target, filter, callback, max, min)` |
| **Game.Current.PlayerTroop** | 切换玩家控制角色 | 直接赋值替换 |

---

## 五、已知问题 / 未完成

1. **UIExtenderEx 集成注释掉了** — 计划使用但未启用
2. **HarmonyLib.cs 废弃** — 旧反射代码已注释，逻辑移到 SkillInventoryManager
3. **粒子特效浏览器** — 硬编码列表，仅调试用
4. **FilterInventoryAtOpening** — 新版骑砍2中此接口已移除
