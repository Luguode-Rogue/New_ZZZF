# 数据结构设计

## 1. 世界遗产数据 (LegacyData)

顶层数据容器，代表一次世界导出。

```json
{
  "version": "0.4.0",
  "world_id": "v1.2.10.164250_1689977024",
  "created_at": "2026-07-23T12:00:00",
  "game_version": "v1.2.10.164250",
  "dominant_culture": "empire",
  "kingdoms": [...],
  "clans": [...],
  "settlements": [...]
}
```

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
}
```
