# New_ZZZF 技能界面完整参考手册

> **概要**：本文档是CustomSkillScreen技能配置界面的完整技术参考，整合自6份工作记录。覆盖全部14个相关文件（C#源码6个、XML布局2个、数据层2个、参考旧版2个）、架构与类继承关系、完整工作流程（打开界面→选英雄→点槽位→弹窗选择→应用保存）、双Movie管理机制、常见问题排查。是理解和维护技能界面系统的核心参考文档。
> 
> 整合自6份工作记录，覆盖从编译修复到功能实现的完整历程。
> 原始记录日期：2025-06-05 ~ 2026-06-06

---

## 一、系统概述

CustomSkillScreen 是 New_ZZZF 模组的**技能配置界面**，基于骑砍2旧版物品界面（InventoryLogic体系）改造而成。核心流程：打开界面 → 左侧选英雄 → 点击右侧技能槽 → 弹出技能选择窗口 → 搜索/选择技能 → 应用更改保存。

---

## 二、全部文件清单（14个文件）

### 2.1 C# 源码（6个）

| # | 文件 | 路径 | 职责 |
|---|------|------|------|
| 1 | `CustomSkillScreen.cs` (261行) | `New_ZZZF/工程/New_ZZZF/GUI/` | Screen层：GauntletLayer管理、Movie加载/释放、键盘输入检测、弹窗生命周期 |
| 2 | `CustomSkillScreenVM.cs` (755行) | 同上 | 主ViewModel：英雄列表(Roster)、技能槽(Skills)、英雄切换、槽位点击→弹窗、应用/撤销 |
| 3 | `SkillSelectionVM.cs` (319行) | 同上 | 弹窗ViewModel：技能列表(FilteredSkills)、搜索过滤、选择/关闭、键盘导航(↑↓Enter) |
| 4 | `SkillItemVM.cs` (242行) | 同上 | 弹窗ItemTemplate ViewModel：单个技能项的属性绑定 |
| 5 | `SkillModel.cs` (291行) | 同上 | 数据模型：SkillUIData、HeroSkillData、SkillCatalog、SkillDiffInfo |
| 6 | `SkillDebug.cs` (48行) | 同上 | 统一日志：游戏内显示 + 写入文件 SkillDebug.log |

### 2.2 XML 布局（2个）

| # | 文件 | 路径 | 职责 |
|---|------|------|------|
| 7 | `CustomSkillScreen.xml` (306行) | `New_ZZZF/GUI/Prefabs/` | 主界面：英雄列表(左侧) + 技能槽网格(右侧) + 应用/撤销按钮 |
| 8 | `SkillSelectionPopup.xml` (209行) | 同上 | 弹窗：搜索框 + 技能列表(纵向滚动) + 确认/取消按钮 |

### 2.3 数据层（2个）

| # | 文件 | 路径 | 职责 |
|---|------|------|------|
| 9 | `SkillConfigManager.cs` (247行) | `New_ZZZF/工程/New_ZZZF/Systems/` | 技能配置持久化：XML加载/保存、SkillSet ↔ troopId 映射 |
| 10 | `SPSkillVM.cs` (5828行) | `New_ZZZF/工程/New_ZZZF/GUI/` | **旧版**技能界面ViewModel（基于InventoryLogic体系，仅供参考） |

### 2.4 参考/旧版（2个）

| # | 文件 | 职责 |
|---|------|------|
| 11 | `GauntletSkillScreen.cs` (New_ZZZF) | 旧版Screen（基于SkillInventoryState/InventoryLogic） |
| 12 | `GauntletSkillScreen.cs` (PST) | PST版本参考 |

---

## 三、架构与类继承关系

```
ScreenBase
  └── CustomSkillScreen           ← Screen层（入口）
        ├── 持有: GauntletLayer _gauntletLayer
        ├── 持有: GauntletMovieIdentifier _movie (主界面)
        ├── 持有: GauntletMovieIdentifier _popupMovie (弹窗)
        └── 持有: CustomSkillScreenVM _dataSource

ViewModel (基类)
  ├── HeroVM                      ← 英雄列表项
  │     ├── string HeroId / HeroName
  │     ├── bool IsSelected
  │     └── ExecuteSelect()
  │
  ├── SkillSlotVM                 ← 技能槽位
  │     ├── string SlotId / SlotLabel / SkillName / SkillIcon
  │     ├── bool IsEmpty
  │     ├── string CooldownText / CostText
  │     ├── SPSkillType SlotFilterType
  │     ├── SkillUIData Skill (非绑定)
  │     ├── SetSkill(SkillUIData)
  │     └── ExecuteClick()
  │
  ├── CustomSkillScreenVM         ← 主界面ViewModel
  │     ├── MBBindingList<HeroVM> Roster
  │     ├── MBBindingList<SkillSlotVM> Skills
  │     ├── HeroVM CurrentHero
  │     ├── bool IsDirty
  │     ├── SkillSelectionVM SkillSelectionPopup  ← 弹窗VM引用
  │     ├── SkillCatalog Catalog
  │     ├── PopulateRoster() → 从 Clan.PlayerClan
  │     ├── SelectHero() / SelectNextHero() / SelectPrevHero()
  │     ├── OnSlotClicked() → 创建SkillSelectionVM
  │     ├── AssignSkillToSlot()
  │     ├── ExecuteApply() / ExecuteUndoChanges()
  │     ├── SelectSlotByIndex(int) → 键盘1~8
  │     └── IsPopupOpen / PopupSelectNextSkill() / Prev / Current
  │
  ├── SkillSelectionVM            ← 弹窗ViewModel
  │     ├── MBBindingList<SkillItemVM> FilteredSkills
  │     ├── string SearchText / Title
  │     ├── bool IsVisible
  │     ├── int SelectedIndex (键盘导航)
  │     ├── FilterSkills() / ResetFilteredList()
  │     ├── ExecuteSelectSkill() / ExecuteClose()
  │     ├── ExecuteSelectCurrentSkill() ← Enter键
  │     └── SelectNextSkill() / SelectPrevSkill() / RefreshSkillHighlight()
  │
  └── SkillItemVM                 ← 弹窗技能项
        ├── string SkillId / SkillName / Description
        ├── string IconItemId / TypeText
        ├── string CooldownText / CostText / CooldownLabel / CostLabel
        ├── bool IsHighlighted
        ├── SkillUIData SkillData
        └── ExecuteSelect()

纯数据类 (非ViewModel)
  ├── SkillUIData: SkillId, SkillName, Description, IconItemId, Type(SPSkillType), Cooldown, ResourceCost
  ├── SkillDiffInfo: Difficulty, UseAttribute
  ├── HeroSkillData: HeroId, MainActive, SubActive, Passive, CombatArt, Spells[4]
  └── SkillCatalog: AllSkills, GetSkillById(), GetSkillsOfType()

数据持久化
  ├── SkillConfigManager (单例): _troopSkillMap, LoadFromXml(), SaveToXml()
  └── SkillSet: MainActive, SubActive, Passive, CombatArt, Spells[4]

调试
  └── SkillDebug (静态): Log(string msg)
```

---

## 四、完整数据流

### 4.1 界面打开

```
用户打开界面
  → ScreenManager.PushScreen(new CustomSkillScreen())
  → OnInitialize()
    → new CustomSkillScreenVM()
      → SkillCatalog.LoadFromFactory()        // 从SkillFactory._skillRegistry加载所有技能
      → CreateSkillSlots()                     // 创建8个SkillSlotVM
      → PopulateRoster()                       // 从Clan.PlayerClan读取英雄列表
        → 过滤：存活 + 成年 + Active状态
        → 默认选中第一个英雄
        → SelectHero(roster[0])
          → HeroSkillData.LoadForHero(heroId)  // 从SkillConfigManager读取
          → LoadSkillsForHero(heroId)          // 填充8个SkillSlotVM
    → new GauntletLayer("CustomSkillScreen", 100)
    → LoadMovie("CustomSkillScreen", _dataSource)
```

### 4.2 英雄切换

```
用户点击英雄列表中的英雄名 / 键盘↑↓
  → HeroVM.ExecuteSelect()
    → CustomSkillScreenVM.OnHeroSelected()
      → 取消旧英雄IsSelected
      → SelectHero(新英雄)
        → IsDirty = false (丢弃未保存更改)
        → CurrentHero = hero
        → hero.IsSelected = true
        → LoadSkillsForHero(hero.HeroId)
          → HeroSkillData.LoadForHero(heroId)
          → SetSlotSkill(...) ×8  (更新每个SkillSlotVM)
```

### 4.3 技能选择（点击槽位→弹窗→选择）

```
用户点击技能槽卡片 / 键盘1~8
  → SkillSlotVM.ExecuteClick()
    → CustomSkillScreenVM.OnSlotClicked(slotVM)
      → new SkillSelectionVM(catalog, slotVM.SlotFilterType, onSelect, onClose)
        → catalog.GetSkillsOfType(filterType)  // 按类型过滤
        → 每个skill → new SkillItemVM(skill, onSelect)
        → ResetFilteredList()
      → SkillSelectionPopup = skillSelectionVM  // 触发弹窗显示

弹窗生命周期:
  CustomSkillScreen.OnFrameTick() 检测到 IsPopupOpen 变为 true
    → _gauntletLayer.LoadMovie("SkillSelectionPopup", _dataSource.SkillSelectionPopup)
    → _hasPopup = true

用户在弹窗中选择:
  方式A: 点击技能项
    → SkillItemVM.ExecuteSelect()
      → SkillSelectionVM.OnSkillSelected(skillData)
        → _onSkillSelected?.Invoke(skillData)
          → CustomSkillScreenVM.AssignSkillToSlot(slotVM, skillData)
            → slotVM.SetSkill(skillData)
            → 同步到 _currentHeroSkillData (switch case)
            → IsDirty = true
          → SkillSelectionPopup = null  ← 关闭弹窗

  方式B: 键盘↑↓导航 + Enter确认
    → SkillSelectionVM.SelectNextSkill()/SelectPrevSkill()
      → _selectedIndex 变更
      → RefreshSkillHighlight() (更新IsHighlighted)
    → CustomSkillScreen.OnFrameTick() 检测 Enter
      → PopupSelectCurrentSkill()
        → SkillSelectionVM.ExecuteSelectCurrentSkill()
          → OnSkillSelected(FilteredSkills[_selectedIndex].SkillData)

弹窗关闭:
  用户点取消/ESC/选择完成
    → SkillSelectionPopup 被设为 null
    → CustomSkillScreen.OnFrameTick() 检测到 IsPopupOpen 变为 false
      → _gauntletLayer.ReleaseMovie(_popupMovie)
      → _hasPopup = false
```

### 4.4 应用/撤销更改

```
应用 (Ctrl+S / 点击"应用更改"):
  → ExecuteApply()
    → IsDirty检查
    → SaveCurrentHeroSkills()
      → _currentHeroSkillData.Save()
        → SkillConfigManager.Instance.SetSkillSetForTroop(HeroId, ToSkillSet())
    → IsDirty = false

撤销 (Ctrl+Z / 点击"撤销更改"):
  → ExecuteUndoChanges()
    → LoadSkillsForHero(CurrentHero.HeroId)  ← 重新从ConfigManager读取
    → IsDirty = false
```

### 4.5 搜索过滤

```
用户在搜索框输入
  → SkillSelectionVM.SearchText setter
    → FilterSkills()
      → 清空 _filteredSkills
      → 如果SearchText为空: ResetFilteredList() (全部显示)
      → 否则: LINQ过滤 SkillName/Description (不区分大小写)
```

---

## 五、编译修复与API迁移记录

### 5.1 物品界面API结构分析（改造基础）

CustomSkillScreen 基于旧版骑砍2物品界面改造：

| 旧API | 新API |
|--------|--------|
| `InventorySide.Equipment` | `BattleEquipment` / `CivilianEquipment` / `StealthEquipment` |
| `InventoryMode` (枚举类型) | `InventoryScreenHelper.InventoryMode` (嵌套类型) |
| `InventoryItemType` (枚举类型) | `InventoryScreenHelper.InventoryItemType` |
| `ImageIdentifierVM` | `ItemImageIdentifierVM` |
| `ItemVM.TypeId` (set) | `ItemVM.OnItemTypeUpdated()` (方法) |
| `InventoryScreenHelper` (可实例化类) | `InventoryScreenHelper` (静态类) |
| `InventoryManager` | `InventoryScreenHelper` |
| `Transfer(8参数)` | `Transfer(7参数)` |
| `CalculateDamage(4参数)` | `CalculateDamage(3参数)` |
| `GetWieldedItemIndex` | `GetPrimaryWieldedItemIndex` |
| `ActionIndexCache.Name` | `ActionIndexCache.GetName()` |
| `WeakGameEntity` | `GameEntity` |
| `RayCastForClosestAgent(原4参数)` | 增加 excludedAgentIndex, rayThickness |

### 5.2 第一轮修复（批量，跨项目通用）

参见 `通用API迁移/批量修复编译错误.md`，涉及：
- `SkillType` → `SPSkillType`（48个文件）
- `GetWieldedItemIndex` → `GetPrimaryWieldedItemIndex`/`GetOffhandWieldedItemIndex`
- `Mission.Missiles` → `Mission.MissilesList`
- `TextObject.Empty` → `TextObject.GetEmpty()`
- `CalculateDamage` 添加 `new` 关键字

### 5.3 第二轮修复（技能界面相关，28个错误/6个文件）

#### SPSkillVM.cs (8个错误)

**SetItem 参数不匹配 (3处)**
- 行 642/694/772：新 API `SetItem(SPItemVM, InventorySide, ItemVM, BasicCharacterObject, int)` 新增 `InventorySide` 参数
- 修复：3处调用都添加 `this._currentEquipmentMode` 作为第2参数

**CalculateInventoryCapacity 参数不匹配 (2处)**
- 行 981/1980：新签名 `CalculateInventoryCapacity(MobileParty, bool, bool, int, int, int, bool)` 第3参数从 `int` 改为 `bool includeDescriptions`
- 修复：`CalculateInventoryCapacity(party, false, false, 0, 0, 0, false)`

#### SkillSystemBehavior.cs (3个错误)
- 行 579：`ActionIndexCache.Name` → `GetName()`
- 行 612/614：变量类型从 `GameEntity` 改为 `WeakGameEntity`

#### ProjectileTrajectorySystem.cs (1个错误)
- 行 344：`GameEntity` → `WeakGameEntity`

#### SkillInventoryManager.cs (1个错误)
- 行 84：`inventoryState.InventoryLogic = _inventoryLogic` → `inventoryState.SetInventoryLogic(_inventoryLogic)`

#### HeroChangeCampaignBehavior.cs (3个错误)
- 行 73-75：`OpenTroopSelection` 新签名增加 `List<Ship> eligibleShips` 参数
- 修复：在 `troopRoster` 后插入 `null` 作为 ship 参数

#### SetActionChannel AnimFlags 类型 (10个错误, 4个文件)
- `SetActionChannel` 第4参数从 `ulong` 改为 `AnimFlags` 枚举类型
- 修复：所有 `ulong` 字面量改为 `(AnimFlags)` 强制转换

| 文件 | 行号 | 修复 |
|------|------|------|
| Harmony.cs | 286 | `272UL` → `(AnimFlags)272UL` |
| HouYueSheJi.cs | 39,40,100,101 | `999UL` → `(AnimFlags)999UL` |
| TianFaZhiJian.cs | 36,122 | `512UL` → `(AnimFlags)512UL` |
| AgentSkillComponent.cs | 226 | `172UL` → `(AnimFlags)172UL` |

### 5.4 关键API签名参考

```csharp
// ItemMenuVM.SetItem
public void SetItem(SPItemVM item, InventoryLogic.InventorySide currentEquipmentMode,
    ItemVM comparedItem = null, BasicCharacterObject character = null, int alternativeUsageIndex = 0)

// InventoryCapacityModel.CalculateInventoryCapacity
public abstract ExplainedNumber CalculateInventoryCapacity(MobileParty mobileParty,
    bool isCurrentlyAtSea, bool includeDescriptions = false, int additionalManOnFoot = 0,
    int additionalSpareMounts = 0, int additionalPackAnimals = 0, bool includeFollowers = false)

// MenuContext.OpenTroopSelection
public void OpenTroopSelection(TroopRoster fullRoster, TroopRoster initialSelections,
    List<Ship> eligibleShips, Func<CharacterObject, bool> canChangeStatusOfTroop,
    Action<TroopRoster> onDone, int maxSelectableTroopCount, int minSelectableTroopCount = 1,
    bool isNavalRaid = false)

// Agent.SetActionChannel
public bool SetActionChannel(int channelNo, in ActionIndexCache actionIndexCache,
    bool ignorePriority = false, AnimFlags additionalFlags = (AnimFlags)0UL, ...)

// ActionIndexCache.GetName
public string GetName()
```

---

## 六、技能选择功能实现

### 6.1 新建文件（3个）

| 文件 | 说明 |
|------|------|
| `SkillItemVM.cs` | 技能选择列表中的单个技能项ViewModel |
| `SkillSelectionVM.cs` | 技能选择弹窗的核心ViewModel（技能列表、搜索过滤、选择回调） |
| `SkillSelectionPopup.xml` | 弹窗UI布局文件 |

### 6.2 SkillItemVM.cs

**主要属性**：`SkillId`、`SkillName`、`Description`、`IconItemId`、`Type`（SPSkillType）、`TypeText`、`Cooldown`、`ResourceCost`、`CooldownLabel`（"冷却: 3.0s"）、`CostLabel`（"消耗: 50"）、`IsHighlighted`

**主要方法**：`ExecuteSelect()`

### 6.3 SkillSelectionVM.cs

**主要属性**：`FilteredSkills`（绑定到XML）、`SearchText`、`Title`（如"选择主主动技能"）、`IsVisible`

**构造函数参数**：`SkillCatalog catalog`、`SPSkillType filterType`、`Action<SkillUIData> onSkillSelected`、`Action onClosed`

**主要方法**：`FilterSkills()`、`ExecuteSelectSkill()`、`ExecuteClose()`、`ExecuteSearch()`、`SelectNextSkill()`/`SelectPrevSkill()`、`ExecuteSelectCurrentSkill()`

### 6.4 SkillSelectionPopup.xml 布局结构

```
Widget (全屏根容器, IsVisible绑定)
├── ButtonWidget (半透明遮罩, ExecuteClose)
│   └── Widget (DoNotAcceptEvents占位)
└── BrushWidget (居中弹窗面板, Frame1Brush, Fixed 640x520)
    └── ListPanel (VerticalTopToBottom 纵向布局)
        ├── Widget (标题栏, h=46)
        │   ├── TextWidget (标题 @Title)
        │   └── ButtonWidget (关闭按钮, SPOptions.CloseButton)
        ├── BrushWidget (搜索框, h=40)
        │   └── EditableTextWidget (搜索输入框, @SearchText)
        ├── ScrollablePanel (技能列表, StretchToParent, 三件套)
        │   ├── Widget Id="PopupSkillViewport" (ClipContents=true, 裁剪视口)
        │   ├── ListPanel Id="PopupSkillInnerPanel" (CoverChildren, 纵向列表)
        │   │   └── ItemTemplate (技能项卡片, h=74)
        │   │       ├── Widget (左边条, h=4, @IsHighlighted高亮)
        │   │       └── ButtonWidget (点击选择, ExecuteSelect)
        │   │           ├── TextWidget (@SkillName, 技能名)
        │   │           ├── TextWidget (@TypeText, 类型标签, 右上角)
        │   │           ├── TextWidget (@CooldownLabel, 冷却)
        │   │           ├── TextWidget (@CostLabel, 消耗)
        │   │           └── TextWidget (@Description, 描述)
        │   └── ScrollbarWidget Id="PopupSkillScrollbar" (垂直滚动条, w=8)
        │       ├── Widget (黑色轨道背景)
        │       └── Widget Id="PopupSkillScrollbarHandle" (滚动条手柄, h=40)
        └── Widget (底部操作栏, h=46)
            ├── TextWidget (键盘提示)
            └── ListPanel (水平按钮组, HorizontalRightToLeft)
                ├── ButtonWidget (取消, ExecuteClose)
                └── ButtonWidget (确认, ExecuteSelectCurrentSkill)
```

### 6.5 CustomSkillScreen.xml 中间面板布局 (技能槽位)

```
BrushWidget (Frame1Brush, ClipContents=true, 中间主面板)
├── BrushWidget (表头, h=40, Frame2Brush)
│   └── TextWidget "已配置战志技能"
├── Widget (外层Clip容器, ClipContents=true, Margin T46 B60 L12 R12)
│   └── ScrollablePanel (三件套: SkillViewport / SkillInnerPanel / SkillScrollbar)
│       ├── Widget Id="SkillViewport" (ClipContents=true, 裁剪视口)
│       ├── ListPanel Id="SkillInnerPanel" (CoverChildren, DataSource="{Skills}")
│       │   └── ItemTemplate (技能槽位卡片, h=!Skill.CardHeight)
│       │       └── ButtonWidget (Brush="ButtonPrimary2Brush", 视觉卡片)
│       │           ├── TextWidget (@SlotLabel, 槽位标签)
│       │           ├── TextWidget (@SkillName / [未装备], 技能名)
│       │           ├── Widget (内容区, Margin T36 B12)
│       │           │   ├── BrushWidget (图标框, Frame1Brush)
│       │           │   ├── ListPanel (技能描述文本)
│       │           │   └── Widget (冷却+消耗, 右侧)
│       │           └── ButtonWidget (透明覆盖层, StretchToParent, Command.Click="ExecuteClick") ★
│       └── ScrollbarWidget Id="SkillScrollbar" (垂直滚动条, w=8)
└── BrushWidget (底栏, h=!Footer.Height, Frame2Brush)
    ├── TextWidget (快捷键提示)
    └── ListPanel (按钮组)
        ├── ButtonWidget "完成契刻" (ExecuteApply)
        └── ButtonWidget "重置" (ExecuteUndoChanges)
```

> ★ 透明覆盖层为 2026-06-09 新增，见《技能槽位点击跳转修复_透明覆盖层方案.md》

### 6.6 弹窗管理策略

采用**动态Movie加载**方案：
- 不在主XML中添加弹窗容器
- 在 `CustomSkillScreen.cs` 的 `OnFrameTick()` 中检测 `SkillSelectionPopup` 属性变化
- 当属性变为非null时，`LoadMovie("SkillSelectionPopup", viewModel)`
- 当属性变为null时，`ReleaseMovie(_popupMovie)`

### 6.7 调试日志（方案B，第二次修改添加）

全部使用 `SkillDebug.Log("[前缀] 内容")` 前缀：
- `[CSS]` = CustomSkillScreen (Screen层)
- `[CSVM]` = CustomSkillScreenVM (主ViewModel)
- `[SSVM]` = SkillSelectionVM (弹窗ViewModel)
- `[SIVM]` = SkillItemVM (弹窗技能项，仅前3个输出，用静态计数器限流)

日志输出到：`Documents\Mount and Blade II Bannerlord\SkillDebug.log`

---

## 七、全键盘快捷键实现

### 7.1 键盘映射表

#### 主界面 (CustomSkillScreen)

| 按键 | 操作 | 响应方式 | 冷却 |
|------|------|----------|------|
| `↑` | 选择上一个英雄 | IsKeyDown + 0.15s冷却 | ✓ |
| `↓` | 选择下一个英雄 | IsKeyDown + 0.15s冷却 | ✓ |
| `1` | 打开主主动槽位弹窗 | IsKeyReleased (单次) | ✗ |
| `2` | 打开副主动槽位弹窗 | IsKeyReleased | ✗ |
| `3` | 打开被动槽位弹窗 | IsKeyReleased | ✗ |
| `4` | 打开战技槽位弹窗 | IsKeyReleased | ✗ |
| `5`~`8` | 打开法术①~④弹窗 | IsKeyReleased | ✗ |
| `Ctrl+S` | 应用更改 | IsKeyReleased | ✗ |
| `Ctrl+Z` | 撤销更改 | IsKeyReleased | ✗ |
| `ESC` | 关闭弹窗/关闭界面 | IsHotKeyReleased("Exit") | ✗ |

#### 弹窗内 (SkillSelectionPopup)

| 按键 | 操作 | 响应方式 | 冷却 |
|------|------|----------|------|
| `↑` | 上一个技能项(循环) | IsKeyDown + 0.15s冷却 | ✓ |
| `↓` | 下一个技能项(循环) | IsKeyDown + 0.15s冷却 | ✓ |
| `Enter` | 确认选择当前高亮项 | IsKeyReleased (单次) | ✗ |
| `ESC` | 关闭弹窗 | IsHotKeyReleased("Exit") | ✗ |

### 7.2 修改文件（6个）

| # | 文件 | 改动 |
|---|------|------|
| 1 | `SkillItemVM.cs` | 新增 `IsHighlighted` DataSourceProperty |
| 2 | `SkillSelectionVM.cs` | 新增 `_selectedIndex`、`SelectNextSkill()`、`SelectPrevSkill()`、`ExecuteSelectCurrentSkill()`、`RefreshSkillHighlight()` |
| 3 | `CustomSkillScreenVM.cs` | 新增 `SelectNextHero()`/`SelectPrevHero()`、`SelectSlotByIndex(int)`、`IsPopupOpen`、弹窗委托方法 |
| 4 | `CustomSkillScreen.cs` | `OnFrameTick` 中添加完整键盘事件检测，`_lastKeyRepeatTime` + `KeyRepeatInterval = 0.15f` |
| 5 | `SkillSelectionPopup.xml` | 高亮背景(`#4A90D940`)和左边条(`#4A90D9FF`) + 底部键盘提示 |
| 6 | `CustomSkillScreen.xml` | 底部键盘提示文字 |

### 7.3 技术要点

- **键重复冷却**：`KeyRepeatInterval = 0.15f`，防止方向键每帧触发多次
- **长按 vs 单次**：方向键用 `IsKeyDown` + 冷却（支持长按快速导航），数字键/Ctrl组合用 `IsKeyReleased`（仅响应一次）
- **Ctrl 组合键**：独立辅助方法检测左右 Ctrl 键
- **弹窗状态判断**：通过 `IsPopupOpen` 属性分流主界面/弹窗两种键盘处理逻辑

---

## 八、UI界面修复记录

### 8.1 修复清单

| 问题 | 根因 | 修复 |
|------|------|------|
| 英雄点击区域太小，在名字左侧 | `ButtonWidget` Brush.AlphaFactor="0.06" 透明，GauntletUI 命中测试不可靠 | 添加可见底板 + ExtendCursorArea + AlphaFactor→0.12 |
| 队伍成员文本与第一个英雄名称太近 | ScrollablePanel MarginTop="8" | MarginTop→18 |
| 技能名称显示两遍 | 图标区 + 底部各一个 @SkillName | 删除图标区名称，仅底部显示 |
| 卡片缺冷却/消耗 | SkillSlotVM 无 CooldownText/CostText | 新增2个属性 + XML 底部信息行 |
| 底部"应用/撤销"按钮不响应点击 | IsEnabled="@IsDirty" 默认禁用 | 移除 IsEnabled 绑定 + AlphaFactor→0.12 |
| 弹窗技能项堆叠无法辨认 | AlphaFactor="0.04" + 无冷却消耗信息 | 增加暗色底板 + AlphaFactor→0.08 + 显示冷却/消耗标签 |
| 弹窗确认/取消按钮不可点击 | AlphaFactor 太低 + 无确认按钮 | AlphaFactor→0.12 + 新增确认按钮 |

### 8.2 修改的文件

1. **CustomSkillScreenVM.cs** (SkillSlotVM类)：新增 `CooldownText` 和 `CostText` DataSourceProperty
2. **SkillItemVM.cs**：新增 `CooldownLabel` 和 `CostLabel`
3. **CustomSkillScreen.xml**：英雄按钮底板+扩展点击区、间距、技能卡片3行布局、按钮AlphaFactor
4. **SkillSelectionPopup.xml**：弹窗高度、暗色底板、AlphaFactor、冷却/消耗显示、确认按钮、按钮栏重构

---

## 九、XML 绑定关系速查

### 9.1 CustomSkillScreen.xml → CustomSkillScreenVM

| XML绑定 | ViewModel属性 | 说明 |
|---------|--------------|------|
| `DataSource="{Roster}"` | `MBBindingList<HeroVM>` | 左侧英雄列表 |
| `Text="@HeroName"` (ItemTemplate) | `HeroVM.HeroName` | 英雄名 |
| `IsSelected="@IsSelected"` | `HeroVM.IsSelected` | 选中高亮 |
| `Command.Click="ExecuteSelect"` | `HeroVM.ExecuteSelect()` | 点击选英雄 |
| `DataSource="{Skills}"` | `MBBindingList<SkillSlotVM>` | 右侧技能槽网格 |
| `Text="@SlotLabel"` | `SkillSlotVM.SlotLabel` | 槽位标签 |
| `Text="@SkillName"` | `SkillSlotVM.SkillName` | 技能名称 |
| `Text="@CooldownText"` | `SkillSlotVM.CooldownText` | 冷却文本 |
| `Text="@CostText"` | `SkillSlotVM.CostText` | 消耗文本 |
| `IsVisible="@IsEmpty"` / `IsVisible="!@IsEmpty"` | `SkillSlotVM.IsEmpty` | 空/非空状态 |
| `Command.Click="ExecuteClick"` | `SkillSlotVM.ExecuteClick()` | 点击槽位 |
| `Command.Click="ExecuteApply"` | `CustomSkillScreenVM.ExecuteApply()` | 应用按钮 |
| `Command.Click="ExecuteUndoChanges"` | `CustomSkillScreenVM.ExecuteUndoChanges()` | 撤销按钮 |

### 9.2 SkillSelectionPopup.xml → SkillSelectionVM

| XML绑定 | ViewModel属性 | 说明 |
|---------|--------------|------|
| `IsVisible="@IsVisible"` | `SkillSelectionVM.IsVisible` | 弹窗显隐 |
| `Text="@Title"` | `SkillSelectionVM.Title` | 弹窗标题 |
| `Text="@SearchText"` | `SkillSelectionVM.SearchText` | 搜索框 |
| `DataSource="{FilteredSkills}"` | `MBBindingList<SkillItemVM>` | 技能列表 |
| `Command.Click="ExecuteSelect"` | `SkillItemVM.ExecuteSelect()` | 点击选择 |
| `Text="@SkillName"` | `SkillItemVM.SkillName` | 技能名 |
| `Text="@TypeText"` | `SkillItemVM.TypeText` | 类型标签 |
| `Text="@CooldownLabel"` | `SkillItemVM.CooldownLabel` | 冷却标签 |
| `Text="@CostLabel"` | `SkillItemVM.CostLabel` | 消耗标签 |
| `IsVisible="@IsHighlighted"` | `SkillItemVM.IsHighlighted` | 键盘导航高亮 |
| `Command.Click="ExecuteClose"` | `SkillSelectionVM.ExecuteClose()` | 取消按钮 |
| `Command.Click="ExecuteSelectCurrentSkill"` | `SkillSelectionVM.ExecuteSelectCurrentSkill()` | 确认按钮 |

---

## 十、已知问题清单

### 10.1 编译/API迁移

| # | 问题 | 状态 |
|---|------|------|
| 1 | `SPSkillVM.cs` (5828行) 仍使用旧版InventoryLogic体系，与新CustomSkillScreen平行存在 | ⚠️ 待处理 |

### 10.2 UI交互问题

| # | 问题 | 状态 |
|---|------|------|
| 2 | **技能槽位点击无响应**（点击技能名称位置无法跳转弹窗） | ✅ 2026-06-09 已修复（透明覆盖层方案） |
| 3 | 弹窗技能项堆叠 | ❌ |
| 4 | ESC关闭弹窗后主界面失焦 | ❌ |
| 5 | 技能图标未显示 | ❌ |
| 6 | 技能描述未显示 | ❌ |

### 10.3 功能缺失

| # | 问题 |
|---|------|
| 7 | ESC关闭主界面时未检查IsDirty（TODO注释，目前直接关闭丢弃更改） |
| 8 | 无多选支持（当前只能单选技能） |

---

## 十一、关键代码路径速查

### 入口链路
```
CustomSkillScreen.OnInitialize()                          ← Line 41
  → new CustomSkillScreenVM()                             ← Line 46
  → _gauntletLayer.LoadMovie("CustomSkillScreen", ...)    ← Line 64
```

### 弹窗打开链路
```
SkillSlotVM.ExecuteClick()                                ← CustomSkillScreenVM.cs:235
  → _onClickSlot?.Invoke(this)
    → CustomSkillScreenVM.OnSlotClicked(slotVM)           ← Line 612
      → new SkillSelectionVM(...)                         ← Line 620
      → SkillSelectionPopup = skillSelectionVM            ← Line 637
```

### 弹窗Movie加载/释放
```
CustomSkillScreen.OnFrameTick()                           ← Line 68
  isPopupOpen 变化检测:                                    ← Line 75-94
    打开: LoadMovie("SkillSelectionPopup", viewModel)
    关闭: ReleaseMovie(_popupMovie)
```

### 键盘检测
```
CustomSkillScreen.OnFrameTick()                           ← Line 68
  canRepeat = (dt + _lastKeyRepeatTime >= 0.15f)          ← Line 97
  弹窗内: ↑↓(IsKeyDown) + Enter(IsKeyReleased)            ← Line 99-123
  主界面: ↑↓(IsKeyDown) + 1~8/Ctrl+S/Ctrl+Z(IsKeyReleased) ← Line 125-168
  ESC: IsHotKeyReleased("Exit")                           ← Line 171
```

### 数据保存
```
CustomSkillScreenVM.ExecuteApply()                        ← Line 693
  → SaveCurrentHeroSkills()                               ← Line 682
    → _currentHeroSkillData.Save()                        ← SkillModel.cs:227
      → SkillConfigManager.Instance.SetSkillSetForTroop() ← SkillConfigManager.cs:124
```

---

## 十二、快速修改索引

| 想改什么 | 改哪个文件 | 大概位置 |
|----------|-----------|----------|
| 添加/删除技能槽位 | `CustomSkillScreenVM.cs` → `CreateSkillSlots()` | Line 431-455 |
| 修改槽位过滤类型 | `CustomSkillScreenVM.cs` → slotDefs 数组 | Line 437-448 |
| 修改主界面布局 | `CustomSkillScreen.xml` | 全文306行 |
| 修改弹窗布局 | `SkillSelectionPopup.xml` | 全文209行 |
| 修改键盘快捷键 | `CustomSkillScreen.cs` → `OnFrameTick()` | Line 96-192 |
| 修改键盘冷却时间 | `CustomSkillScreen.cs` → `KeyRepeatInterval` | Line 31 |
| 修改弹窗搜索逻辑 | `SkillSelectionVM.cs` → `FilterSkills()` | Line 147-171 |
| 修改技能数据模型 | `SkillModel.cs` → `SkillUIData` | Line 56-115 |
| 修改技能持久化逻辑 | `SkillConfigManager.cs` | 全文247行 |
| 修改调试日志 | `SkillDebug.cs` | 全文48行 |

---

## 十三、常见问题排查指南

### Q: 弹窗打不开
1. 检查 `SkillDebug.log` 是否有 `[CSVM] 槽位点击:` 输出
2. 检查是否有 `[CSVM] OnSlotClicked 完成` 输出
3. 检查是否有 `[CSS] 弹窗已打开` 输出
4. 检查 `FilteredSkills.Count` 是否为0

### Q: 弹窗中技能项不可点击
1. 检查XML中ButtonWidget的 `Brush.AlphaFactor` 是否太低（需 ≥ 0.08）
2. 检查是否有其他Widget覆盖在ButtonWidget上面
3. 确认 `ExecuteSelect` 方法的调用链路正确

### Q: 技能保存后重启丢失
1. 检查 `SkillConfigManager._troopSkillMap` 在 LoadFromXml 时是否正确填充
2. 检查 SaveToXml 的路径和权限
3. 检查 SetSkillSetForTroop 逻辑：`if` 分支覆盖 + `else if !TryGetValue` 再Add

### Q: 英雄列表为空
1. 检查 `Clan.PlayerClan` 是否为null
2. 检查过滤条件是否太严格（IsAlive + Age >= 18 + HeroState==Active）
3. 检查 `SkillDebug.log` 中 `[CSVM] PopulateRoster 完成` 的Count

---

## 十四、GauntletUI 关键规则（避免重复踩坑）

1. 根元素必须是 `<Prefab>`，不能直接 `<Window>`
2. 所有子控件必须用 `<Children>` 包裹，直接嵌套会被引擎忽略
3. 数据绑定用 `@` 前缀，DataSource 用 `DataSource=`
4. 按钮命令用 `Command.Click="MethodName"`（不是 `Press`）
5. `ButtonWidget` 的点击区域依赖 `Brush` 的渲染区域，`AlphaFactor` 太低（< 0.08）会导致命中测试失败
6. 推荐在 `ButtonWidget` 下方放一个 `DoNotAcceptEvents="true"` 的可见背景 Widget
7. 用 `ExtendCursorAreaLeft/Right/Top/Bottom` 扩展点击区域
8. `ClipContents="true"` 必须设置，否则滚动内容溢出

---

## 十五、依赖关系图

```
SkillConfigManager (单例/持久化)
        ↓↑
   SkillSet (数据容器)
        ↓↑
  HeroSkillData.FromSkillSet() / ToSkillSet()
        ↓
  SkillUIData (纯UI数据，从 SkillBase 转换)
        ↓
        ├→ SkillSlotVM.SetSkill(SkillUIData)
        │       ↓
        │   CustomSkillScreenVM
        │       ↓
        │   CustomSkillScreen (Screen层)
        │       ↓
        │   CustomSkillScreen.xml (GauntletUI)
        │
        └→ SkillItemVM(SkillUIData)
                ↓
            SkillSelectionVM
                ↓
            CustomSkillScreenVM.SkillSelectionPopup
                ↓
            CustomSkillScreen 检测变化 → LoadMovie/ReleaseMovie
                ↓
            SkillSelectionPopup.xml (GauntletUI)

SkillFactory._skillRegistry (游戏逻辑层技能注册表)
        ↓
  SkillCatalog.LoadFromFactory()
        ↓
  SkillCatalog.AllSkills / GetSkillsOfType()
        ↓
  SkillSelectionVM → FilteredSkills
```

---

## 十六、履历

| 日期 | 工作内容 |
|------|----------|
| 2025-06-05 | 物品界面API结构分析，制定修复计划 |
| 2025-06-05 | 第二轮编译错误修复（28个错误，SPSkillVM/SetActionChannel等） |
| 2026-06-06 | 技能选择功能实现（3个新建文件 + 2个修改文件） |
| 2026-06-06 | 全键盘快捷键实现（6个文件修改） |
| 2026-06-06 | 技能选择功能调试日志添加（第二次修改，4个文件） |
| 2026-06-06 | UI界面修复（英雄选择、技能弹窗、AlphaFactor、冷却消耗显示） |
| 2026-06-06 | 整合为完整参考手册 |

---

*整合时间：2026-06-06 21:20*
