# CodeExplorer 产出：New_ZZZF 技能配置系统全面探索

> 调用时间：2026-06-13 14:06
> 调用原因：用户要求制作新的选择技能界面，需要全面了解现有技能配置系统的架构、数据流和所有关键文件
> 对应讨论：策划新版技能界面（三种目标类型：队伍成员/兵种模板/领主NPC）

---

## 一、调用原因

用户在策划新版技能界面时提出需求：
1. 制作新的选择技能界面，完全淘汰旧界面
2. 除了修改队伍内 NPC 技能外，还需要在调试模式下修改**兵种模板**和**领主NPC**的技能
3. 技术已确认可行，需要重新策划 UI 界面与交互

为完成策划，必须全面了解：
- 技能配置的持久化机制（SkillConfigManager）
- 技能注册与数据模型（SkillFactory / SkillBase / SkillSet）
- 现有的 UI 架构（CustomSkillScreen / ViewModel / XML 布局）
- SubModule 入口和快捷键绑定
- 数据如何在各层之间流转

---

## 二、探索目标

1. 找到 `SkillConfigManager` 的完整实现，特别是 `GetSkillSetForTroop` / `SetSkillSetForTroop`
2. 找到 `SkillFactory._skillRegistry` 的结构和技能注册机制
3. 理解 `CharacterObject.StringId` 作为兵种标识的机制
4. 定位所有 GUI 相关文件（C# + XML）
5. 查找 SubModule 中的 GUI 打开入口
6. 理解整个数据流：配置加载 → UI 展示 → 用户修改 → 保存

---

## 三、发现的文件和目录结构

### 3.1 核心系统文件（数据持久化层）

| 文件 | 绝对路径 | 职责 |
|------|---------|------|
| SkillConfigManager.cs | `e:\SteamLibrary\...\New_ZZZF\工程\New_ZZZF\Systems\SkillConfigManager.cs` | 技能配置单例管理器：XML加载/保存、troopId↔SkillSet映射 |
| SkillFactory.cs | `e:\SteamLibrary\...\New_ZZZF\工程\New_ZZZF\Systems\SkillFactory.cs` | 技能注册表：`_skillRegistry` 字典，技能ID→SkillBase实例 |
| SkillBase.cs | `e:\SteamLibrary\...\New_ZZZF\工程\New_ZZZF\Systems\SkillBase.cs` | 技能抽象基类 + SPSkillType枚举定义 |
| AgentSkillComponent.cs | `e:\SteamLibrary\...\New_ZZZF\工程\New_ZZZF\Systems\AgentSkillComponent.cs` | Agent技能组件：运行时技能触发、冷却管理 |

### 3.2 GUI 文件（UI层）

| 文件 | 绝对路径 | 职责 |
|------|---------|------|
| CustomSkillScreen.cs | `...\New_ZZZF\工程\New_ZZZF\GUI\CustomSkillScreen.cs` (261行) | Screen层：GauntletLayer管理、Movie加载/释放、键盘输入 |
| CustomSkillScreenVM.cs | `...\New_ZZZF\工程\New_ZZZF\GUI\CustomSkillScreenVM.cs` (755行) | 主ViewModel：HeroVM/SkillSlotVM/CustomSkillScreenVM |
| SkillSelectionVM.cs | `...\New_ZZZF\工程\New_ZZZF\GUI\SkillSelectionVM.cs` (319行) | 弹窗ViewModel：技能列表、搜索过滤、键盘导航 |
| SkillItemVM.cs | `...\New_ZZZF\工程\New_ZZZF\GUI\SkillItemVM.cs` (242行) | 弹窗ItemTemplate：单个技能项的属性绑定 |
| SkillModel.cs | `...\New_ZZZF\工程\New_ZZZF\GUI\SkillModel.cs` (291行) | 数据模型：SkillUIData、HeroSkillData、SkillCatalog |
| SkillDebug.cs | `...\New_ZZZF\工程\New_ZZZF\GUI\SkillDebug.cs` (57行) | 调试日志（当前已停用） |

### 3.3 XML 布局

| 文件 | 绝对路径 | 职责 |
|------|---------|------|
| CustomSkillScreen.xml | `...\New_ZZZF\GUI\Prefabs\CustomSkillScreen.xml` (259行) | 主界面布局 |
| SkillSelectionPopup.xml | `...\New_ZZZF\GUI\Prefabs\SkillSelectionPopup.xml` (167行) | 弹窗布局 |

### 3.4 入口与配置

| 文件 | 绝对路径 | 职责 |
|------|---------|------|
| SubModule.cs | `...\New_ZZZF\工程\New_ZZZF\SubModule.cs` (225行) | M键打开界面、L键重载配置、词缀调试快捷键 |
| troop_skills.xml | `...\New_ZZZF\ModuleData\troop_skills.xml` | 技能配置文件 |

### 3.5 旧代码（参考）

| 文件 | 绝对路径 |
|------|---------|
| GauntletSkillScreen.cs | `...\New_ZZZF\工程\New_ZZZF\GUI\旧代码\GauntletSkillScreen.cs` |
| SkillInventoryManager.cs | `...\New_ZZZF\工程\New_ZZZF\GUI\旧代码\SkillInventoryManager.cs` |
| SkillInventoryState.cs | `...\New_ZZZF\工程\New_ZZZF\GUI\旧代码\SkillInventoryState.cs` |
| SPSkillItemVM.cs | `...\New_ZZZF\工程\New_ZZZF\GUI\旧代码\SPSkillItemVM.cs` |
| SPSkillVM.cs | `...\New_ZZZF\工程\New_ZZZF\GUI\旧代码\SPSkillVM.cs` (5828行) |

---

## 四、关键代码片段位置

### 4.1 SPSkillType 枚举
- 文件：`SkillBase.cs`
```csharp
public enum SPSkillType
{
    None = 0,
    MainActive,        // 主主动
    SubActive,         // 副主动
    Passive,           // 被动
    Spell,             // 法术栏
    CombatArt,         // 战技栏
    Passive_Spell,     // 可放在法术栏的被动
    CombatArt_Spell,   // 可放在法术栏的战技
    Spell_CombatArt    // 可放在战技栏的法术
}
```

### 4.2 SkillSet 类（数据容器）
- 文件：`SkillConfigManager.cs`，行 190-211
```csharp
public class SkillSet
{
    public SkillBase MainActive { get; set; }
    public SkillBase SubActive { get; set; }
    public SkillBase Passive { get; set; }
    public SkillBase[] Spells { get; } = new SkillBase[4];
    public SkillBase CombatArt { get; set; }
    // 构造函数全部初始化为 NullSkill
}
```

### 4.3 SkillConfigManager 核心方法
- `GetSkillSetForTroop(string troopId)` — 行 111-120
- `SetSkillSetForTroop(string troopId, SkillSet setSkillSet)` — 行 124-135
- `LoadFromXml(string xmlPath)` — 行 34-66
- `SaveToXml(string xmlPath)` — 行 71-100
- `_troopSkillMap` — `Dictionary<string, SkillSet>`，key = CharacterObject.StringId

### 4.4 SkillBase 抽象基类
- 文件：`SkillBase.cs`
- 关键属性：`SkillID`(string)、`Type`(SPSkillType)、`Cooldown`(float)、`ResourceCost`(float)、`Item`(ItemObject)、`Text`(TextObject)、`Description`(TextObject)
- 核心方法：`abstract bool Activate(Agent casterAgent)`、`virtual void OnEquip(Agent agent)`、`virtual void OnUnequip(Agent agent)`

### 4.5 SkillUIData（UI层纯数据模型）
- 文件：`SkillModel.cs`，行 56-115
- 关键属性：`SkillId`、`SkillName`、`Description`、`IconItemId`、`Type`、`Cooldown`、`ResourceCost`
- 静态工厂：`FromSkillBase(SkillBase skill)` — 行 96-115
- 空单例：`SkillUIData.Empty` — 行 86-91

### 4.6 界面打开入口
- 文件：`SubModule.cs`，行 152-158
```csharp
if (Input.IsKeyPressed(InputKey.M) && Campaign.Current != null
    && Mission.Current == null
    && !Game.Current.GameStateManager.ActiveState.IsMenuState
    && !(ScreenManager.TopScreen is CustomSkillScreen))
{
    ScreenManager.PushScreen(new CustomSkillScreen());
}
```

### 4.7 当前 PopulateRoster（仅队伍成员）
- 文件：`CustomSkillScreenVM.cs`，行 396-425
- 数据源：`Clan.PlayerClan.Heroes`
- 过滤：IsAlive + Age >= heroComesOfAge + HeroState == Active
- 默认选中第一个

### 4.8 技能槽位定义
- 文件：`CustomSkillScreenVM.cs`，行 437-448
```csharp
// 8个槽位：(slotId, slotLabel, filterType)
("MainActive", "主主动", SPSkillType.MainActive),
("SubActive",  "副主动", SPSkillType.SubActive),
("Passive",    "被动",   SPSkillType.Passive),
("CombatArt",  "战技",   SPSkillType.CombatArt),
("Spell0",     "法术①", SPSkillType.Spell),  // 4个法术槽
```

---

## 五、功能模块间的依赖关系

```
SkillFactory._skillRegistry (技能注册表)
        ↓
  SkillCatalog.LoadFromFactory()  ← 全技能目录
        ↓
  SkillCatalog.AllSkills / GetSkillsOfType()
        ↓                              ↓
  SkillSelectionVM (搜索筛选)    CustomSkillScreenVM (技能槽展示)
        ↓                              ↓
  SkillSelectionPopup.xml         CustomSkillScreen.xml
        ↓                              ↓
  用户选择技能 ──────────→ AssignSkillToSlot()
                                ↓
                          HeroSkillData.Save()
                                ↓
                    SkillConfigManager.SetSkillSetForTroop()
                                ↓
                    _troopSkillMap[troopId] = SkillSet
                                ↓
                        AgentSkillComponent.InitializeFromTroop()
                        (下次进战场时生效)
```

**关键数据流**：
1. 配置加载：XML → SkillConfigManager._troopSkillMap
2. UI展示：SkillConfigManager → HeroSkillData.LoadForHero() → SkillSlotVM.SetSkill()
3. 用户修改：SkillSlotVM → HeroSkillData → SkillConfigManager.SetSkillSetForTroop()
4. 战场生效：Agent创建 → AgentSkillComponent → SkillConfigManager.GetSkillSetForTroop()

**核心设计模式**：
- SkillUIData 是 UI 层和逻辑层的桥接模型（凭证模式）
- 不直接持有 ItemObject/TextObject 引用，全部转换为 string/float/enum
- HeroSkillData 负责 SkillSet ↔ SkillUIData 的转换

---

## 六、可复用的分析结论

### 6.1 三种目标类型统一使用 StringId 作为 Key
- `SkillConfigManager._troopSkillMap` 的 key 是 `string troopId`
- 队伍英雄、兵种模板、领主 NPC 都通过 `CharacterObject.StringId` 标识
- 底层保存逻辑完全一致，只需改变数据源即可支持三种目标

### 6.2 可能的三种数据源
| 目标类型 | 数据源 | 获取方式 |
|---------|--------|---------|
| 队伍成员 | `Clan.PlayerClan.Heroes` | 遍历 + 过滤（存活/成年/Active） |
| 兵种模板 | `Game.Current.ObjectManager.GetObjectTypeList<CharacterObject>()` | 过滤非英雄角色 |
| 领主NPC | `Hero.AllAliveHeroes` | 过滤是领主/贵族 |

### 6.3 XML 布局规范（GauntletUI 关键规则）
- 根元素必须是 `<Prefab>`，必须用 `<Children>` 包裹子控件
- `ClipContents="true"` 防止滚动内容溢出
- `AlphaFactor ≥ 0.08` 才能保证命中测试成功
- ScrollablePanel 三件套：Viewport + InnerPanel + Scrollbar（必须是直接子节点）
- 数据绑定用 `@`，DataSource 用 `DataSource=`
- 按钮命令用 `Command.Click="MethodName"`

### 6.4 弹窗管理策略（可复用）
- 不在主 XML 中静态添加弹窗容器
- 在 Screen.OnFrameTick 检测 ViewModel 属性变化
- 属性变为非 null → `LoadMovie("Popup", viewModel)`
- 属性变为 null → `ReleaseMovie(_popupMovie)`
- 键盘事件通过 `IsPopupOpen` 分流主界面/弹窗两种处理

### 6.5 键盘导航实现模式
- 方向键用 `IsKeyDown` + 0.15s 冷却（支持长按）
- 选择键用 `IsKeyReleased`（仅响应一次）
- 在 OnFrameTick 中先检查 canRepeat，再处理各键

---

## 七、未解决的问题

1. `MBObjectManager.GetObjectTypeList<CharacterObject>()` 是否能正确获取所有兵种模板？需要在实际代码中验证
2. 兵种模板列表中是否包含英雄角色？需要额外的 IsHero 过滤
3. 右侧详情面板需要哪些具体字段？与用户确认后确定

---

## 八、相关参考文档

- `New_ZZZF_技能界面完整参考手册.md` — 最全面的技能界面参考
- `2026-06-09_技能槽位点击跳转修复_透明覆盖层方案.md` — 槽位点击修复
- `GauntletUI_ScrollablePanel滚动条制作完整教程.md` — XML 滚动条规范
- `API迁移记忆库.md` — 所有 API 变更对照表

---

> 产出时间：2026-06-13 14:15
> 下次如需了解 New_ZZZF 技能配置系统架构，优先阅读此文档
