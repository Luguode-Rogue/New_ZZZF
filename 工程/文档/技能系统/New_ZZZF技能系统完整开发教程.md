# New_ZZZF 技能系统完整开发教程

> **文档版本**: v2.0  
> **适用版本**: New_ZZZF 新技能界面 (CustomSkillScreen)  
> **最后更新**: 2026-06-19

---

## 目录

1. [系统概述](#1-系统概述)
2. [核心架构](#2-核心架构)
3. [技能类型说明](#3-技能类型说明)
4. [核心类参考](#4-核心类参考)
5. [新增技能完整流程](#5-新增技能完整流程)
6. [新增Buff完整流程](#6-新增buff完整流程)
7. [技能配置方式](#7-技能配置方式)
8. [技能界面使用指南](#8-技能界面使用指南)
9. [高级开发主题](#9-高级开发主题)
10. [完整代码示例](#10-完整代码示例)
11. [常见问题排查](#11-常见问题排查)

---

## 1. 系统概述

New_ZZZF 技能系统是一个为 Mount & Blade II Bannerlord 设计的完整技能框架，支持：

- **5种技能类型**：主主动、副主动、被动、法术、战技
- **Buff/Debuff系统**：基于状态机的持续效果管理
- **图形化配置界面**：不依赖原版物品系统的独立界面
- **兵种技能配置**：通过XML为不同兵种配置技能
- **AI自动施法**：士兵AI会根据条件自动使用技能

### 系统文件结构

```
New_ZZZF/
├── Skills/                      # 技能实现文件
│   ├── MainActive/              # 主主动技能
│   │   ├── ChaoFeng.cs
│   │   ├── JianQi.cs
│   │   └── ...
│   ├── SubActive/              # 副主动技能
│   ├── Passive/               # 被动技能
│   ├── Spell/                 # 法术技能
│   └── CombatArt/             # 战技技能
├── Systems/                    # 核心系统文件
│   ├── SkillBase.cs           # 技能基类
│   ├── AgentState.cs          # Buff基类 + 状态容器
│   ├── SkillFactory.cs        # 技能注册工厂
│   ├── SkillConfigManager.cs  # 兵种技能配置管理
│   ├── AgentSkillComponent.cs # Agent技能组件
│   └── ZZZF_SandboxAgentStatCalculateModel.cs  # 属性计算
├── GUI/                       # 界面相关
│   ├── CustomSkillScreen.cs   # 技能界面Screen
│   ├── CustomSkillScreenVM.cs # 主ViewModel
│   ├── SkillItemVM.cs        # 技能项ViewModel
│   └── Prefabs/
│       └── CustomSkillScreen.xml  # 界面布局
└── _Module/ModuleData/
    ├── troop_skills.xml       # 兵种技能配置
    └── languages/CNs/
        └── ZZF_Skill-CN.xml  # 技能中文翻译
```

---

## 2. 核心架构

### 2.1 技能执行流程

```
游戏启动
    ↓
SkillFactory._skillRegistry 初始化（注册所有技能）
    ↓
SkillToItemObject() 创建物品对象（用于界面显示）
    ↓
Mission开始
    ↓
SkillSystemBehavior.OnAgentCreated()
    ↓
AgentSkillComponent 挂载到 Agent
    ↓
InitializeFromTroop() 加载技能配置
    ↓
游戏进行中
    ↓
AgentSkillComponent.Tick() 每帧更新
    ↓
玩家按E/Alt/右键 或 AI触发
    ↓
TryActivateSkill() → Skill.Activate()
    ↓
创建Buff → StateContainer.AddState()
    ↓
Buff.OnApply() → OnUpdate()每帧 → OnRemove()
```

### 2.2 界面打开流程

```
玩家触发（某个入口）
    ↓
ScreenManager.PushScreen(new CustomSkillScreen())
    ↓
CustomSkillScreen.OnInitialize()
    ↓
创建 CustomSkillScreenVM
    ↓
加载 SkillCatalog（所有可用技能列表）
    ↓
填充 Roster（队伍成员列表）
    ↓
显示界面
    ↓
玩家选择目标 → 选择槽位 → 打开技能目录
    ↓
选择技能 → AssignSkillToSlot()
    ↓
Ctrl+S → SaveCurrentHeroSkills() → HeroSkillData.Save()
```

---

## 3. 技能类型说明

| 类型 | 枚举值 | 触发方式 | 资源类型 | 说明 |
|------|----------|----------|----------|------|
| **主主动** | `MainActive` | E键 | 耐力 | 主要战斗技能 |
| **副主动** | `SubActive` | 左Alt | 耐力 | 辅助战斗技能 |
| **被动** | `Passive` | 自动生效 | 无 | 永久属性加成或特效 |
| **法术** | `Spell` | 鼠标滚轮选择+右键 | 法力 | 最多4个，共享GCD |
| **战技** | `CombatArt` | 长按攻击键后松开 | 耐力/法力 | 特殊战斗技巧 |

### 技能类型组合（高级）

| 类型 | 说明 |
|------|------|
| `Passive_Spell` | 可放在法术栏的被动技能 |
| `CombatArt_Spell` | 可放在法术栏的战技 |
| `Spell_CombatArt` | 可放在战技栏的法术 |

---

## 4. 核心类参考

### 4.1 SkillBase（技能基类）

**文件位置**: `Systems/SkillBase.cs`

```csharp
public abstract class SkillBase
{
    // === 基础属性（必须在构造函数中设置）===
    public string SkillID { get; protected set; }      // 技能唯一ID
    public SPSkillType Type { get; protected set; }    // 技能类型
    public float Cooldown { get; protected set; }        // 冷却时间（秒）
    public float ResourceCost { get; protected set; }    // 资源消耗
    public List<SkillDifficulty> Difficulty { get; protected set; }  // 使用难度要求
    public ItemObject Item { get; set; }               // 对应的显示物品
    public TextObject Text { get; set; }               // 技能名称（本地化）
    public TextObject Description { get; set; }         // 技能描述（本地化）

    // === 必须实现的抽象方法 ===
    public abstract bool Activate(Agent casterAgent);    // 技能激活逻辑

    // === 虚拟方法（可选重写）===
    public virtual void OnEquip(Agent agent);           // 装备时触发（被动技能用）
    public virtual void OnUnequip(Agent agent);         // 卸下时触发
    public virtual void GameEntityDamage(GameEntity missileEntity);  // 弹丸伤害回调
    public virtual bool CheckCondition(Agent caster);    // AI施法条件检查
}
```

### 4.2 AgentBuff（Buff基类）

**文件位置**: `Systems/AgentState.cs`

```csharp
public abstract class AgentBuff
{
    public string StateId { get; protected set; }    // Buff唯一标识
    public float Duration { get; set; }             // 剩余持续时间（秒）
    public Agent SourceAgent { get; set; }           // 来源Agent
    public Agent TargetAgent { get; set; }           // 目标Agent

    // === 必须实现的抽象方法 ===
    public abstract void OnApply(Agent agent);        // Buff生效时调用（仅一次）
    public abstract void OnUpdate(Agent agent, float dt);  // 每帧更新
    public abstract void OnRemove(Agent agent);       // Buff移除时调用（仅一次）
}
```

### 4.3 AgentBuffContainer（Buff容器）

**文件位置**: `Systems/AgentState.cs`

```csharp
public class AgentBuffContainer
{
    public bool HasState(string stateId);              // 检查是否有指定Buff
    public void AddState(AgentBuff state);            // 添加Buff（自动调用OnApply）
    public void UpdateStates(Agent agent, float dt);  // 更新所有Buff（每帧调用）
    public AgentBuff GetState(string stateId);         // 获取指定Buff
    public void RemoveState(string stateId, Agent agent);  // 移除Buff（自动调用OnRemove）
}
```

### 4.4 SkillFactory（技能工厂）

**文件位置**: `Systems/SkillFactory.cs`

核心字段：
```csharp
public static readonly Dictionary<string, SkillBase> _skillRegistry = Refresh_skillRegistry();
```

核心方法：
```csharp
public static SkillBase Create(string skillID);    // 根据ID创建技能实例
public static void SkillToItemObject();          // 将技能转换为物品对象
```

### 4.5 AgentSkillComponent（Agent技能组件）

**文件位置**: `Systems/AgentSkillComponent.cs`

```csharp
public class AgentSkillComponent : AgentComponent
{
    // === 技能槽 ===
    public SkillBase MainActiveSkill { get; private set; }
    public SkillBase SubActiveSkill { get; private set; }
    public SkillBase PassiveSkill { get; private set; }
    public SkillBase[] SpellSlots { get; }
    public SkillBase CombatArtSkill { get; private set; }

    // === 资源 ===
    public float _currentMana = 100f;
    public float _currentStamina = 100f;

    // === Buff容器 ===
    public AgentBuffContainer StateContainer { get; }

    // === 核心方法 ===
    public void InitializeFromTroop(string troopId);  // 从兵种配置初始化
    public void Tick(float dt);                        // 每帧更新
    public void CoolDownTick(float dt);               // 冷却更新
}
```

---

## 5. 新增技能完整流程

### 步骤1：创建技能类

在 `Skills/` 目录下的对应子文件夹中创建新类。

**示例：创建一个名为"火焰冲击"的技能**

**文件位置**: `Skills/MainActive/HuoYanChongJi.cs`

```csharp
using New_ZZZF.Systems;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class HuoYanChongJi : SkillBase
    {
        public HuoYanChongJi()
        {
            // === 基础属性设置 ===
            SkillID = "HuoYanChongJi";                    // 必须唯一
            Type = SPSkillType.MainActive;                  // 技能类型
            Cooldown = 8f;                                // 冷却时间（秒）
            ResourceCost = 30f;                           // 耐力消耗
            Text = new TextObject("{=ZZZF0100}火焰冲击");  // 名称（需要添加翻译）
            Description = new TextObject("{=ZZZF0101}向前释放一道火焰冲击波，对前方锥形区域内敌人造成火焰伤害。消耗耐力：30。冷却时间：8秒。");

            // 可选：设置使用难度要求
            // Difficulty = new List<SkillDifficulty> 
            // { 
            //     new SkillDifficulty(30, "火焰") 
            // };
        }

        /// <summary>
        /// 技能激活逻辑
        /// </summary>
        /// <param name="agent">施法者</param>
        /// <returns>是否成功激活</returns>
        public override bool Activate(Agent agent)
        {
            // === 1. 创建Buff实例 ===
            List<AgentBuff> newStates = new List<AgentBuff> 
            { 
                new HuoYanChongJiBuff(5f, agent)  // 持续时间5秒
            };

            // === 2. 将Buff添加到施法者 ===
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }

            // === 3. 对范围内敌人造成伤害 ===
            List<Agent> enemies = Script.GetTargetedInRange(
                agent, 
                agent.GetEyeGlobalPosition(), 
                15f);  // 15米范围

            if (enemies != null && enemies.Count > 0)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy.IsHuman && !enemy.IsMount)
                    {
                        // 计算伤害
                        Script.CalculateFinalMagicDamage(
                            agent,           // 施法者
                            enemy,           // 目标
                            50f,             // 基础伤害
                            DamageType.FIRE_DAMAGE);  // 伤害类型
                    }
                }
            }

            // === 4. 播放特效（可选）===
            agent.PlayParticleEffect("fire_explosion");

            return true;  // 返回true表示技能激活成功
        }

        /// <summary>
        /// AI施法条件检查
        /// </summary>
        public override bool CheckCondition(Agent caster)
        {
            // 基础条件：存活且非坐骑
            if (!caster.IsActive() || caster.IsMount)
                return false;

            // 自定义条件：附近有敌人
            List<Agent> enemies = Script.GetTargetedInRange(
                caster, 
                caster.GetEyeGlobalPosition(), 
                10f);

            return enemies != null && enemies.Count > 0;
        }

        // === 内部Buff类定义 ===
        public class HuoYanChongJiBuff : AgentBuff
        {
            private float _timeSinceLastTick;

            public HuoYanChongJiBuff(float duration, Agent source)
            {
                StateId = "HuoYanChongJiBuff";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0;
            }

            public override void OnApply(Agent agent)
            {
                // 应用时：播放火焰特效
                agent.PlayParticleEffect("fire_body");
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                _timeSinceLastTick += dt;

                // 每秒造成一次伤害
                if (_timeSinceLastTick >= 1f)
                {
                    // 对附近敌人造成伤害
                    List<Agent> enemies = Script.GetTargetedInRange(
                        SourceAgent, 
                        SourceAgent.GetEyeGlobalPosition(), 
                        10f);

                    if (enemies != null)
                    {
                        foreach (var enemy in enemies)
                        {
                            Script.CalculateFinalMagicDamage(
                                SourceAgent, enemy, 10f, 
                                DamageType.FIRE_DAMAGE);
                        }
                    }

                    _timeSinceLastTick -= 1f;
                }
            }

            public override void OnRemove(Agent agent)
            {
                // 移除时：停止特效
                agent.StopParticleEffect("fire_body");
            }
        }
    }
}
```

### 步骤2：注册技能到SkillFactory

**文件位置**: `Systems/SkillFactory.cs`

在 `_skillRegistry` 字典中添加新技能：

```csharp
public static readonly Dictionary<string, SkillBase> _skillRegistry = Refresh_skillRegistry();
public static Dictionary<string, SkillBase> Refresh_skillRegistry()
{
    return new Dictionary<string, SkillBase>
    {
        // ... 现有技能 ...
        
        // 添加新技能
        { "HuoYanChongJi", new HuoYanChongJi() },  // 火焰冲击
    };
}
```

### 步骤3：添加翻译文本

**文件位置**: `_Module/ModuleData/languages/CNs/ZZF_Skill-CN.xml`

在 `<strings>` 节点中添加：

```xml
<string id="ZZZF0100" text="火焰冲击"/>
<string id="ZZZF0101" text="向前释放一道火焰冲击波，对前方锥形区域内敌人造成火焰伤害。消耗耐力：30。冷却时间：8秒。"/>
```

### 步骤4：配置技能（可选）

如果要将技能配置给特定兵种，编辑：

**文件位置**: `_Module/ModuleData/troop_skills.xml`

```xml
<Troop id="imperial_veteran_infantryman">
    <MainActive>HuoYanChongJi</MainActive>  <!-- 配置给帝国老练步兵 -->
    <!-- 其他槽位... -->
</Troop>
```

### 步骤5：测试技能

1. 编译项目
2. 启动游戏
3. 打开技能界面（通过MOD提供的入口）
4. 选择角色 → 选择主主动槽位 → 选择"火焰冲击"
5. 保存并进入战斗测试

---

## 6. 新增Buff完整流程

### 6.1 纯Buff（不依赖技能）

如果只需要一个独立的Buff效果（比如装备附加的Buff），直接继承 `AgentBuff`：

```csharp
public class SpeedBoostBuff : AgentBuff
{
    public SpeedBoostBuff(float duration, Agent source)
    {
        StateId = "SpeedBoostBuff";
        Duration = duration;
        SourceAgent = source;
    }

    public override void OnApply(Agent agent)
    {
        // 不需要立即生效的逻辑
    }

    public override void OnUpdate(Agent agent, float dt)
    {
        // 持续效果在属性计算系统中处理
        // 参见 9.1 属性强化
    }

    public override void OnRemove(Agent agent)
    {
        // 清理逻辑
    }
}
```

### 6.2 在技能中使用Buff

参考 [5. 新增技能完整流程](#5-新增技能完整流程) 中的示例。

### 6.3 Buff高级功能

#### 6.3.1 强化人物属性

**重要说明**：单纯的调用 `agent.AgentDrivenProperties` 修改数值只能在修改的那一帧生效，必须在 `ZZZF_SandboxAgentStatCalculateModel` 中覆写 `UpdateHumanStats` 方法。

**文件位置**: `Systems/ZZZF_SandboxAgentStatCalculateModel.cs`

```csharp
public override void UpdateHumanStats(Agent agent, AgentDrivenProperties properties)
{
    base.UpdateHumanStats(agent, properties);

    // 检查Agent是否有指定Buff
    var skillComponent = agent.GetComponent<AgentSkillComponent>();
    if (skillComponent == null) return;

    if (skillComponent.StateContainer.HasState("SpeedBoostBuff"))
    {
        // 增加移动速度30%
        properties.MaxSpeedMultiplier += 0.3f;
    }

    if (skillComponent.StateContainer.HasState("ArmorBuff"))
    {
        // 增加护甲
        properties.ArmorHead += 20;
        properties.ArmorBody += 20;
        properties.ArmorArms += 20;
        properties.ArmorLegs += 20;
    }
}
```

#### 6.3.2 强化伤害

有两种方式：

**方式A：百分比增伤**

**文件位置**: `Systems/NewDamageModel.cs` 或 `WoW_Script_AgentStatCalculateModel.cs`

```csharp
// 在伤害计算方法中
if (skillComponent.StateContainer.HasState("DamageBoostBuff"))
{
    damageMultiplier += 0.5f;  // 增加50%伤害
}
```

**方式B：固定数值增伤**

在 `NewDamageModel.cs` 中的适当位置添加：

```csharp
// 近战伤害
adjustedDamage += 10f;  // 固定增加10点伤害

// 远程伤害
adjustedDamage += 5f;
```

#### 6.3.3 附加Buff给士兵

```csharp
// 方式1：附加给自身
var buff = new MyCustomBuff(duration, agent);
buff.TargetAgent = agent;
agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(buff);

// 方式2：附加给目标
var buff = new MyCustomBuff(duration, agent);  // agent是施法者
buff.TargetAgent = targetAgent;                 // targetAgent是目标
targetAgent.GetComponent<AgentSkillComponent>().StateContainer.AddState(buff);

// 方式3：附加给范围内所有敌人
List<Agent> enemies = Script.GetTargetedInRange(caster, position, range);
foreach (var enemy in enemies)
{
    var buff = new MyCustomBuff(duration, caster);
    buff.TargetAgent = enemy;
    enemy.GetComponent<AgentSkillComponent>().StateContainer.AddState(buff);
}
```

---

## 7. 技能配置方式

### 7.1 通过图形界面配置（推荐）

1. 打开技能界面（`ScreenManager.PushScreen(new CustomSkillScreen())`）
2. 选择目标角色（左侧列表）
3. 点击技能槽位
4. 在技能目录中选择技能
5. 按 `Ctrl+S` 保存

### 7.2 通过XML配置（兵种默认技能）

**文件位置**: `_Module/ModuleData/troop_skills.xml`

```xml
<?xml version="1.0" encoding="utf-16"?>
<TroopSkills>
    <!-- 配置单个兵种 -->
    <Troop id="imperial_veteran_infantryman">
        <!-- 主主动技能 -->
        <MainActive>ChaoFeng</MainActive>
        
        <!-- 副主动技能 -->
        <SubActive>Rush</SubActive>
        
        <!-- 被动技能（只能配一个）-->
        <Passive></Passive>
        
        <!-- 战技 -->
        <CombatArt>JianQi</CombatArt>
        
        <!-- 法术栏（最多4个）-->
        <Spells>
            <Slot1>Fireball</Slot1>
            <Slot2></Slot2>
            <Slot3></Slot3>
            <Slot4></Slot4>
        </Spells>
    </Troop>
</TroopSkills>
```

**注意**：
- `id` 属性对应 `CharacterObject.StringId`
- 空节点表示空槽位
- 法术栏可以混合法术和低级被动

### 7.3 程序中动态配置

```csharp
// 获取Agent的技能组件
var skillComponent = agent.GetComponent<AgentSkillComponent>();

// 设置技能
skillComponent.MainActiveSkill = SkillFactory.Create("HuoYanChongJi");

// 初始化被动技能（会触发OnEquip）
skillComponent.PassiveSkill.OnEquip(agent);
```

---

## 8. 技能界面使用指南

### 8.1 打开技能界面

在游戏中通过以下代码打开：

```csharp
// C# 代码
ScreenManager.PushScreen(new CustomSkillScreen());
```

### 8.2 界面布局

```
┌─────────────────────────────────────────────────────┐
│                   技能配置                        │
│               [队伍成员]  [调试]                │
├──────────────┬──────────────────────────────────┤
│              │  已配置技能槽                    │
│  队伍成员    ├──────────────────────────────────┤
│  ┌────────┐  │  [主主动] 火焰冲击             │
│  │英雄1   │  │  [副主动] 冲锋                │
│  │英雄2   │  │  [被动]   铁壁                 │
│  │英雄3   │  │  [战技]   剑气斩              │
│  └────────┘  │  [法术①] 火球术              │
│              │  [法术②]                       │
│  兵种模板    │  [法术③]                       │
│  (调试模式)   │  [法术④]                       │
│              ├──────────────────────────────────┤
│  领主NPC     │  原生技能熟练度                  │
│  (调试模式)   │  ├ 跑动: 150                  │
│              │  ├ 耐力: 120                  │
│              │  └ ...                         │
└──────────────┴──────────────────────────────────┘
│ [1-8]槽位 [Tab]切换 [F12]调试 [Ctrl+S]保存 │
└─────────────────────────────────────────────────────┘
```

### 8.3 键盘快捷键

#### 主界面

| 快捷键 | 功能 |
|--------|------|
| `↑` / `↓` | 选择上一个/下一个角色 |
| `1` ~ `8` | 选择对应技能槽位（同时打开技能目录） |
| `Tab` | 循环切换目标类型（队伍/兵种/领主，需调试模式） |
| `F12` | 切换调试模式（显示兵种模板和领主NPC） |
| `Ctrl+S` | 应用更改（保存） |
| `Ctrl+Z` | 撤销更改 |
| `Esc` | 关闭界面 |

#### 技能目录视图

| 快捷键 | 功能 |
|--------|------|
| `↑` / `↓` | 选择上一个/下一个技能 |
| `←` / `→` | 选择上一列/下一列技能 |
| `Enter` | 确认选择当前高亮技能 |
| `Esc` | 关闭目录视图（返回技能槽界面） |

---

## 9. 高级开发主题

### 9.1 属性强化系统

所有属性强化都需要在 `ZZZF_SandboxAgentStatCalculateModel.cs` 中处理。

**文件位置**: `Systems/ZZZF_SandboxAgentStatCalculateModel.cs`

```csharp
public override void UpdateHumanStats(Agent agent, AgentDrivenProperties properties)
{
    base.UpdateHumanStats(agent, properties);

    var skillComponent = agent.GetComponent<AgentSkillComponent>();
    if (skillComponent == null) return;

    // 示例：根据Buff增加属性
    if (skillComponent.StateContainer.HasState("PowerBuff"))
    {
        properties.MeleeWeaponDamageMultiplier += 0.5f;
        properties.ThrustSpeed += 20f;
    }

    if (skillComponent.StateContainer.HasState("DefenseBuff"))
    {
        properties.ArmorHead += 30;
        properties.ArmorBody += 30;
        properties.DamageInterruptResistance += 0.5f;
    }

    // 示例：根据装备的被动技能增加属性
    if (skillComponent.PassiveSkill != null && 
        skillComponent.PassiveSkill.SkillID == "IronWill")
    {
        properties.MaxHealth += 100f;
        agent.Health = Math.Min(agent.Health + 100f, agent.HealthLimit);
    }
}
```

### 9.2 伤害计算系统

**文件位置**: `Systems/NewDamageModel.cs`

关键方法：
- `ComputeMeleeDamage()` - 近战伤害计算
- `ComputeRangedDamage()` - 远程伤害计算
- `ComputeDamage()` - 通用伤害计算

在相应方法中添加Buff检测逻辑：

```csharp
// 示例：Buff增加近战伤害
if (attackerSkillComponent.StateContainer.HasState("BerserkerBuff"))
{
    adjustedDamage *= 1.5f;  // 狂暴状态增加50%伤害
}
```

### 9.3 AI施法逻辑

**文件位置**: `Systems/AgentSkillComponent.cs` → `HandleAIBehaviorOfTick()`

```csharp
private void HandleAIBehaviorOfTick(float dt)
{
    Random random = new Random();
    
    // 主主动技能AI
    if (MainActiveSkill.CheckCondition(Agent) && random.NextFloat() > 0.5f)
    {
        TryActivateSkill(MainActiveSkill);
    }
    // ... 其他技能类型的AI逻辑
}
```

**自定义AI条件**：重写 `SkillBase.CheckCondition()` 方法

```csharp
public override bool CheckCondition(Agent caster)
{
    // 基础条件
    if (!caster.IsActive() || caster.IsMount) 
        return false;

    // 自定义条件1：血量低于50%时使用
    if (caster.Health / caster.HealthLimit < 0.5f)
        return true;

    // 自定义条件2：附近有超过3个敌人
    List<Agent> enemies = Script.GetTargetedInRange(
        caster, caster.GetEyeGlobalPosition(), 10f);
    if (enemies != null && enemies.Count >= 3)
        return true;

    return false;
}
```

### 9.4 被动技能开发

被动技能在装备时生效，通常需要：

1. **在 `OnEquip()` 中注册事件或设置标志**
2. **在 `OnUnequip()` 中清理**
3. **在属性计算系统中添加对应逻辑**

```csharp
public class IronWill : SkillBase
{
    public IronWill()
    {
        SkillID = "IronWill";
        Type = SPSkillType.Passive;
        // ...
    }

    public override bool Activate(Agent agent)
    {
        return false;  // 被动技能不需要激活
    }

    public override void OnEquip(Agent agent)
    {
        // 被动效果在 ZZZF_SandboxAgentStatCalculateModel 中处理
        // 这里可以设置一些标志或注册事件
        Debug.Print($"[IronWill] 已装备，增加100点血量");
    }

    public override void OnUnequip(Agent agent)
    {
        Debug.Print($"[IronWill] 已卸下");
    }
}
```

### 9.5 技能难度系统

通过 `Difficulty` 属性设置技能使用要求：

```csharp
public ChaoFeng()
{
    // ...
    Difficulty = new List<SkillDifficulty> 
    { 
        new SkillDifficulty(50, "跑动"),   // 需要跑动技能达到50级
        new SkillDifficulty(30, "耐力")    // 或耐力属性达到30
    };
}
```

**注意**：当前版本中，难度检查逻辑可能需要在技能界面中额外实现。

---

## 10. 完整代码示例

### 10.1 完整主动技能示例（嘲讽）

**文件位置**: `Skills/MainActive/ChaoFeng.cs`

```csharp
using New_ZZZF.Systems;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 嘲讽技能 - 强制敌人攻击自己，同时回复生命值
    /// </summary>
    internal class ChaoFeng : SkillBase
    {
        public ChaoFeng()
        {
            SkillID = "ChaoFeng";
            Type = SPSkillType.MainActive;
            Cooldown = 60f;
            ResourceCost = 20f;
            Text = new TextObject("{=ZZZF0023}嘲讽");
            Description = new TextObject("{=ZZZF0024}嘲讽附近敌方单位，并持续大幅回复自身血量。受到嘲讽的单位会持续靠近施法者。消耗耐力：20。持续时间：30秒。冷却时间：60秒。");
        }

        public override bool Activate(Agent agent)
        {
            // 给自己添加Buff
            List<AgentBuff> newStates = new List<AgentBuff> 
            { 
                new ChaoFengBuffApplyToSelf(30f, agent) 
            };
            
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }

            // 对附近敌人添加嘲讽Buff
            List<Agent> enemies = Script.GetTargetedInRange(
                agent, 
                agent.GetEyeGlobalPosition(), 
                30f);

            if (enemies != null && enemies.Count > 0)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy.IsHero) continue;  // 不对英雄单位生效

                    newStates = new List<AgentBuff> 
                    { 
                        new ChaoFengBuffApplyToEnemy(30f, agent) 
                    };
                    
                    foreach (var state in newStates)
                    {
                        state.TargetAgent = enemy;
                        enemy.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
                    }
                }
                return true;
            }

            return false;
        }

        public override bool CheckCondition(Agent caster)
        {
            // 检查是否已有该Buff
            var component = caster.GetComponent<AgentSkillComponent>();
            if (component != null && 
                component.StateContainer.HasState("ChaoFengBuffApplyToSelf"))
            {
                return false;  // 已有Buff，不能再次使用
            }

            // 血量低于50%时可以使用
            if (component != null && caster.Health / component.MaxHP <= 0.5f)
            {
                return true;
            }

            // 附近敌人多于5个时可以使用
            List<Agent> enemies = Script.GetTargetedInRange(
                caster, caster.GetEyeGlobalPosition(), 30f);
            if (enemies != null && enemies.Count > 5)
            {
                return true;
            }

            return false;
        }

        // === Buff定义：对敌人的嘲讽效果 ===
        public class ChaoFengBuffApplyToEnemy : AgentBuff
        {
            private float _timeSinceLastTick;

            public ChaoFengBuffApplyToEnemy(float duration, Agent source)
            {
                StateId = "ChaoFengBuffApplyToEnemy";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0;
            }

            public override void OnApply(Agent agent)
            {
                // 设置AI目标为施法者
                agent.SetTargetAgent(SourceAgent);
                agent.SetTargetPosition(SourceAgent.Position.AsVec2);
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                _timeSinceLastTick += dt;

                // 每秒刷新一次目标
                if (_timeSinceLastTick >= 1f)
                {
                    agent.SetTargetAgent(SourceAgent);
                    agent.SetTargetPosition(SourceAgent.Position.AsVec2);
                    _timeSinceLastTick -= 1f;
                }
            }

            public override void OnRemove(Agent agent)
            {
                agent.ClearTargetFrame();
                agent.InvalidateTargetAgent();
            }
        }

        // === Buff定义：对自身的回血效果 ===
        public class ChaoFengBuffApplyToSelf : AgentBuff
        {
            private float _timeSinceLastTick;

            public ChaoFengBuffApplyToSelf(float duration, Agent source)
            {
                StateId = "ChaoFengBuffApplyToSelf";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0;
            }

            public override void OnApply(Agent agent)
            {
                // 不需要立即效果
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                _timeSinceLastTick += dt;

                // 每秒回复一次血量
                if (_timeSinceLastTick >= 1f)
                {
                    var skillComponent = agent.GetComponent<AgentSkillComponent>();
                    if (skillComponent != null)
                    {
                        float healAmount = (skillComponent.MaxHP - agent.Health) * 0.5f;
                        agent.Health += healAmount;
                    }
                    _timeSinceLastTick -= 1f;
                }
            }

            public override void OnRemove(Agent agent)
            {
                // 不需要清理
            }
        }
    }
}
```

### 10.2 完整被动技能示例

```csharp
public class IronWill : SkillBase
{
    public IronWill()
    {
        SkillID = "IronWill";
        Type = SPSkillType.Passive;
        Cooldown = 0f;
        ResourceCost = 0f;
        Text = new TextObject("{=ZZZF0XXX}铁壁");
        Description = new TextObject("{=ZZZF0XXX}增加100点最大生命值，增加20点护甲。");
    }

    public override bool Activate(Agent agent)
    {
        return false;  // 被动技能不激活
    }

    public override void OnEquip(Agent agent)
    {
        Debug.Print($"[铁壁] 已装备");
        // 属性效果在 ZZZF_SandboxAgentStatCalculateModel 中处理
    }

    public override void OnUnequip(Agent agent)
    {
        Debug.Print($"[铁壁] 已卸下");
    }
}
```

---

## 11. 常见问题排查

### 11.1 技能不显示在界面中

**可能原因**：
1. 没有在 `SkillFactory._skillRegistry` 中注册
2. 技能类型与槽位类型不匹配
3. `SkillToItemObject()` 没有正确执行

**解决方法**：
- 检查 `SkillFactory.Refresh_skillRegistry()` 是否包含新技能
- 确认技能 `Type` 属性设置正确
- 在游戏启动时检查控制台输出

### 11.2 Buff不生效

**可能原因**：
1. `OnApply()` 中没有正确初始化
2. 属性强化逻辑没有添加到 `ZZZF_SandboxAgentStatCalculateModel`
3. Buff被提前移除

**解决方法**：
- 在 `OnApply()` 中添加日志输出
- 确认 `StateContainer.AddState()` 被正确调用
- 检查 `Duration` 是否设置正确

### 11.3 技能激活后没有效果

**可能原因**：
1. `Activate()` 返回 `false`
2. 资源不足
3. 冷却未结束

**解决方法**：
- 在 `Activate()` 开头添加日志
- 检查 `CanActivateSkill()` 的返回值
- 使用 `Script.SysOut()` 输出调试信息

### 11.4 翻译文本不显示

**可能原因**：
1. XML ID 与代码中的 `{=ID}` 不匹配
2. XML 文件格式错误
3. 游戏没有重新加载翻译

**解决方法**：
- 确认 `TextObject` 中的ID与XML中的 `id` 完全一致
- 检查XML文件格式（必须是有效的XML）
- 重启游戏

### 11.5 界面打开后崩溃

**可能原因**：
1. `CustomSkillScreenVM` 构造函数中抛出异常
2. XML绑定属性名称错误
3. `SkillCatalog` 加载失败

**解决方法**：
- 检查输出日志中的异常信息
- 确认 XML 中的 `@PropertyName` 与 ViewModel 中的属性名称一致
- 在 `SkillCatalog.LoadFromFactory()` 中添加异常处理

---

## 附录A：常用脚本方法

**文件位置**: `Script.cs`（项目根目录）

```csharp
// 获取范围内的目标（敌人）
List<Agent> GetTargetedInRange(Agent caster, Vec3 position, float range, bool friend = false)

// 计算魔法伤害
void CalculateFinalMagicDamage(Agent caster, Agent target, float damage, DamageType damageType)

// 输出调试信息
void SysOut(string message, Agent agent = null)

// 获取Agent的技能组件
AgentSkillComponent GetActiveComponents(Agent agent)
```

## 附录B：伤害类型枚举

```csharp
public enum DamageType
{
    None = 0,
    ICE_DAMAGE,                    // 冰属性伤害
    FIRE_DAMAGE,                  // 火属性伤害
    ELECTRICITY_DAMAGE,           // 电属性伤害
    TOXIN_DAMAGE,                 // 毒素伤害
    ICE_ENHANCEMENT_FREEZING,    // 冰强化-冻结
    FIRE_ENHANCEMENT_BLASTING,   // 火强化-爆炸
    ELECTRICITY_ENHANCEMENT_PARALYZING,  // 电强化-麻痹
    TOXIN_ENHANCEMENT_CORRUPTING  // 毒强化-腐蚀
}
```

## 附录C：相关文档

| 文档 | 位置 |
|------|------|
| 技能系统执行流程 | `工程/文档/技能系统/技能系统执行流程.md` |
| 新增技能开发流程 | `工程/文档/技能系统/新增技能开发流程.md` |
| 新增Buff开发流程 | `工程/文档/技能系统/新增Buff状态开发流程.md` |
| 物品前后缀系统 | `物品前后缀系统_功能文档.md` |

---

**文档结束**
