# ProjectileTrajectorySystem

> **骑马与砍杀2：霸主 (Mount & Blade II: Bannerlord)** 弹道轨迹可视化 Mod，版本 v1.4.0.0

实时计算并可视化战斗中投射物的飞行轨迹，同时提供敌人高亮、抬头虚化、近敌慢动作、移动目标预瞄等辅助功能。

---

## 功能一览

| 功能 | 描述 | 对应模块 |
|------|------|----------|
| 🏹 **玩家弹道** | 手持远程武器（弓/弩/投掷）瞄准时实时显示预测弹道 | `PlayerTrajectorySystem` |
| 🏰 **攻城武器弹道** | 投石车、弩炮等攻城器械的弹道预测 | `PlayerTrajectorySystem` |
| 👁️ **飞行投射物轨迹** | 已射出的箭矢/弩箭的真实飞行路径追踪（绿色） | `MissileTrajectorySystem` |
| 🔴 **敌人弹道** | 当敌人瞄准玩家时显示其预测射击线（红色） | `EnemyTrajectorySystem` |
| ✨ **敌人高亮** | 瞄准玩家的敌方远程单位自动添加红色轮廓描边 | `EnemyOutlineSystem` |
| 🌫️ **抬头虚化** | 高角度仰射时虚化玩家/攻城武器模型，防止遮挡视野 | `AlphaBlurSystem` |
| 🎯 **移动目标预瞄** | 迭代拟合算法计算移动敌人的提前量（实验性功能） | `LeadPredictionSystem` |
| ⏸️ **近敌慢动作** | 敌人靠近时拉弓瞄准自动减慢时间流速 | `SlowMotionSystem` |

## 渲染方式

支持两种渲染方式，可在 MCM 设置中切换：

| 方式 | 优势 | 劣势 |
|------|------|------|
| **DebugLine** (默认) | 性能极高，无性能开销 | 开启 DLSS 时会显示异常 |
| **GameEntity 图标** | DLSS 下显示正常 | 视觉效果较差 |

## 依赖项

### 必须前置 Mod

| Mod | 最低版本 |
|-----|---------|
| Bannerlord.Harmony | v2.2.2+ |
| Native | - |
| SandBoxCore | - |
| Sandbox | - |
| StoryMode | - |
| CustomBattle | - |

### 编译依赖

- [Harmony](https://github.com/pardeike/Harmony) v2.3.3 - 运行时方法补丁
- [ButterLib](https://github.com/BUTR/Bannerlord.ButterLib) - 社区工具库
- [MCM v5](https://github.com/Aragas/Bannerlord.MBOptionScreen) - 设置 UI 框架
- [UIExtenderEx](https://github.com/BUTR/Bannerlord.UIExtenderEx) - UI 扩展
- Newtonsoft.Json (随游戏分发)
- TaleWorlds SDK (随游戏分发)

## 快速开始

### 编译

```bash
# 1. 设置游戏目录环境变量
set BANNERLORD_GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord

# 2. 确保前置 Mod (Harmony, ButterLib, MCM, UIExtenderEx) 已放置在游戏 Modules 目录

# 3. 编译
dotnet build
```

### 安装

将编译产物 (`bin/`) 复制到游戏 `Modules/ProjectileTrajectorySystem/` 目录下。

### 配置

- **MCM 方式**：游戏中通过 MCM 菜单 (Options → Mod Options → ProjectileTrajectorySystem) 调整所有设置
- **手动方式**：直接编辑 `Modules/ProjectileTrajectorySystem/ProjectileTrajectorySettings.xml`，支持热重载

---

## 项目结构

```
ProjectileTrajectorySystem/
├── SubModule.cs                          # Mod 入口点（MBSubModuleBase）
├── SkillSystemBehavior.cs                # 薄编排层（MissionLogic，~240行）
├── ProjectileTrajectorySystem.cs         # 薄外观层（静态门面，~180行）
│
├── Core/                                 # 🟢 纯计算/渲染层（无状态）
│   ├── TrajectoryPhysics.cs              # 欧拉积分 + 抛物线解算
│   ├── TrajectoryRenderer.cs             # GameEntity + DebugLine 双管线渲染
│   ├── SiegeWeaponHelper.cs              # 攻城武器反射接口
│   └── LeadPredictionMath.cs             # 移动目标提前量迭代算法
│
├── Systems/                              # 🟡 有状态子系统
│   ├── GameEntityPool.cs                 # 对象池 + 颜色缓存
│   ├── AlphaBlurSystem.cs                # 仰射虚化（平滑插值）
│   ├── PlayerTrajectorySystem.cs         # 玩家弹道（远程 + 攻城）
│   ├── EnemyTrajectorySystem.cs          # 敌人弹道追踪
│   ├── EnemyOutlineSystem.cs             # 敌人红色描边
│   ├── LeadPredictionSystem.cs           # 移动目标预瞄
│   ├── SlowMotionSystem.cs               # 慢动作
│   └── MissileTrajectorySystem.cs        # 投射物弹道
│
├── Settings/                             # 🔵 MCM 设置（三层架构）
│   ├── ProjectileTrajectorySettings.cs   # MCM UI 层
│   ├── ProjectileTrajectorySettingsData.cs # XML 数据模型层
│   └── ProjectileTrajectorySettingsManager.cs # 管理层（含热重载）
│
└── DLC/                                  # 🟣 DLC 适配层
    └── NavalDLCTrajectorySupport.cs      # 海战 DLC 支持（纯反射）
```

### 四层架构

```
┌─────────────────────────────────────┐
│         Settings 层 (配置)          │
│   MCM UI → XML Data → Manager      │
├─────────────────────────────────────┤
│         Core 层 (纯计算/渲染)       │
│   Physics / Renderer / Helper / Math│
├─────────────────────────────────────┤
│         Systems 层 (有状态子系统)    │
│   Player/Enemy/Missile/Alpha/...    │
├─────────────────────────────────────┤
│         DLC 层 (扩展适配)           │
│   NavalDLC (反射)                   │
└─────────────────────────────────────┘
```

---

## 核心物理模型

弹道计算采用欧拉积分法 (步长 0.01s)：

```
速度衰减: v -= v_norm * friction * v² * dt
重力:     v_z -= 9.81 * dt
位置:     p += v * dt
```

其中 `friction` 由武器类型通过引擎 API `ItemObject.GetAirFrictionConstant()` 获取。

---

## 技术亮点

1. **对象池**：GameEntity 使用 `Stack<GameEntity>` 实现对象池，避免频繁创建销毁
2. **颜色缓存**：GameEntity 描边颜色通过 `ColorCache` 字典缓存，相同颜色跳过 `SetContourColor` 调用
3. **Alpha 平滑过渡**：抬头虚化使用平滑插值算法（`current + (target - current) * 0.15`），避免突变
4. **XML 热重载**：`FileSystemWatcher` 监听配置文件变化，无需重启即可生效
5. **反射回退**：NavalDLC 通过纯反射实现，无 DLC 时自动退化
6. **双渲染管线**：GameEntity 和 DebugLine 可独立开关，适配不同场景
7. **薄编排层**：`SkillSystemBehavior` 仅 ~240 行，委托给 8 个子系统，便于维护和扩展

---

## 文档索引

| 文档 | 用途 |
|------|------|
| [ARCHITECTURE.md](ARCHITECTURE.md) | 架构设计：四层架构、类关系、数据流 |
| [DEV_GUIDE.md](DEV_GUIDE.md) | 开发指南：文件详解、API 签名、扩展指南 |
| [TEST_CASES.md](TEST_CASES.md) | 测试用例：12 大类手动测试覆盖 |
| [REFACTOR_LOG.md](REFACTOR_LOG.md) | 重构记录：过程、问题、经验教训 |
| [CODE_REVIEW.md](CODE_REVIEW.md) | 代码审查：历史参考 |
| [REFACTOR_PLAN.md](REFACTOR_PLAN.md) | 重构计划：历史参考 |

---

## 许可证

社区 Mod，自由使用。
