# 2026-06-13 CodeExplorer: 兵种模板与领主获取逻辑排查

## 调用原因
用户反馈"兵种获取有问题"，需要排查 CustomSkillScreen 中兵种模板和领主NPC的获取逻辑，确认使用了正确的 API 来获取全部兵种和全部领主。

## 探索目标
1. 查找 `PopulateTroopTemplates()` 如何获取兵种模板
2. 查找 `PopulateLordNPCs()` 如何获取领主列表
3. 查找 `PopulateRoster()` 如何获取队伍成员
4. 查找所有与 CharacterObject、Hero、TroopRoster 相关的 API 调用
5. 确认 `HeroVM` 构造函数的参数使用

## 探索范围
- `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\New_ZZZF\工程\New_ZZZF\`

---

## 发现的文件和关键代码

### 文件 1: `GUI\CustomSkillScreenVM.cs` — 核心 ViewModel

#### (A) `PopulateTroopTemplates()` — 兵种模板获取（第 717-737 行）

```csharp
private void PopulateTroopTemplates()
{
    TroopTemplates.Clear();
    try
    {
        var addedIds = new HashSet<string>();
        foreach (var culture in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
        {
            if (culture == null) continue;
            var basicTroop = culture.BasicTroop;
            if (basicTroop != null && addedIds.Add(basicTroop.StringId))
                TroopTemplates.Add(new HeroVM(basicTroop, OnTargetSelected));
            var eliteTroop = culture.EliteBasicTroop;
            if (eliteTroop != null && addedIds.Add(eliteTroop.StringId))
                TroopTemplates.Add(new HeroVM(eliteTroop, OnTargetSelected));
        }
    }
    catch { /* 静默失败 */ }
}
```

**问题**: 只取每个文化的 BasicTroop 和 EliteBasicTroop（最多2个/文化 ≈ 十几个兵种），远少于游戏实际加载的全部兵种模板。

#### (B) `PopulateLordNPCs()` — 领主NPC获取（第 739-756 行）

```csharp
private void PopulateLordNPCs()
{
    LordNPCs.Clear();
    try
    {
        foreach (var clan in Clan.All)
        {
            if (clan == null || clan.IsEliminated) continue;
            foreach (var hero in clan.Heroes)
            {
                if (hero == null || !hero.IsAlive || hero.Age < 18) continue;
                LordNPCs.Add(new HeroVM(hero, OnTargetSelected));
            }
        }
    }
    catch { /* 静默失败 */ }
}
```

**问题**: 遍历 Clan.All → Heroes 会混入流浪者、商人等非领主角色，过滤条件仅有 IsAlive + Age >= 18。

#### (C) `PopulateRoster()` — 队伍成员获取（第 694-715 行）

```csharp
private void PopulateRoster()
{
    Roster.Clear();
    var playerClan = Clan.PlayerClan;
    if (playerClan == null) return;
    int comeOfAge = Campaign.Current.Models?.AgeModel?.HeroComesOfAge ?? 18;
    foreach (var hero in playerClan.Heroes)
    {
        if (hero == null) continue;
        if (!hero.IsAlive) continue;
        if (hero.Age < comeOfAge) continue;
        if (hero.HeroState != Hero.CharacterStates.Active && hero != Hero.MainHero) continue;
        Roster.Add(new HeroVM(hero, OnTargetSelected));
    }
    if (Roster.Count > 0) SelectTarget(Roster[0]);
}
```

#### (D) `HeroVM` 构造函数 — 兵种模板版本（第 58-68 行）

```csharp
public HeroVM(CharacterObject character, Action<HeroVM> onSelect)
{
    Hero = null;
    Character = character ?? throw new ArgumentNullException(nameof(character));
    _heroId = character.StringId ?? string.Empty;
    _heroName = character.Name?.ToString() ?? _heroId;
    _subtitle = character.IsBasicTroop ? "基础兵种" : "升级兵种";
    _isSelected = false;
    _onSelect = onSelect;
}
```

**问题**: `_subtitle` 只区分"基础兵种"/"升级兵种"，`IsBasicTroop` 在新API中可能不可靠。

---

### 文件 2: `Systems\WoW_DefaultPartySpeedCalculatingModel.cs`

#### `GetTroopRoster()` 使用（第 72-83 行）

```csharp
foreach (TroopRosterElement item in mobileParty.MemberRoster.GetTroopRoster())
{
    if (!item.Character.IsHero) { baseNumber = 5; }
    else { baseNumber = this.CalculateBaseSpeedForParty(num4); break; }
}
```

辅助参考：`MobileParty.MemberRoster.GetTroopRoster()` 获取部队中的兵种名册。

---

## 功能模块依赖关系

```
CustomSkillScreenVM 构造函数
├── PopulateRoster()          → Clan.PlayerClan.Heroes → Roster (MBBindingList)
├── PopulateTroopTemplates()  → CultureObject.BasicTroop/EliteBasicTroop → TroopTemplates
├── PopulateLordNPCs()        → Clan.All → Heroes → LordNPCs
├── CreateSkillSlots()        → 8个固定槽位 → Skills
└── BuildAllSkillItemVMs()    → SkillCatalog → _allSkillItemVMs
```

目标类型切换流程：
```
SwitchTargetType(TargetType)
  → GetTargetList(type)
    ├── PartyMember   → Roster
    ├── TroopTemplate → TroopTemplates
    └── LordNPC       → LordNPCs
  → SelectTarget(list[0])
    → LoadSkillsForTarget() + LoadProficiencies()
```

---

## 需修复的问题汇总

| # | 方法 | 当前API | 问题 | 正确API |
|---|------|---------|------|---------|
| 1 | `PopulateTroopTemplates` | `CultureObject.BasicTroop/EliteBasicTroop` | 每个文化最多2个兵种，总共十几条 | `MBObjectManager.Instance.GetObjectTypeList<CharacterObject>()` 过滤 `!IsHero && !IsPlayerCharacter` |
| 2 | `PopulateLordNPCs` | `Clan.All → Heroes` 遍历 | 混入流浪者等非领主 | `Hero.AllAliveHeroes.Where(h => h.IsLord && h.Clan != null)` |
| 3 | `HeroVM(CharacterObject)` | `_subtitle = IsBasicTroop ? "基础兵种" : "升级兵种"` | 信息粒度太粗，IsBasicTroop 可能不可靠 | `_subtitle = $"T{character.Tier} {character.Culture?.StringId}"` |

---

## 未解决问题
无。所有问题已定位，等待用户确认后修改。
