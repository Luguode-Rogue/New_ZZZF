---
name: update-docs-and-testcases
overview: 为重构完成的项目编写测试用例并更新工程文档，包括：1) 游戏内手动测试用例；2) 更新 README.md/ARCHITECTURE.md/DEV_GUIDE.md 反映新架构；3) 新增重构记录文档
todos:
  - id: create-test-cases
    content: 创建 TEST_CASES.md，编写覆盖所有 8 大功能模块的手动测试用例
    status: completed
  - id: update-readme
    content: 更新 README.md，反映重构后的项目结构和功能清单
    status: completed
  - id: update-architecture
    content: 重写 ARCHITECTURE.md，反映新的四层架构和类关系
    status: completed
  - id: update-devguide
    content: 更新 DEV_GUIDE.md，反映新文件组织和 API 签名
    status: completed
  - id: create-refactor-log
    content: 创建 REFACTOR_LOG.md，记录重构过程、遇到的问题和解决方案
    status: completed
---

## Product Overview

ProjectileTrajectorySystem 是一个 Mount & Blade II: Bannerlord 弹道轨迹可视化 Mod。重构已成功完成，用户确认功能正常。现在需要：(1) 编写测试用例覆盖所有子系统；(2) 全面更新工程文档，反映重构后的新架构和重构过程中遇到的问题。

## Core Features

- 编写覆盖 8 大功能模块的手动测试用例（玩家弹道、敌人弹道、投射物轨迹、敌人高亮、抬头虚化、预瞄、慢动作、DLC支持）
- 更新 README.md 反映重构后的项目结构和功能
- 更新 ARCHITECTURE.md 反映新四层架构（Settings/Core/Systems/DLC）
- 更新 DEV_GUIDE.md 反映新的文件组织和 API 变化
- 新增重构记录文档，记录重构过程、遇到的问题及解决方案

## Tech Stack

- 文档格式: Markdown
- 测试方式: Bannerlord Mod 无法使用传统单元测试框架（依赖游戏引擎运行时），采用手动游戏内测试用例
- 项目语言: C# 10.0 (.NET Framework 4.7.2 / .NET 6)

## Implementation Approach

基于对整个代码库的深入理解，制定系统化的手动测试用例，覆盖每个子系统的核心路径和边界条件。文档更新需全面反映重构后的四层架构变化，同时保留重构历史记录以供后续参考。

### 测试用例设计原则

- Bannerlord Mod 运行在游戏引擎内部，无法脱离引擎进行单元测试
- 测试用例以"游戏内操作步骤 + 预期结果"的形式编写
- 每个子系统独立测试，再进行集成测试
- 包含设置开关测试（MCM + XML 热重载）

### 文档更新原则

- README.md: 面向用户，反映功能、结构、依赖
- ARCHITECTURE.md: 面向架构师，反映新四层架构、数据流、依赖关系
- DEV_GUIDE.md: 面向开发者，反映新文件组织、API 签名、扩展指南
- REFACTOR_LOG.md: 新增，记录重构历程和经验教训

## Directory Structure

```
ProjectileTrajectorySystem/
├── README.md                    # [MODIFY] 更新项目结构、功能描述
├── ARCHITECTURE.md              # [MODIFY] 重写为新的四层架构
├── DEV_GUIDE.md                 # [MODIFY] 更新文件详解和API文档
├── REFACTOR_LOG.md              # [NEW] 重构记录：过程、问题、经验
├── TEST_CASES.md                # [NEW] 手动测试用例文档
├── CODE_REVIEW.md               # [KEEP] 保留，作为历史参考
├── REFACTOR_PLAN.md             # [KEEP] 保留，作为历史参考
└── (所有 .cs 文件不变)
```