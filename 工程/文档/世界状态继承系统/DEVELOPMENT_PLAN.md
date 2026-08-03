# 开发计划

## 当前进度

**当前版本**: 0.5.0 (英雄模板复刻 + MCM 验证按钮 + 日志完善)

## 里程碑

| 版本 | 日期 | 状态 | 说明 |
|------|------|------|------|
| v0.1.0 | 2026-07-18 | ✅ 完成 | 架构搭建 + 数据模型 + JSON 序列化 + 导出系统 |
| v0.2.0 | 2026-07-20 | ✅ 完成 | 导入框架（Importer） + 适配器系统 |
| v0.3.0 | 2026-07-22 | ✅ 完成 | Campaign 运行时集成 + Save/Load 事件 |
| v0.4.0 | 2026-07-23 | ✅ 完成 | MCM 设置菜单 + 触发时机修正（仅新游戏导入） |
| v0.5.0 | 2026-08-03 | ✅ 完成 | 英雄模板复刻（遇到原来的自己）+ MCM 验证按钮 + 日志完善 |

## v0.1.0 完成项

- [x] 三层架构设计（Core / Adapter / Runtime）
- [x] 依赖规则：严格单向依赖
- [x] `LegacyData` / `KingdomState` / `ClanState` / `SettlementState` 数据模型
- [x] `LegacySerializer` JSON 序列化/反序列化（Newtonsoft.Json）
- [x] `LegacyStorage` 存储路径管理
- [x] `LegacyExporter` 导出引擎
- [x] 产品文档体系创建

## v0.2.0 完成项

- [x] `LegacyImporter` 导入编排入口
- [x] `KingdomImporter` 王国状态恢复
- [x] `ClanImporter` 家族状态恢复
- [x] `SettlementImporter` 领地所有权恢复
- [x] `IGameAdapter` 接口定义
- [x] `BannerlordGameAdapter` 实现
- [x] `ObjectFinder` 对象查找辅助
- [x] `SettlementChangeFactory` 定居点变更工厂
- [x] `LegacySettings` 配置类

## v0.3.0 完成项

- [x] `LegacyBehavior` CampaignBehavior 实现
- [x] `LegacyService` 静态服务入口
- [x] `LegacySubModule` 独立 SubModule 入口
- [x] Save/Load 事件挂钩（OnBeforeSave → 导出，OnGameLoaded → 恢复状态）
- [x] 防重复导入机制（`_applied` + `_appliedWorldId` 标志）
- [x] 事件驱动生命周期（从 DailyTick 改为事件驱动）

## v0.4.0 完成项

- [x] MCM 设置菜单（`AttributeGlobalSettings`）
- [x] 三层设置架构（Data / Manager / MCMSettings）
- [x] XML 持久化层（`LegacyWorldSettings.xml`）
- [x] 基础控制开关（主开关 / 自动导出 / 调试日志）
- [x] 导入类别开关（王国 / 家族 / 领地 / 经济 / 创建缺失）
- [x] 操作按钮（手动导出 / 手动应用）
- [x] 触发时机修正：导入仅在新游戏触发
- [x] 废弃 JSON 配置文件（删除 `LegacyWorldConfig.cs` + `LegacyWorldConfig.json`）
- [x] 移除非 MCM 快捷键（Ctrl+F10/F11）
- [x] 构建零错误部署

## v0.5.0 完成项

- [x] 英雄模板复刻（A 方案：复原"原来的自己"）
- [x] `HeroProfile` 数据模型（姓名/来源/文化/等级/技能/特性/职业/性别/体型）
- [x] `BannerlordGameAdapter.GetHeroProfiles()` 导出玩家本体 + 存活 companion
- [x] `HeroResurrectionFactory.CreateHeroFromProfile()` 重建游荡英雄
- [x] `ResurrectedHeroTracker` 运行时登记表
- [x] MCM 验证按钮«列出已复刻英雄（验证）»
- [x] `LegacyImporter` 异常容错（try/catch + `HeroesResurrectFailed` 统计）
- [x] 导入/导出日志完善（`[HERO]` / `[VERIFY]` 标签）
- [x] 修复 `Hero.Culture` 不存在编译错误（改为模板 `CharacterObject.Culture`）

## v0.6.0 规划

- [ ] 导入错误处理增强（容错机制，向 v0.5.0 已有英雄容错扩展）
- [ ] 导出数据校验和完整性检查
- [ ] 导入日志优化（统计详情更完善）
- [ ] 边界情况处理（如空世界、残缺 JSON）
- [ ] 性能优化（大量 Kingdom/Clan 时的导入速度）
