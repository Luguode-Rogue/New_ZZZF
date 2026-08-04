# 数据结构设计

## 1. 世界遗产数据 (LegacyData)

顶层数据容器，代表一次世界导出。

```json
{
  "version": "0.6.0",
  "world_id": "v1.2.10.164250_1689977024",
  "created_at": "2026-07-23T12:00:00",
  "game_version": "v1.2.10.164250",
  "dominant_culture": "empire",
  "kingdoms": [...],
  "clans": [...],
  "settlements": [...]
}
```

> 存储位置：模块根目录（与 `SubModule.xml` 同级）。世界状态写入 `Legacy.json`（覆盖写）。

### 1.1 LegacyData (C#)

```csharp
public class LegacyData
{
    [JsonProperty("version")]     public string Version { get; set; }    // 版本号
    [JsonProperty("world_id")]    public string WorldId { get; set; }    // 世界唯一标识
    [JsonProperty("created_at")]  public string CreatedAt { get; set; }  // 导出时间
    [JsonProperty("game_version")]public string GameVersion { get; set; }// 游戏版本
    [JsonProperty("culture")]     public string DominantCulture { get; set; }
    [JsonProperty("kingdoms")]    public List<KingdomState> Kingdoms { get; set; }
    [JsonProperty("clans")]       public List<ClanState> Clans { get; set; }
    [JsonProperty("settlements")] public List<SettlementState> Settlements { get; set; }
    // 注意：导入时 HeroProfiles 由 LegacyHeroes.json 合并填充（见 §8）
}
```

> 使用 `Newtonsoft.Json` + `[JsonProperty("snake_case")]` 属性标注。

## 2. 王国状态 (KingdomState)

```json
{
  "id": "kingdom_empire",
  "name": "Southern Empire",
  "ruler_clan_id": "clan_rhagaea",
  "culture": "empire"
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 游戏内部 Kingdom.StringId |
| `name` | string | 王国名称 |
| `ruler_clan_id` | string | 统治者家族 ID |
| `culture` | string | 文化（作为备用标记） |

## 3. 家族状态 (ClanState)

```json
{
  "id": "clan_rhagaea",
  "name": "Rhagaea",
  "kingdom_id": "kingdom_empire",
  "tier": 6,
  "gold": 50000,
  "renown": 3500,
  "influence": 1200,
  "is_destroyed": false
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 游戏内部 Clan.StringId |
| `name` | string | 家族名称 |
| `kingdom_id` | string | 所属王国 ID（可为空） |
| `tier` | int | 家族等级 |
| `gold` | long | 金币数量 |
| `renown` | float | 声望值 |
| `influence` | float | 影响力 |
| `is_destroyed` | bool | 是否已被消灭 |

## 4. 定居点状态 (SettlementState)

```json
{
  "id": "town_EP1",
  "name": "Epicrotea",
  "type": "Town",
  "owner_clan_id": "clan_rhagaea",
  "owner_kingdom_id": "kingdom_empire",
  "culture": "empire",
  "prosperity": 4500
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 游戏内部 Settlement.StringId |
| `name` | string | 定居点名称 |
| `type` | string | 类型：Town / Castle / Village |
| `owner_clan_id` | string | 拥有者家族 ID |
| `owner_kingdom_id` | string | 所属王国 ID |
| `culture` | string | 文化类型 |
| `prosperity` | float | 繁荣度 |

## 5. XML 设置数据 (LegacyWorldSettingsData)

持久化在 `Modules\New_ZZZF\Settings\LegacyWorldSettings.xml`。

```csharp
[XmlRoot("LegacyWorldSettings")]
public class LegacyWorldSettingsData
{
    public bool Enabled = true;             // 主开关
    public bool AutoExportOnSave = true;    // 存档自动导出
    public bool LogEnabled = true;          // 调试日志

    // 导入类别开关
    public bool RestoreKingdoms = true;     // 恢复王国结构
    public bool RestoreClans = true;        // 恢复家族数据
    public bool RestoreSettlements = true;  // 恢复领地所有权
    public bool RestoreClanEconomy = true;  // 恢复家族经济
    public bool CreateMissingClans = false; // 创建缺失家族
}
```

### 默认配置 XML

```xml
<?xml version="1.0"?>
<LegacyWorldSettings>
  <Enabled>true</Enabled>
  <AutoExportOnSave>true</AutoExportOnSave>
  <LogEnabled>true</LogEnabled>
  <RestoreKingdoms>true</RestoreKingdoms>
  <RestoreClans>true</RestoreClans>
  <RestoreSettlements>true</RestoreSettlements>
  <RestoreClanEconomy>true</RestoreClanEconomy>
  <CreateMissingClans>false</CreateMissingClans>
</LegacyWorldSettings>
```

## 6. IGameAdapter 接口定义

```csharp
public interface IGameAdapter
{
    // 世界信息
    string GetWorldId();
    string GetCurrentGameTime();
    string GetDominantCulture();
    string GetGameVersion();

    // 王国操作
    IEnumerable<IKingdomInfo> GetAllKingdoms();
    IKingdomInfo FindKingdom(string id);
    void SetKingdomRuler(IKingdomInfo kingdom, IClanInfo ruler);

    // 家族操作
    IEnumerable<IClanInfo> GetAllClans();
    IClanInfo FindClan(string id);
    IClanInfo CreateClan(string id, string name, string culture);
    void SetClanKingdom(IClanInfo clan, IKingdomInfo kingdom);
    void SetClanGold(IClanInfo clan, long gold);
    void SetClanRenown(IClanInfo clan, float renown);
    void SetClanInfluence(IClanInfo clan, float influence);

    // 定居点操作
    IEnumerable<ISettlementInfo> GetAllSettlements();
    ISettlementInfo FindSettlement(string id);
    void ChangeSettlementOwner(ISettlementInfo settlement, IClanInfo newOwner);
    void SetSettlementProsperity(ISettlementInfo settlement, float prosperity);
}
```

## 7. 核心枚举

### 导入结果 (ImportResult)

```csharp
public class ImportResult
{
    public int KingdomsRestored { get; set; }
    public int ClansRestored { get; set; }
    public int SettlementsRestored { get; set; }
    public int HeroesResurrected { get; set; }   // 新建的游荡英雄数
}
```

## 8. 玩家人物遗产 (LegacyHeroes.json)

v0.6.0 起，玩家人物模板独立存储于 `LegacyHeroes.json`（与 `Legacy.json` 同目录），采用**累积写**，
形成跨世界遗产链：A 世界导出 → B 世界导出（保留 A）→ C 世界导入时同时拿到 A 与 B 的遗留人物。

```json
{
  "version": "0.6.0",
  "profiles": [
    {
      "source": "player",
      "world_id": "v1.2.10.164250_1689977024",
      "name": "张三",
      "culture_id": "empire",
      "level": 32,
      "skills": { "OneHanded": 120, "Leadership": 100 },
      "traits": { "Generous": 1 },
      "age": 36,
      "occupation": "Wanderer",
      "gender": "Male",
      "body_properties": "..."
    },
    {
      "source": "companion",
      "world_id": "v1.2.10.164250_1689977024",
      "name": "李四",
      "culture_id": "empire",
      ...
    }
  ],
  "applied_world_ids": ["v1.2.10.164250_1689977024"]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `version` | string | 数据版本 |
| `profiles` | List\<HeroProfile\> | 累积的玩家人物模板（跨世界） |
| `applied_world_ids` | List\<string\> | 已复刻过的遗产来源 WorldId，持久化防重复 |

### 8.1 HeroProfile (C#)

```csharp
public class HeroProfile
{
    [JsonProperty("source")]      public string Source { get; set; }   // player / companion / wanderer ...
    [JsonProperty("world_id")]    public string WorldId { get; set; }  // 模板所属存档（用于区分同存档/跨存档）
    [JsonProperty("name")]        public string Name { get; set; }
    [JsonProperty("culture_id")]  public string CultureId { get; set; }
    [JsonProperty("level")]       public int Level { get; set; }
    [JsonProperty("skills")]      public Dictionary<string,int> Skills { get; set; }
    [JsonProperty("traits")]      public Dictionary<string,int> Traits { get; set; }
    [JsonProperty("age")]         public int Age { get; set; }
    [JsonProperty("occupation")]  public string Occupation { get; set; }
    [JsonProperty("gender")]      public string Gender { get; set; }
    [JsonProperty("body_properties")] public string BodyProperties { get; set; }
}
```

> `WorldId` 是 v0.6.0 新增：防本存档二重身依赖它——仅当「当前 WorldId == 遗产 WorldId」时才判断模板是否指向当前存活的自己/队友。

### 8.2 去重与防二重身规则

- **累积去重**：导出时按 `(WorldId + Name + Source)` 去重，避免同一存档重复追加。
- **防本存档二重身**：导入时 `player`/`companion` 模板若当前 WorldId == 遗产 WorldId 且命中存活的自己/队友，则跳过（禁止在本存档生成二重身）。
- **跨存档放行**：当前 WorldId ≠ 遗产 WorldId（如 A 导出、B 导入）即使姓名雷同也放行，实现"遇到原来的自己"。
- **持久化防重复**：`applied_world_ids` 记录已复刻世界，重开游戏后再导入也不会重复复刻。

