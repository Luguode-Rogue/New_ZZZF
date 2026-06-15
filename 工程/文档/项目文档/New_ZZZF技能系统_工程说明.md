# New_ZZZF技能系统 - 工程说明

> **文档版本**: v1.0  
> **创建日期**: 2026-06-14  
> **适用范围**: New_ZZZF模块技能系统（除物品前后缀系统外的所有旧系统）  
> **目标读者**: 模块开发者、模组维护人员

---

## 一、技能系统概述

### 1.1 系统定位

New_ZZZF技能系统是一个基于Mount & Blade II: Bannerlord引擎的模块化技能框架，支持为兵种配置主动技能、被动技能、法术和战技。系统采用组件化设计，每个Agent（游戏单位）绑定一个`AgentSkillComponent`组件，负责管理工作。

### 1.2 核心特性

- **多技能槽位**: 支持主主动、副主动、被动、4个法术槽、战技槽
- **双资源系统**: 法力值（法术/战技）和耐力值（主动技能）独立管理
- **冷却机制**: 单个技能独立冷却 + 法术共享全局冷却（GCD）
- **Buff系统**: 支持状态效果的添加、持续更新和移除
- **AI支持**: 非玩家控制的Agent可自动施放技能
- **配置驱动**: 通过XML文件配置兵种技能，无需修改代码

---

## 二、系统架构

### 2.1 核心类图

```
SkillSystemBehavior (全局管理)
    ├── 监听Agent创建事件
    ├── 挂载AgentSkillComponent组件
    └── 驱动每帧更新

AgentSkillComponent (技能组件)
    ├── 技能槽位管理 (MainActive/SubActive/Passive/Spells/CombatArt)
    ├── 资源管理 (_currentMana/_currentStamina)
    ├── 冷却管理 (_cooldownTimers/_globalCooldownTimer)
    ├── Buff管理 (StateContainer)
    └── 输入处理 (HandlePlayerInput/HandleAIBehavior)

SkillBase (技能基类)
    ├── 技能属性 (SkillID/Type/Cooldown/ResourceCost)
    ├── Activate (激活技能)
    ├── OnEquip (装备时触发，被动技能)
    ├── OnUnequip (卸下时触发)
    └── CheckCondition (AI施法条件检查)

AgentBuff (Buff基类)
    ├── Buff属性 (StateId/Duration/SourceAgent)
    ├── OnApply (应用时触发)
    ├── OnUpdate (持续更新)
    └── OnRemove (移除时触发)

SkillConfigManager (配置管理)
    ├── 加载XML配置 (LoadFromXml)
    ├── 获取兵种技能配置 (GetSkillSetForTroop)
    └── 技能ID解析 (ParseSkill)

SkillFactory (技能工厂)
    ├── 技能注册表 (_skillRegistry)
    └── 技能创建 (Create)
```

### 2.2 文件结构

```
New_ZZZF/工程/New_ZZZF/
├── SkillSystemBehavior.cs          # 全局技能系统管理
├── Script.cs                       # 工具类（目标查找、弹道计算等）
├── Systems/                        # 核心系统代码
│   ├── AgentSkillComponent.cs      # Agent技能组件
│   ├── SkillBase.cs               # 技能基类 + 技能类型枚举
│   ├── SkillConfigManager.cs      # 兵种技能配置管理
│   ├── SkillFactory.cs            # 技能工厂（技能注册表）
│   ├── AgentState.cs              # Buff基类 + Buff容器
│   ├── AgentMissileSpeedData.cs   # 投射物速度记录
│   └── ProjectileData.cs         # 自定义投射物数据
├── Skills/                        # 技能实现代码
│   ├── MainActive/               # 主主动技能
│   │   ├── ZhanYi.cs           # 战意（示例）
│   │   ├── WeiYa.cs            # 威压（示例）
│   │   └── ...                 # 其他技能
│   ├── SubActive/                # 副主动技能
│   ├── Passive/                  # 被动技能
│   ├── Spell/                    # 法术技能
│   └── CombatArt/               # 战技技能
└── ModuleData/
    └── troop_skills.xml           # 兵种技能配置文件
```

---

## 三、技能类型详解

### 3.1 技能类型枚举 (SPSkillType)

| 类型 | 枚举值 | 触发方式 | 资源消耗 | 说明 |
|------|---------|---------|---------|------|
| 主主动 | `MainActive` | E键 | 耐力 | 角色的主要主动技能 |
| 副主动 | `SubActive` | 左Alt键 | 耐力 | 角色的次要主动技能 |
| 被动 | `Passive` | 自动生效 | 无 | 装备时自动生效，持续有效 |
| 法术 | `Spell` | 鼠标滚轮选择+右键 | 法力 | 可配置4个法术槽位 |
| 战技 | `CombatArt` | 长按攻击键后松开 | 法力 | 特殊攻击技能 |
| 法术被动 | `Passive_Spell` | 自动生效 | 无 | 可放在法术栏的被动技能 |
| 法术战技 | `CombatArt_Spell` | 长按攻击键后松开 | 法力 | 可放在法术栏的战技 |
| 战技法术 | `Spell_CombatArt` | 鼠标滚轮选择+右键 | 法力 | 可放在战技栏的法术 |

### 3.2 技能槽位说明

每个Agent有**7个技能槽位**：

1. **主主动技能槽** (`MainActiveSkill`) - E键触发
2. **副主动技能槽** (`SubActiveSkill`) - 左Alt键触发
3. **被动技能槽** (`PassiveSkill`) - 装备时自动生效
4. **法术槽1-4** (`SpellSlots[0-3]`) - 鼠标滚轮切换，右键触发
5. **战技槽** (`CombatArtSkill`) - 长按攻击键后松开触发

---

## 四、如何使用技能系统

### 4.1 为兵种配置技能

#### 步骤1：编辑XML配置文件

文件路径：`e:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\New_ZZZF\ModuleData\troop_skills.xml`

```xml
<!-- troop_skills.xml -->
<TroopSkills>
    <Troop id="commander_1">
        <!-- 主主动技能 -->
        <MainActive>ZhanYi</MainActive>
        
        <!-- 副主动技能 -->
        <SubActive>AdrenalineRush</SubActive>
        
        <!-- 被动栏（仅允许一个） -->
        <Passive>IronWill</Passive>
        
        <!-- 法术栏（最多4个，可混合法术或低级被动） -->
        <Spells>
            <Slot1>Fireball</Slot1>          <!-- 火球术 -->
            <Slot2>HouYueSheJi</Slot2>       <!-- 后跃射击 -->
            <Slot3>LeiJi</Slot3>            <!-- 雷击 -->
            <Slot4>HuiJianYuanZhen</Slot4>   <!-- 辉剑圆阵 -->
        </Spells>
        
        <!-- 战技栏 -->
        <CombatArt>JianQi</CombatArt>
    </Troop>
</TroopSkills>
```

#### 步骤2：获取兵种ID

兵种ID对应游戏中的`CharacterObject.StringId`，可通过以下方式获取：

```csharp
// 在SkillSystemBehavior.GetTroopId方法中
string troopId = agent.Character?.StringId ?? "unknown_troop";
```

#### 步骤3：加载配置

在Mod初始化时加载配置：

```csharp
// 在SubModule.cs中
protected override void OnGameStart(Game game, IGameStarter starter)
{
    SkillConfigManager.Instance.LoadFromXml(
        "Modules/New_ZZZF/ModuleData/troop_skills.xml"
    );
}
```

### 4.2 在游戏中触发技能

#### 玩家控制角色

| 操作 | 触发技能 |
|------|---------|
| 按下E键 | 主主动技能 |
| 按下左Alt键 | 副主动技能 |
| 鼠标滚轮 | 切换法术槽位 |
| 按下右键 | 施放当前选中的法术 |
| 长按左键后松开 | 战技 |

#### AI控制角色

AI会自动检查技能条件并概率触发：

```csharp
// 在AgentSkillComponent.HandleAIBehaviorOfTick中
if (MainActiveSkill.CheckCondition(Agent) && random.NextFloat() > 0.5f)
{
    TryActivateSkill(MainActiveSkill);
}
```

---

## 五、如何扩展技能系统

### 5.1 创建新技能

#### 步骤1：创建技能类文件

在`Skills/`目录下创建新技能文件，按类型放入对应子文件夹：

```
Skills/
├── MainActive/               # 主主动技能
│   └── MyNewSkill.cs        # 新技能
├── SubActive/                # 副主动技能
├── Passive/                  # 被动技能
├── Spell/                    # 法术技能
└── CombatArt/               # 战技技能
```

#### 步骤2：继承SkillBase基类

```csharp
using New_ZZZF.Systems;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class MyNewSkill : SkillBase
    {
        public MyNewSkill()
        {
            // 必须唯一
            SkillID = "MyNewSkill";
            
            // 技能类型：MainActive/SubActive/Passive/Spell/CombatArt
            Type = SPSkillType.MainActive;
            
            // 冷却时间（秒）
            Cooldown = 10f;
            
            // 资源消耗（耐力或法力）
            ResourceCost = 30f;
            
            // 技能名称（本地化）
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF9999}我的新技能");
            
            // 技能描述（本地化）
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF9998}这是我的新技能描述");
            
            // 使用难度（可选，影响角色是否可以装备该技能）
            // Difficulty = new List<SkillDifficulty> 
            // { 
            //     new SkillDifficulty(50, "跑动"), 
            //     new SkillDifficulty(5, "耐力") 
            // };
        }
        
        /// <summary>
        /// 激活技能的主逻辑（必须由子类实现）
        /// </summary>
        /// <param name="agent">施法者</param>
        /// <returns>激活成功返回true，失败返回false</returns>
        public override bool Activate(Agent agent)
        {
            // 在这里实现技能效果
            
            // 示例：创建一个Buff效果
            List<AgentBuff> newStates = new List<AgentBuff> 
            { 
                new MyNewSkillBuff(10f, agent),  // 持续10秒
            };
            
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            
            return true;
        }
        
        /// <summary>
        /// Buff效果定义（嵌套类）
        /// </summary>
        public class MyNewSkillBuff : AgentBuff
        {
            private float _timeSinceLastTick;
            
            public MyNewSkillBuff(float duration, Agent source)
            {
                StateId = "MyNewSkillBuff";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0;
            }
            
            /// <summary>
            /// Buff应用时触发
            /// </summary>
            public override void OnApply(Agent agent)
            {
                // Buff应用时的逻辑
                InformationManager.DisplayMessage(new InformationMessage("我的新技能Buff已应用"));
            }
            
            /// <summary>
            /// Buff持续期间每帧更新
            /// </summary>
            public override void OnUpdate(Agent agent, float dt)
            {
                // 累积时间
                _timeSinceLastTick += dt;
                
                // 每秒执行一次
                if (_timeSinceLastTick >= 1f)
                {
                    // 在这里实现持续效果
                    InformationManager.DisplayMessage(new InformationMessage("我的新技能Buff持续中..."));
                    
                    _timeSinceLastTick -= 1f;
                }
            }
            
            /// <summary>
            /// Buff移除时触发
            /// </summary>
            public override void OnRemove(Agent agent)
            {
                // Buff移除时的清理逻辑
                InformationManager.DisplayMessage(new InformationMessage("我的新技能Buff已移除"));
            }
        }
    }
}
```

#### 步骤3：注册技能到技能工厂

在`Systems/SkillFactory.cs`中的`_skillRegistry`字典中注册新技能：

```csharp
public static readonly Dictionary<string, SkillBase> _skillRegistry = Refresh_skillRegistry();

public static Dictionary<string, SkillBase> Refresh_skillRegistry()
{
    return new Dictionary<string, SkillBase>
    {
        // ... 其他技能
        
        // 新技能注册
        { "MyNewSkill", new MyNewSkill() },
    };
}
```

#### 步骤4：配置兵种技能

在`ModuleData/troop_skills.xml`中为新兵种配置技能：

```xml
<Troop id="my_new_troop">
    <MainActive>MyNewSkill</MainActive>
    <!-- 其他槽位配置 -->
</Troop>
```

### 5.2 创建被动技能

被动技能在装备时自动生效，无需手动触发：

```csharp
internal class MyPassiveSkill : SkillBase
{
    public MyPassiveSkill()
    {
        SkillID = "MyPassiveSkill";
        Type = SPSkillType.Passive;
        Cooldown = 0f;
        ResourceCost = 0f;
        Text = new TaleWorlds.Localization.TextObject("{=ZZZF9997}我的被动技能");
        Description = new TaleWorlds.Localization.TextObject("{=ZZZF9996}这是我的被动技能描述");
    }
    
    /// <summary>
    /// 被动技能在装备时触发
    /// </summary>
    public override void OnEquip(Agent agent)
    {
        // 注册事件监听或应用永久效果
        InformationManager.DisplayMessage(new InformationMessage("我的被动技能已装备"));
        
        // 示例：增加血量上限
        agent.Health = agent.Health * 1.2f;
        agent.UpdateAgentProperties();
    }
    
    /// <summary>
    /// 被动技能在卸下时触发
    /// </summary>
    public override void OnUnequip(Agent agent)
    {
        // 清理效果
        InformationManager.DisplayMessage(new InformationMessage("我的被动技能已卸下"));
        
        // 示例：恢复血量上限
        agent.Health = agent.Health / 1.2f;
        agent.UpdateAgentProperties();
    }
    
    public override bool Activate(Agent agent)
    {
        // 被动技能无需实现激活逻辑
        return false;
    }
}
```

### 5.3 创建法术技能

法术技能消耗法力值，支持远程效果：

```csharp
internal class MySpellSkill : SkillBase
{
    public MySpellSkill()
    {
        SkillID = "MySpellSkill";
        Type = SPSkillType.Spell;
        Cooldown = 5f;
        ResourceCost = 20f;  // 消耗法力值
        Text = new TaleWorlds.Localization.TextObject("{=ZZZF9995}我的法术");
        Description = new TaleWorlds.Localization.TextObject("{=ZZZF9994}这是我的法术描述");
    }
    
    public override bool Activate(Agent agent)
    {
        // 法术技能通常需要选择目标
        if (Script.FindTarAgents(agent, 10, out var targets))
        {
            foreach (var target in targets)
            {
                // 对目标施加效果
                Script.CalculateFinalMagicDamage(agent, target, 50f, DamageType.FIRE_DAMAGE);
            }
            return true;
        }
        
        return false;
    }
}
```

### 5.4 创建战技技能

战技技能通过长按攻击键后松开触发：

```csharp
internal class MyCombatArtSkill : SkillBase
{
    public MyCombatArtSkill()
    {
        SkillID = "MyCombatArtSkill";
        Type = SPSkillType.CombatArt;
        Cooldown = 15f;
        ResourceCost = 40f;  // 消耗法力值
        Text = new TaleWorlds.Localization.TextObject("{=ZZZF9993}我的战技");
        Description = new TaleWorlds.Localization.TextObject("{=ZZZF9992}这是我的战技描述");
    }
    
    public override bool Activate(Agent agent)
    {
        // 战技技能通常在近战范围内生效
        Agent target = Script.FindClosestAgentToCaster(agent, Mission.Current.Agents);
        
        if (target != null && target.IsEnemyOf(agent))
        {
            // 对目标施加特殊攻击效果
            InformationManager.DisplayMessage(new InformationMessage("我的战技已触发"));
            return true;
        }
        
        return false;
    }
}
```

---

## 六、高级功能

### 6.1 使用Script工具类

`Script`类提供了大量实用方法，可在技能中调用：

#### 目标查找

```csharp
// 查找施法者视线范围内的目标
Script.FindTarAgents(casterAgent, selectRange, out var targetList);

// 查找指定位置周围的Agent
List<Agent> agentsInRange = Script.FindAgentsWithinSpellRange(targetPosition, spellRange);

// 查找距离最近的Agent
Agent closestAgent = Script.FindClosestAgentToCaster(casterAgent, agentList);

// 敌我识别
Script.AgentListIFF(casterAgent, agentList, out var friendList, out var foeList);
```

#### 弹道计算

```csharp
// 计算弹道射击角度
Vec3 firingSolution = Script.CalculateProjectileFiringSolution(
    startPosition, 
    targetPosition, 
    projectileSpeed, 
    gravity
);

// 发射投射物
int missileIndex = Script.FireProjectileFromAgentWithWeaponAtPosition(
    shooterAgent,
    weaponMissionWeapon,
    ammoMissionWeapon,
    startPosition,
    targetPosition,
    missileSpeed
);
```

#### 伤害计算

```csharp
// 造成魔法伤害
Script.CalculateFinalMagicDamage(casterAgent, victimAgent, damageAmount, damageType);
```

#### 其他实用方法

```csharp
// 获取Agent目视位置
Vec3 lookPosition = Script.AgentLookPos(agent);

// 获取相机目视位置
Vec3 cameraLookPosition = Script.CameraLookPos();

// 向量缩放
Vec3 scaledVector = Script.MultiplyVectorByScalar(vector, scalar);

// 输出调试信息（仅对玩家显示）
Script.SysOut("调试信息", agent);
```

### 6.2 Buff系统高级用法

#### 添加Buff

```csharp
// 创建Buff实例
AgentBuff buff = new MyBuff(duration, sourceAgent);
buff.TargetAgent = targetAgent;

// 添加到目标的Buff容器
targetAgent.GetComponent<AgentSkillComponent>().StateContainer.AddState(buff);
```

#### 检查Buff

```csharp
// 检查Agent是否有指定Buff
bool hasBuff = agent.GetComponent<AgentSkillComponent>().StateContainer.HasState("BuffStateId");

// 获取Buff实例
AgentBuff buff = agent.GetComponent<AgentSkillComponent>().StateContainer.GetState("BuffStateId");
```

#### 移除Buff

```csharp
// 移除指定Buff
agent.GetComponent<AgentSkillComponent>().StateContainer.RemoveState("BuffStateId", agent);
```

### 6.3 自定义投射物

系统支持创建自定义投射物（如追踪导弹）：

```csharp
// 创建GameEntity作为投射物
GameEntity projectileEntity = GameEntity.CreateEmpty(Mission.Current.Scene);
projectileEntity.AddMesh(Mesh.GetFromResource("my_projectile_mesh"));

// 设置初始位置和方向
MatrixFrame frame = new MatrixFrame(new Mat3(), initialPosition);
projectileEntity.SetGlobalFrame(frame);

// 添加到自定义投射物字典
SkillSystemBehavior.WoW_CustomGameEntity.Add(projectileEntity);
SkillSystemBehavior.WoW_ProjectileDB.Add(projectileEntity, new ProjectileData 
{ 
    Name = "MyProjectile",
    CasterAgent = casterAgent,
    TargetAgent = targetAgent,
    BaseSpeed = 20f,
    Lifetime = 5f,
    // ...
});
```

---

## 七、调试技巧

### 7.1 启用调试日志

在技能代码中添加调试日志：

```csharp
// 仅对玩家显示消息
Script.SysOut("调试信息", agent);

// 显示到游戏内消息框
InformationManager.DisplayMessage(new InformationMessage("调试信息"));

// 输出到控制台
Debug.Print("调试信息");
```

### 7.2 常见错误处理

#### 技能无法触发

1. 检查技能是否已注册到`SkillFactory._skillRegistry`
2. 检查兵种ID是否正确配置在`troop_skills.xml`
3. 检查资源是否充足（耐力/法力）
4. 检查技能是否在冷却中
5. 检查`Activate`方法是否返回`true`

#### Buff无法生效

1. 检查Buff的`StateId`是否唯一
2. 检查Buff是否已正确添加到`StateContainer`
3. 检查`OnApply`、`OnUpdate`、`OnRemove`方法是否正确实现
4. 检查Buff的`Duration`是否大于0

#### 投射物无法显示

1. 检查`GameEntity`是否已正确创建
2. 检查`Mesh`是否已正确加载
3. 检查是否已添加到`WoW_CustomGameEntity`和`WoW_ProjectileDB`
4. 检查投射物位置是否在地势上方

---

## 八、最佳实践

### 8.1 技能设计原则

1. **唯一SkillID**: 每个技能的SkillID必须唯一，建议使用英文+数字
2. **资源消耗平衡**: 主动技能消耗耐力，法术/战技消耗法力，保持平衡
3. **冷却时间合理**: 强力技能冷却时间长，弱小技能冷却时间短
4. **Buff持续时间**: 避免过长的Buff持续时间，建议不超过60秒
5. **错误处理**: 在`Activate`方法中添加必要的错误检查，返回`false`表示激活失败

### 8.2 代码组织

1. **按类型分类**: 将技能按类型放入对应子文件夹（MainActive/SubActive/Passive/Spell/CombatArt）
2. **嵌套Buff类**: 将Buff类定义为技能类的嵌套类，提高代码可读性
3. **注释清晰**: 为每个技能和方法添加清晰的注释
4. **本地化文本**: 使用`TextObject`进行本地化，支持多语言

### 8.3 性能优化

1. **避免每帧计算**: 在`OnUpdate`中使用计时器，避免每帧执行复杂计算
2. **缓存Agent引用**: 避免频繁调用`GetComponent<AgentSkillComponent>()`
3. **批量处理**: 对多个目标施加效果时，使用批量处理减少循环次数
4. **及时清理**: 移除过期的Buff和投射物，避免内存泄漏

---

## 九、常见问题解答

### Q1: 如何为现有兵种添加新的技能？

**A**: 编辑`ModuleData/troop_skills.xml`，找到对应兵种的`<Troop>`节点，在对应槽位标签中填入技能ID即可。

### Q2: 如何创建只对自己生效的Buff？

**A**: 在技能的`Activate`方法中，创建Buff实例并设置`TargetAgent`为施法者自身：

```csharp
public override bool Activate(Agent agent)
{
    AgentBuff buff = new MyBuff(10f, agent);
    buff.TargetAgent = agent;  // 目标为自身
    agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(buff);
    return true;
}
```

### Q3: 如何创建对周围敌人生效的技能？

**A**: 使用`Script.FindAgentsWithinSpellRange`查找周围敌人，然后对每个敌人施加效果：

```csharp
public override bool Activate(Agent agent)
{
    List<Agent> targets = Script.FindAgentsWithinSpellRange(agent.Position, 5);
    Script.AgentListIFF(agent, targets, out var friends, out var foes);
    
    foreach (var foe in foes)
    {
        // 对敌人施加效果
        Script.CalculateFinalMagicDamage(agent, foe, 30f, DamageType.FIRE_DAMAGE);
    }
    return true;
}
```

### Q4: 如何创建需要选择目标的技能？

**A**: 使用`Script.FindTarAgents`方法，该方法会自动处理玩家和AI的目标选择逻辑：

```csharp
public override bool Activate(Agent agent)
{
    if (Script.FindTarAgents(agent, 10, out var targets) && targets.Count > 0)
    {
        foreach (var target in targets)
        {
            // 对目标施加效果
        }
        return true;
    }
    return false;
}
```

### Q5: 如何创建持续伤害技能（DOT）？

**A**: 创建Buff，在`OnUpdate`方法中每帧造成伤伤害：

```csharp
public class MyDOTBuff : AgentBuff
{
    private float _timeSinceLastTick;
    
    public override void OnUpdate(Agent agent, float dt)
    {
        _timeSinceLastTick += dt;
        
        if (_timeSinceLastTick >= 1f)  // 每秒造成一次伤害
        {
            Script.CalculateFinalMagicDamage(
                SourceAgent, 
                agent, 
                10f, 
                DamageType.TOXIN_DAMAGE
            );
            _timeSinceLastTick -= 1f;
        }
    }
}
```

---

## 十、附录

### 10.1 现有技能列表

以下是当前已实现的技能列表（部分）：

| 技能ID | 技能名称 | 类型 | 说明 |
|---------|---------|------|------|
| ZhanYi | 战意 | 主主动 | 击杀恢复血量，增加伤害和速度 |
| WeiYa | 威压 | 主主动 | 降低敌方移动能力/伤害/精度 |
| JianQi | 剑气 | 战技 | 发射剑气攻击敌人 |
| Fireball | 火球术 | 法术 | 发射火球造成火焰伤害 |
| ShadowStep | 暗影步 | 主主动 | 瞬间移动到目标位置 |
| ... | ... | ... | ... |

### 10.2 参考资料

- **骑砍2模组开发文档**: https://docs.bannerlord.com/
- **SkillSystemBehavior.cs**: 全局技能系统管理
- **AgentSkillComponent.cs**: Agent技能组件详解
- **SkillBase.cs**: 技能基类定义
- **AgentState.cs**: Buff系统详解
- **troop_skills.xml**: 兵种技能配置示例

### 10.3 联系方式

如有问题或建议，请联系模组开发者。

---

**文档结束**
