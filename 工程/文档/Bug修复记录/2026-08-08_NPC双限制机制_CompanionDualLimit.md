# NPC 双限制机制（CompanionDualLimit）实施计划

## 背景
原生 Bannerlord 用 `Clan.CompanionLimit`（`DefaultClanTierModel`：`tier+3` + 2 个 perk）限制家族**拥有**的同伴数，
且仅统计 `Clan.Companions`（流浪者同伴），**家人（Clan.Heroes / AliveLords）不计入**。
需求：改为「跟随上限 + 拥有上限」双限制，并让家人也纳入统计。

## 数值规则（已与用户确认）
令 `perk = 拥有 WePledgeOurSwords(统御) + Camaraderie(魅力) 的数量（0/1/2）`
令 `tier = 家族等级`

- **携带上限（跟随数量）** = `3 + perk + tier`
- **拥有上限（家族总数量）** = `(3 + perk + tier) × (3 + perk)`
- 每个相关 perk 同时：+1 携带、+1 倍率（即拥有上限乘数中 +1）
- 两上限均随家族等级提升

示例（tier=0）：
- 0 perk：携带 3，拥有 3×3=9
- 1 perk：携带 4，拥有 4×4=16
- 2 perk：携带 5，拥有 5×5=25

## 统计口径（原生已支持，无需自研）
- 拥有数 = `Clan.PlayerClan.Companions.Count + Clan.PlayerClan.Heroes.Count`（同伴 + 家人）
- 跟随数 = 上述集合中 `hero.PartyBelongedTo == MobileParty.MainParty` 的数量
- 关键原生 API：`Hero.PartyBelongedTo`、`MobileParty.MainParty`、`Clan.Companions`、`Clan.Heroes`

## 代码结构（独立解耦，便于抽成新 mod）
文件夹：`New_ZZZF/工程/New_ZZZF/CompanionDualLimit/`
1. `ZZZFClanTierModel.cs`
   - 继承 `TaleWorlds.CampaignSystem.ClanTierModel`
   - override `GetCompanionLimit(Clan)` → 返回**拥有上限**（新公式）
   - 新增 `GetFollowLimit(Clan)` → 返回**携带上限**
   - perk 判定沿用原生 `WePledgeOurSwords` / `Camaraderie`（参考 DefaultClanTierModel 的 GetPerkEnabled 逻辑）
2. `NpcLimitHelper.cs`
   - 静态方法：`GetOwnedCount(Clan)`、`GetFollowingCount(Clan)`
   - 家人纳入：合并 Companions + Heroes
3. `NpcLimitBehavior.cs`（实现 `ICampaignBehavior`）
   - 接入校验点，做「拥有<拥有上限 且 跟随<携带上限」双判断：
     - 招募对话（`LordConversationsCampaignBehavior` 招募分支）
     - 任务前置（`LordsNeedsTutorIssueBehavior` 等）
     - 属性重置警告（`PerkResetCampaignBehavior`）
   - 区分超限原因，给出不同提示文本
4. `SubModule.cs`（模组入口）
   - 注册 `ZZZFClanTierModel` 替换原生 `DefaultClanTierModel`
   - 注册 `NpcLimitBehavior`

## UI 微调（家族界面 ClanManagement）
原生位置：`TaleWorlds.CampaignSystem.ViewModelCollection/.../ClanManagement/Categories/ClanMembersVM.cs`
- `FamilyText`（家人框标题，原第 88-90 行）：改为显示「当前已有 NPC 数量 / NPC 上限」
  → `拥有数 / 拥有上限`
- `CompanionsText`（同伴框标题，原第 91-95 行）：改为显示「当前队伍 NPC 数量 / 队伍 NPC 上限」
  → `跟随数 / 携带上限`

模组侧以**继承/替换 VM 或 Harmony Postfix** 介入，避免直接改原生文件。
新增游戏文本放 `ModuleData/languages/CNs/`：
- `str_clan_owned_npc_count_limit`（家人框）
- `str_clan_follow_npc_count_limit`（同伴框）

## 待用户确认的小口径
- UI「家人框显示已有npc数量/上限」按"家人+同伴合并总拥有"理解；若只想显示家人部分请纠正。

## 实施顺序
1. 建文件夹与 `ZZZFClanTierModel`（双上限计算）
2. `NpcLimitHelper`（统计，含家人）
3. `NpcLimitBehavior`（校验点接入）
4. `SubModule` 注册
5. UI 微调 + 文本
6. 自测：招募、跟随/驻守切换、属性重置、家族升级

---

## 后续补充需求与修复（已实现）

### A. 重复计算 bug 修复（NpcLimitHelper）
- **问题**：`Clan.Heroes` 与 `Clan.Companions` 有重叠（Companions 是 Heroes 的子集，实测 clan.Heroes=22 / clan.Companions=6），原统计逻辑导致家族 NPC 被重复计数 → 拥有上限误判已满。
- **修复**：`GetAllLimitedHeroes`（供 GetOwnedCount/GetFollowingCount 使用）先建 `HashSet<Hero> companions` 收集同伴，家人数侧加 `&& !companions.Contains(hero)` 去重后再 yield。
- **状态**：已修复，编译通过。

### B. 超上限拦截招募（用户明确要求"超上限就直接不招募，弹提示，不进队"）
- **最初错误方案（已弃用）**：Postfix 监听 `AddHeroToPartyAction.Apply`，英雄进队后再 `TeleportHeroAction` 传送走。问题：①属于"事后"，英雄已进队；②且 `AddCompanionAction` 已先执行 → 英雄变成"拥有但不跟随"的虚空同伴、凭空消失。
- **源码验证（骑砍2源码）**：`LordConversationsCampaignBehavior.conversation_companion_hire_on_consequence`（第2837-2842行）调用顺序：
  ```
  GiveGoldAction.ApplyBetweenCharacters(...)      // 扣钱
  AddCompanionAction.Apply(Clan.PlayerClan, hero) // 加为家族同伴
  AddHeroToPartyAction.Apply(hero, MainParty, true) // 进队伍
  ```
  原生 `too_many_companions()`（第104-107行）只看"拥有上限"，不看跟随上限。
- **最终方案（双层 + 兜底，已实现）**：
  1. **显示层**：`NpcLimitRecruitPatch.TooManyCompanionsPrefix` 已把 `too_many_companions` 替换为双判断（`GetRecruitBlockReason` 含拥有+跟随），超上限时招募选项"Right... Here you are"不出现。
  2. **源头拦截（关键新增）**：`NpcLimitRecruitPatch.CompanionHireConsequencePrefix` 给 `conversation_companion_hire_on_consequence` 加 Prefix，`return false` 跳过整段 consequence（扣钱+加同伴+进队全不做），英雄留在酒馆，不出现虚空状态。
  3. **兜底**：`NpcLimitEnforceBehavior` 对 `AddHeroToPartyAction.Apply` 加 Prefix，覆盖**其他进队入口**（任务随从、召同伴进队等）超跟随上限的拦截；只对 `party == MobileParty.MainParty && GetFollowingCount >= followLimit` 拦截，正常召同伴/未超上限一律放行。
- **状态**：已编码，编译通过，待实测（超上限酒馆招募应英雄留原地、弹红/橙提示；家族拥有数、队伍跟随数均不涨）。

### C. 文件清单（CompanionDualLimit/）
- `ZZZFClanTierModel.cs` — 双上限公式
- `NpcLimitHelper.cs` — 统计（含家人 + HashSet 去重）
- `NpcLimitBehavior.cs` — `GetModel()`(internal static)、`GetRecruitBlockReason()`、`GetFollowLimit/GetFollowingCount` 等
- `NpcLimitRecruitPatch.cs` — `too_many_companions` Prefix + `conversation_companion_hire_on_consequence` Prefix（源头拦截）
- `NpcLimitEnforceBehavior.cs` — `AddHeroToPartyAction.Apply` Prefix（兜底拦截）

### D. 已知 API 约束（此游戏版本，已通过反射/源码确认）
- 无 `Hero.ChangePartyOwner`、无 `Hero.PartyBelongedTo` setter、无 `Clan.HeroesChangedInClan` 事件
- `AddHeroToPartyAction.Apply(Hero, MobileParty, Boolean)` 存在（所有进队入口走此）
- `AddCompanionAction.Apply(Clan, Hero)` 存在（仅加为家族同伴，不带进队伍）
- `SubModule.cs` 已有 `new Harmony("New_ZZZF").PatchAll(...)`，`[HarmonyPatch]` 静态类自动生效（无需 AddBehavior 实例化静态类）

