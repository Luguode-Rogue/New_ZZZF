# CleanHeroCreate 招募 NPC 开发问题集

**日期**：2026-08-08  
**模块**：CleanHeroCreate（酒馆老板"选择并创建 NPC"招募改造）  
**状态**：✅ 已修复，验证通过

> 本文汇总该模块开发期间遇到的典型问题及解决方法。其中标记为
> 「★可复用」的，在其他功能模块里也反复出现过，建议横向排查时优先对照。

---

## 问题 1 ★可复用：从 MBObjectManager 取对象时混入"实例"，导致同类重复 + 显示具体名字

**现象**：酒馆招募列表里，同一职业出现两条，且其中一条带具体 NPC 名字（如 "Aldric the Scholar"），而非纯职业标题。

**根因**：
`MBObjectManager.Instance.GetObjectTypeList<CharacterObject>()` 取到的不只是 XML 里的
**基础模板定义**，还包括游戏世界里**由模板生成的具体 NPC 实例**。

在 Bannerlord 中：
- **基础模板**（XML 中 `is_template="true"`）：`_originCharacter == null`，属性 `IsOriginalCharacter == true`。
- **具体 NPC 实例**（由模板派生）：`_originCharacter` 指向其模板，`IsOriginalCharacter == false`，
  且 `Name` 中的 `{FIRSTNAME}` 占位符已被解析为真实名字。

原过滤只判断 `Occupation == Wanderer`，于是同一个模板 + 它的实例都被列出 → 同类重复 + 带名字显示。

**解决**：在加载层加 `IsOriginalCharacter` 过滤，只保留基础模板：

```csharp
var templates = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>()
    .Where(c => c.Occupation == Occupation.Wanderer && c.IsOriginalCharacter)
    .ToList();
```

`CleanName` 只需删掉 `{FIRSTNAME}` token 即可（模板名里该 token 仍在）：

```csharp
private static readonly Regex FirstNameToken = new Regex(@"\{(FIRSTNAME|LASTNAME|CLANNAME)\}", RegexOptions.IgnoreCase);
private static string CleanName(CharacterObject template)
{
    string raw = template.Name != null ? template.Name.ToString() : template.StringId;
    raw = FirstNameToken.Replace(raw, "").Trim();
    return string.IsNullOrWhiteSpace(raw) ? template.StringId : raw;
}
```

**★可复用提示**：凡是"遍历 MBObjectManager 取 CharacterObject / ItemObject / 任意 MBObject"时，
务必区分**模板（定义）**与**运行时实例**。典型判断字段：
- `CharacterObject.IsOriginalCharacter`（`_originCharacter == null` 即模板）
- 实例通过 `OriginalCharacter` 可回溯到模板。
若只想要"定义"，必须加 `IsOriginalCharacter` 过滤，否则会重复列出且实例名已带具体名字/属性。

---

## 问题 2 ★可复用：用正则"去首词"治标不治本——误把根因放在展示层

**现象（前一轮错误修复）**：为消除"带名字的 NPC"，在 `CleanName` 里用正则去掉名字首词。
结果：同类 NPC 仍重复显示（根因未除），且对正常模板名有潜在误伤。

**根因**：真正的重复来自加载层混入了 NPC 实例（见问题 1），展示层去名字只是掩盖。

**解决**：把过滤逻辑上移到加载层（`IsOriginalCharacter`），`CleanName` 回归只处理模板占位符。

**★可复用提示**：遇到"列表里有具体名字/重复项"，先查**数据来源（加载/取数）**是否混入了实例，
不要只在 UI/格式化层打补丁。展示层正则无法消除数据层的重复。

---

## 问题 3：创建的 NPC "看不到在哪"、也没有提示

**现象**：对话里确认 NPC 已创建，但场景和地图上都看不到 NPC，游戏内百科的 NPC 列表能看到它，
但玩家不知道去哪找，也没有任何指引。

**根因**：`HeroCreator.CreateSpecialHero(template, settlement, ...)` 只是把 NPC 对象塞进
该城镇数据层的 `HeroesWithoutParty`，**不会**让 NPC 真正"驻扎"在城镇、也不会触发任何
"待招募"任务/日志/地图标记。原版"查找 NPC"之所以没问题，是因为被找的 NPC 本来就在某城镇走动；
而我们是"新建"，没有任何位置/指引信息。

**解决**：创建后让 NPC 真正进驻城镇，并给玩家定位提示：

```csharp
Hero hero = HeroCreator.CreateSpecialHero(entry.Template, settlement, null, null, -1);
hero.IsKnownToPlayer = true;
hero.ChangeState(Hero.CharacterStates.Active);

// 让 NPC 进驻城镇：出现在该城镇冒险者列表，可被找到
EnterSettlementAction.ApplyForCharacterOnly(hero, settlement);
hero.StayingInSettlement = settlement;

// 确保城镇在地图可见 + 弹提示告知玩家去哪找
settlement.IsVisible = true;
var tip = new TextObject("{=CHC_RECRUIT_TIP}{NEW_HERO} has arrived at the tavern in {TOWN}. Go there and recruit them from the wanderer list.")
    .SetTextVariable("NEW_HERO", hero.Name)
    .SetTextVariable("TOWN", settlement.Name);
MBInformationManager.AddQuickInformation(tip);
```

完成对话文案同步改为"正在 {城镇名} 酒馆等候，去该城镇冒险者列表招募"。

**★可复用提示**：`CreateSpecialHero` 不会自动让 NPC 在场景/地图可见，必须显式
`EnterSettlementAction.ApplyForCharacterOnly` + 设置 `StayingInSettlement`，
否则 NPC 只存在于数据层。需要指引玩家时，用 `MBInformationManager.AddQuickInformation` 弹提示。

> 注：`MBInformationManager.AddQuickInformation` 存在重载歧义（第 3 参 `BasicCharacterObject` /
> 第 4 参 `string`），单参调用 `AddQuickInformation(tip)` 最稳，避免 `null` 被推断错类型导致编译失败。

---

## 问题 4：对话界面无滚动，职能/模板选项过多导致界面溢出

**现象**：合并的职能（如原 Leader 同时含 Tactics+Leadership）单类模板过多；游戏对话菜单
**没有滚动条**，选项会超出可视区域无法选择。

**根因**：Bannerlord 原生对话（`ConversationManager`）的玩家选项列表不支持滚动，
选项数过多会被截断或遮挡。

**解决**：
1. 把合并的职能拆开，减少单类数量：`Leader(Tactics/Leadership)` 拆为
   `Tactician(Tactics)` 与 `Leader(Leadership)` 两个独立职能（枚举 + `RoleSkill` 映射 + 本地化三处同步改）。
2. 硬性截断每类显示数量（`MaxPerRole = 8`），超出部分只给一行提示，不可选：

```csharp
int shown = Math.Min(templates.Count, MaxPerRole);
for (int j = 0; j < shown; j++) { /* 生成可选模板行 */ }

if (templates.Count > MaxPerRole)
{
    starter.AddDialogLine("chc_list_more_" + idx, listId, listId,
        "{=CHC_LIST_MORE}...and {MORE} more I won't bother listing here.",
        () => true,
        () => { MBTextManager.SetTextVariable("MORE", templates.Count - MaxPerRole); }, 0);
}
```

**★可复用提示**：任何通过 `AddPlayerLine` 生成的对话菜单，都要**预估选项数量上限**。
Bannerlord 对话无滚动，选项过多必须分页（拆职能/拆字母段）或截断，否则玩家无法选到靠后的项。

---

## 问题 5：对话流程缺少"返回"选项，玩家卡在子菜单

**现象**：进入职能选择、模板清单后，没有返回上一级或退出酒馆的入口。

**解决**：在 self-loop 菜单节点上补充返回分支（直接指向上一层对话 ID 或酒馆主菜单）：

```csharp
// 从职能选择页返回酒馆主菜单
starter.AddPlayerLine("chc_recruit_type_back", "chc_recruit_type", "tavernkeeper_talk",
    "{=CHC_BACK_TO_TAVERN}Never mind, I'll think about it.", () => true, () => { }, 1);

// 从模板清单页返回职能选择
starter.AddPlayerLine("chc_list_back_" + idx, listId, "chc_recruit_type",
    "{=CHC_BACK_TO_TYPES}Let me look at other types.", () => true, () => { }, 1);
```

**★可复用提示**：自建对话树（尤其 self-loop 菜单节点）必须显式补"返回/退出"分支，
原生对话系统不会自动提供，否则玩家会被困在当前层级。

---

## 问题 6 ★可复用：编译常见错误（Bannerlord 模组通用）

开发期遇到的 5 个编译错误及对应修复，均属跨模块高频问题：

| 错误 | 原因 | 修复 |
|------|------|------|
| `TavernEmployeesCampaignBehavior` 找不到（CS0246） | 编译期硬引用 SandBox 内部类 | 改用 Harmony **字符串类名** patch：`[HarmonyPatch("TaleWorlds.CampaignSystem.CampaignBehaviors.TavernEmployeesCampaignBehavior")]`，消除对 SandBox 程序集的硬引用 |
| `DefaultSkills.Navigation` 不存在 | 该静态属性在原版不存在 | 改用 `Campaign.Current.Models.ClanMemberPartyRoleModel.GetRelevantSkillForPartyRole(PartyRole.Navigator/FirstMate)` 获取职能技能 |
| `IGameStarter` 无 `AddBehavior` | 该接口方法在 `CampaignGameStarter` 上 | cast：`((CampaignGameStarter)starter).AddBehavior(new XxxBehavior())` |
| `MBObjectManager` 命名空间错 | 位于 `TaleWorlds.ObjectSystem` | `using TaleWorlds.ObjectSystem;`，用 `MBObjectManager.Instance.GetObjectTypeList<T>()` |
| `StringHelpers` / `GiveGoldAction` 找不到 | 命名空间错 | 用 `MBTextManager.SetTextVariable` 设文本变量；`using TaleWorlds.CampaignSystem.Actions;` 引入 `GiveGoldAction` |

**★可复用提示**：
- 跨程序集 patch 内部类一律用**字符串类名 + 字符串方法名**的 Harmony 写法，避免编译期依赖。
- 想拿"某职能对应的技能"优先用 `ClanMemberPartyRoleModel.GetRelevantSkillForPartyRole`，不要硬编码 `DefaultSkills.Xxx`。
- 注册 `ICampaignBehavior` 时 `starter` 需 cast 成 `CampaignGameStarter` 再 `AddBehavior`。

---

## 问题 7：原版招募是"查找"非"创建"，规划文档事实错误

**现象**：早期整合设计文档把酒馆招募写成"创建 NPC"，与原版不符。

**根因**：原版 `TavernEmployeesCampaignBehavior.FindCompanionWithType` 是从**现有流浪者**中
查找（被找的 NPC 本来就在某城镇），并非凭空创建。我们的需求是"选择并创建"，属于改造而非还原。

**解决**：设计文档已纠正为"选择并创建"路线；代码侧隐藏原版查找入口
（`tavernkeeper_companion_info_on_condition` prefix 返回 false），用自建创建流程替代。

**★可复用提示**：动原版系统前，先用 dnSpy 确认原版到底做"查找"还是"创建"、
数据源在 XML 模板还是运行时实例，避免基于错误前提设计。

---

## 附：本次涉及的关键代码位置

| 内容 | 文件 |
|------|------|
| 模板加载 / 重名去重 / 职能分类 / 显示名 | `CleanHeroCreate/NPCTemplateService.cs` |
| 对话流（菜单 / 返回 / 截断 / 创建 / 提示） | `CleanHeroCreate/CHCRecruitCampaignBehavior.cs` |
| 隐藏原版查找入口 | `CleanHeroCreate/Patch_TavernRecruit.cs` |
| 注册 Behavior | `CleanHeroCreate/SubModule.cs` |
| 中英双语本地化 | `ModuleData/Languages/CNs/std_module_strings_xml-zho-CN.xml`、`ModuleData/Languages/EN/std_module_strings_xml.xml` |

## 附：提交记录（CleanHeroCreate 仓库）

| Commit | 内容 |
|--------|------|
| f7fb19e | 初始化 git 仓库 |
| 27668fc | 招募改造（选择并创建 NPC） |
| 9ff9fe2 | NPC 创建后进驻城镇 + 提示位置 |
| ceaf... | 补返回选项 / 战术领导拆分 / 每类截断 |
| 393e4e0 | 只取基础模板（IsOriginalCharacter），修复同类重复 + 带名字显示 |
