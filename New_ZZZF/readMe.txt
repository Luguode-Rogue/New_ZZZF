YourMod/                             # Mod根目录
├── SubModule.xml                    # Mod元数据声明
├── SubModule.cs                     # Mod入口类
├── ModuleData/                      # 配置数据
│   └── troop_skills.xml             # 兵种技能配置
├── SkillSystem/                     # 技能系统核心代码
│   ├── SkillBase.cs                 # 技能基类与枚举定义
│   ├── AgentSkillComponent.cs       # Agent技能组件（绑定到每个单位）
│   ├── SkillConfigManager.cs        # 配置加载与管理（单例）
│   ├── SkillFactory.cs              # 技能实例工厂
│   ├── SkillSystemBehavior.cs       # 全局Mission行为监听器
│   └── Skills/                      # 具体技能实现
│       ├── Actives/                 # 主动技能
│       │   ├── ShieldChargeSkill.cs # 主主动：盾牌冲锋
│       │   └── AdrenalineRushSkill.cs # 副主动：肾上腺素爆发
│       ├── Passives/                # 被动技能
│       │   ├── IronWillPassive.cs   # 钢铁意志（被动栏）
│       │   └── WarriorPassive.cs    # 战士低级被动（法术栏）
│       ├── Spells/                  # 法术技能
│       │   ├── FireballSkill.cs     # 火球术
│       │   └── HealSkill.cs         # 治疗术
│       └── CombatArts/              # 战技
│           └── ShieldBashSkill.cs   # 盾击
└── Assets/                          # 可选：资源文件（粒子特效、音效等）
    ├── Particles/                   # 技能特效预设
    └── Sounds/                      # 技能音效文件



        SubModule.cs

路径: YourMod/SubModule.cs

职责: Mod入口，注册技能系统到游戏。

SkillSystem/SkillSystemBehavior.cs

职责: 监听Agent创建事件，挂载技能组件。

SkillSystem/AgentSkillComponent.cs

职责: 管理单个Agent的技能槽、冷却、输入响应。

SkillSystem/SkillConfigManager.cs

职责: 加载并缓存兵种技能配置（XML）。

SkillSystem/SkillFactory.cs

职责: 将技能ID映射到代码中硬编码的技能实例。

SkillSystem/SkillBase.cs

职责: 技能基类与枚举定义。

ModuleData/troop_skills.xml

职责: 兵种技能的外部配置。

Skills/FireballSkill.cs

职责: 具体技能实现示例（法术）。





