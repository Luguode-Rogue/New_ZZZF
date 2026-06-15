# WoW (战争之风) 技术实现参考手册

> 来源: `C:\Users\42029\CodeBuddy\旧版本更新代码\WoW`
> 版本: v0.0.1
> 用途: 已合入 New_ZZZF，保留本文档作为技术实现参考

---

## 一、项目概览

| 项 | 值 |
|---|---|
| **Mod ID** | `WoW` (War of the Wind / 战争之风) |
| **定位** | RPG化战斗全面改造 |
| **核心功能** | 技能系统、枪械系统、自瞄、RPG伤害、大地图移速 |
| **技术栈** | GameModel覆写 / Harmony补丁 / 自定义投射物 / GameEntity / 弹道计算 |
| **代码规模** | ~10个核心类，Scripts.cs(2084行) + missionSetting.cs(1536行) |

---

## 二、文件结构及职责

```
WoW/
├── SubModule.xml
├── GUI/Prefabs/
│   └── WoW_AgentStatus.xml       # 自定义HUD布局 (体力/法力/弹药)
├── ModuleData/
│   ├── combat_parameters.xml     # 战斗参数 (109KB)
│   ├── crafting_templates.xslt   # 锻造模板
│   ├── item_usage_sets.xslt      # 物品使用集合
│   ├── managed_core_parameters.xml
│   ├── native_parameters.xml
│   └── weapon_descriptions.xslt
└── 工程/WoW/WoW/
    ├── SubModule.cs              # 入口: Model注册 + Harmony + MissionBehavior
    ├── missionSetting.cs         # 战斗Tick核心 (1536行)
    ├── Scripts.cs                # 功能实现库 (2084行) + 自定义Model类
    ├── ExtendedData.cs           # 数据结构定义 (AgentExtend/TroopSkillData/GunData)
    ├── Harmony.cs                # 坐骑补丁 (Agent.Mount)
    ├── TroopSkill.cs             # 测试兵种技能硬编码
    ├── WoW_DefaultPartySpeedCalculatingModel.cs  # 大地图移速
    ├── WoW_HideoutCampaignBehavior.cs            # 藏身处难度
    ├── WoW_DefaultClanTierModel.cs               # 家族同伴翻倍
    └── WoW_MainAgentStatus.cs    # 自定义HUD ViewModel
```

---

## 三、技术实现详解

### 3.1 GameModel 覆写系统

WoW 的核心技术手段之一：**覆写骑砍2原版游戏模型**。

#### 注册方式
```csharp
// SubModule.cs → InitializeGameStarter
public override void InitializeGameStarter(Game game, IGameStarter gameStarterObject)
{
    // 通用模式 (CustomBattle + Campaign)
    gameStarterObject.AddModel(new WOW_DefaultStrikeMagnitudeModel());     // 打击强度
    gameStarterObject.AddModel(new WOW_CustomBattleAgentStatCalculateModel()); // 属性计算
    gameStarterObject.AddModel(new WOW_CustomAgentApplyDamageModel());    // 伤害应用
    gameStarterObject.AddModel(new WOW_DefaultRidingModel());             // 骑术
    gameStarterObject.AddModel(new WOW_DefaultPartySpeedCalculatingModel()); // 移速

    // 仅战役模式
    if (game.GameType is Campaign)
    {
        gameStarterObject.AddModel(new WoW_DefaultClanTierModel());
        gameStarterObject.AddModel(new WOW_SandboxAgentApplyDamageModel());
        gameStarterObject.AddModel(new WOW_SandboxStrikeMagnitudeModel());
        gameStarterObject.AddModel(new WOW_SandboxAgentStatCalculateModel());
    }

    new Harmony("WoW").PatchAll(Assembly.GetExecutingAssembly());
}
```

**关键API**: `gameStarterObject.AddModel(new CustomModel())` — 注册自定义模型，替换原版

#### 覆写伤害计算模型
```csharp
// NewDamageModel → WOW_DefaultStrikeMagnitudeModel
public class WOW_DefaultStrikeMagnitudeModel : DefaultStrikeMagnitudeModel
{
    public override float CalculateStrikeMagnitudeForThrust(
        WeaponComponentData weapon, float thrustSpeed, float armor, ...)
    {
        float num = base.CalculateStrikeMagnitudeForThrust(...);
        // 保底伤害：不低于武器的面板戳刺伤害
        num = MathF.Max(num, weapon.GetModifiedThrustDamageForCurrentUsage());
        return num;
    }

    public override float CalculateStrikeMagnitudeForSwing(
        WeaponComponentData weapon, float swingSpeed, float armor,
        float impactPointAsPercent, ...)
    {
        // 移除打击点位影响，用 SweetSpotReach 替代
        impactPointAsPercent = weapon.SweetSpotReach;
        return base.CalculateStrikeMagnitudeForSwing(...);
    }
}
```

**关键API**:
- `WeaponComponentData.GetModifiedThrustDamageForCurrentUsage()` — 武器面板戳刺伤害
- `WeaponComponentData.SweetSpotReach` — 武器最佳打击点距离分数

#### 覆写伤害应用模型
```csharp
// WOW_CustomAgentApplyDamageModel
public override float CalculateDamage(
    float baseDamage, float armorAmount, ...)
{
    // 护甲 → 固定数值减伤（而非原版的百分比）
    float finalDamage = baseDamage - armorAmount * 0.1f;
    return MathF.Max(finalDamage, 1f);  // 最小1点伤害
}

public override bool DecideCrushedThrough(
    Agent attacker, Agent defender, float totalAttackEnergy,
    Agent.UsageDirection attackDirection, StrikeType strikeType,
    ItemObject defendItem, bool isPassiveUsage)
{
    // 戳刺 + 目标没举盾 = 直接突破格挡
    if (strikeType == StrikeType.Thrust && !defendItem.IsShield)
        return true;
    return base.DecideCrushedThrough(...);
}
```

#### 覆写大地图移速模型
```csharp
// WoW_DefaultPartySpeedCalculatingModel
public override float CalculateBaseSpeedForParty(MobileParty party, ...)
{
    float baseSpeed = 7.5f;  // 原版 5.0

    // 无英雄领导的队伍保持原版速度
    if (party.LeaderHero == null) return 5.0f;

    // 移除部队人数对基础速度的影响
    // 最低速度提升到 3.0
    // 牧羊人perk完全消除牲畜减速
    // ...
}
```

---

### 3.2 战斗核心驱动 (missionSetting.cs, 1536行)

#### 3.2.1 全局状态字典
```csharp
public static Dictionary<int, AgentExtend> WoW_Agents;              // agent.Index → 扩展数据
public static List<int> WoW_MissileIndex;                            // 自定义投射物追踪
public static Dictionary<int, int> WoW_WeaponMissile;               // 投射物索引 → 武器伤害
public static Dictionary<int, Agent> WoW_AgentRushAgent;            // 冲刺: 施法者 → 目标
public static Dictionary<int, Vec3> WoW_AgentRushPos;               // 翻滚: 施法者 → 目标位置
public static Dictionary<int, Agent> WoW_SmartMisslie;              // 制导投射物 → 追踪目标
public static List<GameEntity> WoW_CustomGameEntity;                // 技能特效实体
public static Dictionary<GameEntity, ProjectileData> WoW_ProjectileDB; // 特效实体 → 弹道数据
```

#### 3.2.2 OnMissionTick 每帧流程 (~1500行)

```
OnMissionTick(float dt)
├── 阶段A: 调试热键
│   ├── InputKey.Up → 冻结所有敌军AI + 施加伤害
│   ├── InputKey.Down → 切换描边总开关
│   ├── InputKey.Right → 丢弃非长杆/盾牌武器
│   ├── InputKey.Numpad7 → 快进开关
│   └── InputKey.Z → 灵马哨笛测试
├── 阶段B: 速度刷新
│   └── 为每个Agent计算速度向量 (差分 Position)
├── 阶段C: 自动武器射击系统
│   ├── 玩家 (agent.IsHero + Agent.Main): 右键按住自动开火
│   └── AI (非玩家): 瞄准状态概率开火
├── 阶段D: 弹药共享
│   └── 弹药≤3时从同编队队友抢夺同类型弹药
├── 阶段E: 自瞄系统
│   └── 方向键↓切换，选择最佳目标
├── 阶段F: 技能冷却更新
│   └── AgentSkillComponent 每0.5秒减1冷却
├── 阶段G: 制导投射物追踪
│   └── 通过旋转矩阵插值使投射物转向目标
├── 阶段H: 自定义GameEntity生命周期
│   └── 超时清理
└── 阶段I: 描边刷新
    └── 每0.5秒通过 SetContourColor 刷新
```

---

### 3.3 枪械系统

#### 3.3.1 武器→枪械映射
```csharp
// 基于手上的 WeaponClass 确定枪械类型
// Bow → TUJIBUQIANG (突击步枪) RPM=600, Clip=30, Reload=5s
// Crossbow → JINGZHUNBUQIANG (精准步枪) RPM=20, Clip=15, Reload=8s
// Consumable + Ranged → XIANDANQIANG (霰弹枪) RPM=45, Clip=10, Reload=8s
```

#### 3.3.2 自动射击实现
```csharp
// Scripts.cs
public static bool AgentShootTowardsLookDirection(
    Agent agent, EquipmentIndex weaponIndex, float damageFactor,
    bool autoAim, int bulletsPerShot, float spreadAngle)
{
    // 1. 获取武器和弹药
    WeaponComponentData weapon = agent.Equipment[weaponIndex].Item.PrimaryWeapon;
    WeaponComponentData ammo = agent.Equipment[weaponIndex].Item.AmmoWeapon;

    // 2. 计算射击方向 (自瞄模式下修正)
    Vec3 direction = autoAim ? CalculateAimDirection(agent, target)
                             : agent.LookDirection;

    // 3. 散布
    direction = ApplySpread(direction, spreadAngle);

    // 4. 通过反射调用 Agent.AgentFireWeaponAtPosition
    // 或使用 Mission.AddCustomMissile
}
```

**关键API**:
- `agent.Equipment[equipmentIndex].Item.PrimaryWeapon` — 获取主武器数据
- `agent.Equipment[weaponIndex].Item.AmmoWeapon` — 获取弹药数据
- `Mission.AddCustomMissile(agent, weapon, position, direction, speed, ...)` — 发射自定义投射物

---

### 3.4 弹药共享系统

```csharp
// 当 agent 弹药 ≤ 3，从同编队队友抢夺
public void OnAgentShootMissile(Agent shooter, EquipmentIndex weaponIndex, Vec3 position, ...)
{
    int ammoCount = GetAmmoCount(shooter);
    if (ammoCount <= 3)
    {
        foreach (Agent teammate in GetTeammates(shooter))
        {
            if (HasMatchingAmmo(teammate, shooter))
            {
                StealAmmo(teammate, shooter, 1);  // 偷1发弹药
                break;
            }
        }
    }
}
```

**关键API**: `agent.Equipment[slot].Amount` — 弹药数量（可读写）

---

### 3.5 自瞄系统

#### 3.5.1 主逻辑
```csharp
// Scripts.cs — AimShoot
public static Agent AimShoot(Mission mission, Agent shooter)
{
    // 1. 获取所有敌人
    List<Agent> enemies = GetEnemyAgents(mission, shooter.Team);

    // 2. 过滤：在视线内、可被瞄准
    enemies = enemies.Where(e => CanSeeAgent(shooter, e)).ToList();

    // 3. 优先级排序
    //    优先级1: 无盾牌或未举盾
    //    优先级2: 不在队友/障碍物后方
    //    优先级3: 距离最近
    enemies.Sort(...);

    return enemies.FirstOrDefault();
}
```

#### 3.5.2 AgentShotAgent — 弹道修正射击
```csharp
public static void AgentShotAgent(Agent shooter, Agent target,
    EquipmentIndex weaponIndex, float baseSpeed, out Vec3 targetPos)
{
    // 1. 计算飞行时间
    float dist = (target.Position - shooter.Position).Length;
    float flightTime = dist / baseSpeed;

    // 2. 预测目标位置 (基于目标当前速度)
    Vec3 predictedPos = target.Position + target.Velocity * flightTime;

    // 3. 迭代收敛 (最多5次)
    for (int i = 0; i < 5; i++)
    {
        flightTime = CalculateFlightTime(shooter.Position, predictedPos, baseSpeed);
        predictedPos = target.Position + target.Velocity * flightTime;
    }

    // 4. 解二次方程求发射角
    targetPos = CalculateProjectileFiringSolution(
        shooter.Position, predictedPos, baseSpeed, Gravity);
}
```

---

### 3.6 弹道计算

#### 3.6.1 抛物线发射解算
```csharp
// Scripts.cs — CalculateProjectileFiringSolution
public static Vec3 CalculateProjectileFiringSolution(
    Vec3 start, Vec3 target, float speed, float gravity = 9.806f)
{
    Vec3 horizontal = new Vec3(target.X - start.X, 0, target.Y - start.Y);
    float range = horizontal.Length;
    float height = target.Z - start.Z;

    // 解二次方程: v₀² · sin²θ ± ...
    float v2 = speed * speed;
    float v4 = v2 * v2;
    float discriminant = v4 - gravity * (gravity * range * range + 2 * height * v2);

    if (discriminant < 0)
        return Vec3.Invalid;  // 超出射程

    float angle = MathF.Atan2(v2 - MathF.Sqrt(discriminant), gravity * range);
    Vec3 dir = horizontal.NormalizedCopy();
    dir.z = MathF.Tan(-angle);  // 游戏坐标系Z向上
    return dir.NormalizedCopy();
}
```

#### 3.6.2 弹道显示
```csharp
// dandaoxianshi() — 绘制弹道轨迹
public static void dandaoxianshi(Mission mission, Vec3 start, Vec3 direction,
    float speed, float lifetime = 3f)
{
    // 用 DebugLine 或 GameEntity 逐点绘制
    for (float t = 0; t < flightTime; t += 0.05f)
    {
        Vec3 pos = CalculatePositionAtTime(start, direction, speed, t);
        // 绘制线段或点
    }
}

// CalculatePositionAtTime — 含空气阻力
public static Vec3 CalculatePositionAtTime(
    Vec3 start, Vec3 direction, float speed, float time,
    float friction = 0.002f)
{
    float vx = direction.X * speed;
    float vy = direction.Y * speed;
    float vz = direction.Z * speed;

    // 欧拉积分 (每步 dt=0.01)
    // 空气阻力: a = -friction * v²
    // 重力: a_z -= gravity
    // ...
    return position;
}
```

**物理常量**: `Gravity = 9.806f` (与 `MBGlobals.Gravity` 一致)

---

### 3.7 技能系统

#### 3.7.1 数据结构
```csharp
// ExtendedData.cs
public class AgentExtend
{
    public float Stamina;        // 体力
    public float Mana;           // 法力
    public float CD;             // 冷却
    public float Dur;            // 持续时间

    // 7个技能槽位
    public string primarySkill;
    public string additionalSkill;
    public string passiveSkill;
    public string MagicCombatSkill1;
    public string MagicCombatSkill2;
    public string MagicCombatSkill3;
    public string MagicCombatSkill4;
    public string heavyStrikeSkill;

    // 枪械属性
    public GunData Gun;
    public int ClipRemaininAmmunition;
    public float ShootCD;
    public float ReloadCD;
}

public class TroopSkillData
{
    public string TroopId;
    public string PrimarySkill;
    public string AdditionalSkill;
    public string PassiveSkill;
    public List<string> MagicCombatSkills;  // 4个
    public string HeavyStrikeSkill;
}

public class GunData
{
    public string id;         // TUJIBUQIANG / JINGZHUNBUQIANG / XIANDANQIANG
    public int RPM;           // 射速 (发/分钟)
    public int Clip;          // 弹匣容量
    public float Reload;      // 换弹时间 (秒)
}
```

#### 3.7.2 技能触发
```csharp
// 玩家输入 → 技能
// 左Alt → primarySkill (主动技能)
// 左Ctrl → additionalSkill (副主动技能)
// Q 键 → 切换法术槽 (1→2→3→4→循环)
// 左Ctrl + Q → 施放当前法术 (MagicCombatSkillN)
// Z 键 → heavyStrikeSkill (重击技能)

// AI 自动释放: 随机选择4个法术之一
```

#### 3.7.3 典型技能实现

**刀扇 (DaoShan)**:
```csharp
public static void DaoShan(Mission mission, Agent caster)
{
    // 360度发射多个投射物
    for (int i = 0; i < 8; i++)
    {
        float angle = i * MathF.PI / 4;
        Vec3 dir = new Vec3(MathF.Cos(angle), MathF.Sin(angle), 0);
        Mission.AddCustomMissile(caster, weapon, caster.Position, dir, ...);
    }
}
```

**王之宝库 (WangZhiBaoKu)**:
```csharp
public static void WangZhiBaoKu(Mission mission, Agent caster)
{
    for (int i = 0; i < 30; i++)
    {
        // 随机位置在施法者周围生成
        Vec3 spawnPos = caster.Position + RandomVec3() * 3f;

        // 每个投射物自动寻找目标
        Agent target = FindNearestEnemy(mission, caster, spawnPos);

        // 创建制导投射物 (GameEntity + ProjectileData)
        GameEntity entity = GameEntity.CreateEmpty(mission.Scene, true);
        ProjectileData data = new ProjectileData
        {
            caster = caster,
            target = target,
            lifetime = 5f,
            trackingRate = 0.1f  // 转向率
        };
        WoW_ProjectileDB[entity] = data;
    }
}
```

**闪电术 (LightningBolt)**:
```csharp
public static void LightningBolt(Mission mission, Agent caster)
{
    Vec3 pos = caster.Position;
    Vec3 dir = caster.LookDirection;

    // 沿光线50米方向，每1米检测
    for (float dist = 0; dist < 50; dist += 1f)
    {
        pos += dir * 1f;

        // 5米半径AOE
        foreach (Agent target in FindAgentsInRange(pos, 5f))
        {
            ApplyDamage(caster, target, 20f);  // 固定20伤害
        }
    }

    // 生成可视化弹道实体 (飞向施法者，带追踪效果)
}
```

**翻滚 (AgentRoll)**:
```csharp
public static void AgentRoll(Mission mission, Agent agent, Vec3 direction)
{
    // 射线检测障碍物
    float maxDist = 5f;
    Vec3 end = agent.Position + direction * maxDist;
    // RayCastForClosestAgent/碰撞检测...

    // 播放动作 (从30%进度开始播放)
    agent.SetActionChannel(0, ActionIndexCache.Create("act_horse_fall_roll"));
    agent.SetActionProgress(0, 0.3f);

    // 注册平滑移动
    WoW_AgentRushPos[agent.Index] = end;
}
```

**灵马哨笛 (lingmashaodi)**:
```csharp
public static void lingmashaodi(Mission mission, Agent agent)
{
    if (!agent.MountAgent)  // 无马 → 召唤
    {
        // 凭空生成一匹马
        Horse horse = SpawnMount(agent.Position, agent.LookDirection);
        agent.Mount(horse);  // 骑上去
    }
    else  // 有马 → 遣散
    {
        agent.MountAgent.SetActionChannel(0,
            ActionIndexCache.Create("act_horse_rear"));
        agent.MountAgent.FadeOut(false, true);
    }
}
```

---

### 3.8 Harmony 补丁 — 灵马哨笛坐骑绕过

```csharp
// Harmony.cs
[HarmonyPatch(typeof(Agent), "Mount")]
public class MountPatch
{
    static bool Prefix(Agent __instance, Agent mountAgent)
    {
        var extend = WoW_MissionSetting.WoW_Agents[__instance.Index];
        if (extend.heavyStrikeSkillDur > 0)
        {
            // 强制上马/下马，绕过骑术检查
            Traverse.Create(__instance)
                .Property("MountAgent")
                .SetValue(mountAgent);
            return false;  // 跳过原版方法
        }
        return true;  // 正常执行原版
    }
}
```

**关键技巧**:
- `Traverse` — Harmony 的反射辅助类，比原生反射更方便
- `return false` — 跳过原版方法执行

---

### 3.9 自定义 HUD

#### 3.9.1 XML 布局
```xml
<!-- GUI/Prefabs/WoW_AgentStatus.xml -->
<Prefab>
  <Window>
    <Widget Id="AgentStatusWidget" ...>
      <!-- 体力条 -->
      <Widget Id="StaminaBar" ... DataSource="{Stamina}" />
      <!-- 法力条 -->
      <Widget Id="ManaBar" ... DataSource="{Mana}" />
      <!-- 法术充能 -->
      <Widget Id="SpellCharge" ... DataSource="{SpellChargeDur}" />
      <!-- 弹药 -->
      <Widget Id="AmmoText" ... DataSource="{ClipRemaininAmmunition}" />
      <!-- 弹匣 -->
      <Widget Id="ClipText" ... DataSource="{Clip}" />
    </Widget>
  </Window>
</Prefab>
```

#### 3.9.2 ViewModel
```csharp
// WoW_MainAgentStatus.cs
public class WoW_MainAgentStatusVM : ViewModel
{
    [DataSourceProperty] public float Stamina { get; set; }
    [DataSourceProperty] public float Mana { get; set; }
    [DataSourceProperty] public float SpellChargeDur { get; set; }
    [DataSourceProperty] public int Clip { get; set; }
    [DataSourceProperty] public int ClipRemaininAmmunition { get; set; }

    public void UpdateAgentStatuses(AgentExtend data)
    {
        Stamina = data.Stamina;
        Mana = data.Mana;
        ClipRemaininAmmunition = data.ClipRemaininAmmunition;
        // ...
    }
}
```

#### 3.9.3 加载方式
```csharp
// missionSetting.cs → OnMissionBehaviorInitialize
GauntletLayer layer = new GauntletLayer(100, "GauntletLayer");
_viewModel = new WoW_MainAgentStatusVM();
_gauntletMovie = layer.LoadMovie("WoW_AgentStatus", _viewModel);
missionScreen.AddLayer(layer);
```

---

### 3.10 描边系统

```csharp
// 每 0.5 秒刷新一次
if (mission.CurrentTime - _lastOutlineTime > 0.5f)
{
    foreach (Agent agent in Mission.Agents)
    {
        if (agent.Team == Mission.PlayerTeam)
            agent.AgentVisuals.SetContourColor(new uint?(0xFF00FF00));  // 友军绿色
        else
            agent.AgentVisuals.SetContourColor(new uint?(0xFFFF0000));  // 敌军红色
    }
    _lastOutlineTime = mission.CurrentTime;
}
```

**关键API**: `agent.AgentVisuals.SetContourColor(uint?)` — 设置角色描边颜色

---

### 3.11 制导投射物追踪

```csharp
// OnMissionTick 每帧更新
foreach (var kv in WoW_SmartMisslie)
{
    Missile missile = Mission.MissilesList[kv.Key];  // 注意: 旧版 Mission.Missiles
    Agent target = kv.Value;

    // 计算从当前方向到目标方向的旋转矩阵
    Vec3 currentDir = missile.GetVelocity().NormalizedCopy();
    Vec3 desiredDir = (target.Position - missile.GetPosition()).NormalizedCopy();

    // 插值转向 (trackingRate 控制转向速度)
    Vec3 newDir = Vec3.Lerp(currentDir, desiredDir, trackingRate);

    // 更新导弹速度方向
    missile.SetVelocity(newDir * missile.GetVelocity().Length);
}
```

**注意**: `Mission.Missiles` 在新版骑砍2中已改名为 `Mission.MissilesList`。

---

### 3.12 大地图移速模型
```csharp
public override float CalculateBaseSpeedForParty(
    MobileParty party, StatExplainer explainer, float baseSpeed)
{
    // 无效队伍返回 0
    if (!party.IsActive || party.Party.SizeLimit <= 0)
        return 0f;

    // 计算物品负重
    ExplainedNumber finalSpeed = CalculatePartyWeight(party, ...);

    // 基础速度 7.5
    finalSpeed.Add(7.5f, null);

    // 英雄部队加速
    if (party.LeaderHero != null)
    {
        // 移除部队规模惩罚
        // 骑兵加成
        // 步兵/弓兵惩罚
        // 囚犯惩罚
    }
    else
    {
        finalSpeed.Add(5.0f, null);  // 无英雄部队保持原版速度
    }

    // 最低速度 3.0
    return MathF.Max(finalSpeed.ResultNumber, 3.0f);
}
```

**关键API**: `ExplainedNumber` — 骑砍2用于可解释数值计算的类（支持多因子累加/累乘）

---

## 四、关键技术模式索引

| 需求 | 实现方式 | 位置 |
|------|----------|------|
| **替换游戏核心算法** | 继承原版Model + `gameStarterObject.AddModel()` | `SubModule.cs` |
| **方法拦截** | Harmony `[HarmonyPatch]` + `Prefix`/`Postfix` | `Harmony.cs` |
| **反射读写私有属性** | Harmony `Traverse` 或原生 `typeof(T).GetField()` | `Harmony.cs`, `Scripts.cs` |
| **自定义投射物** | `Mission.AddCustomMissile()` | `Scripts.cs` |
| **投射物追踪** | 每帧修改 `Missile.GetVelocity()` + `Vec3.Lerp` | `missionSetting.cs` |
| **Agent速度检测** | 差分 `Position` 计算速度向量 | `missionSetting.cs` (AgentSpeed) |
| **弹道计算** | 解二次方程 + 欧拉积分 | `Scripts.cs` |
| **移动目标预瞄** | 迭代收敛 (5次) | `Scripts.cs` |
| **角色描边** | `AgentVisuals.SetContourColor()` | `missionSetting.cs` |
| **自定义HUD** | `GauntletLayer.LoadMovie("xml名称", VM)` | `WoW_MainAgentStatus.cs` |
| **AI控制** | `agent.SetIsAIPaused(true/false)` | `missionSetting.cs` |
| **快进** | `mission.SetFastForwardingFromUI(true/false)` | `missionSetting.cs` |
| **弹药操作** | `agent.Equipment[slot].Amount` 读写 | `Scripts.cs` |
| **强制上马** | Harmony Prefix拦截 `Agent.Mount()`，Traverse修改 | `Harmony.cs` |
| **动作播放** | `agent.SetActionChannel(0, ActionIndexCache.Create("act_name"))` | `Scripts.cs` |
| **地图移速** | 覆写 `DefaultPartySpeedCalculatingModel` | `WoW_DefaultPartySpeedCalculatingModel.cs` |

---

## 五、与 New_ZZZF 的关系

| WoW 功能 | New_ZZZF 对应 | 说明 |
|---|---|---|
| 战斗核心 Tick | `SkillSystemBehavior.cs` | 重构后更模块化 |
| 技能系统 (7槽硬编码) | `SkillBase`/`SkillFactory`/AgentSkillComponent | 重构为可配置框架，36个技能 |
| 自瞄 | `Script.cs` → `AimShoot` | 保留 |
| 枪械系统 | `Script.cs` | 保留 |
| 灵马哨笛 | `Skills/Spell/` | 作为注册技能 |
| 伤害模型 | `NewDamageModel.cs` | 保留 |
| 大地图移速 | `WoW_DefaultPartySpeedCalculatingModel.cs` | 直接复用 |
| 自定义HUD | ? | 未确认是否保留 |
| 藏身处难度 | ? | 未确认是否保留 |
