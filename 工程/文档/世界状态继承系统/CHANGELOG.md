# 更新日志

## [0.5.0] — 2026-08-03

### Added
- **英雄模板复刻（A 方案：复原"原来的自己"）**
  - 突破原 PRD 第 4 节「排除功能」，实现跨存档的英雄角色复原
  - 范围：**仅玩家本体**（`Hero.MainHero`）+ **玩家招募过且存活的 NPC**（`Clan.PlayerClan.Companions` 且 `IsAlive`）
  - 不复制家族全部成员 / 固定名 NPC 英雄
- 导出层新增英雄档案
  - `HeroProfile` 数据模型：记录 StringId、姓名、来源(player/companion)、文化、等级、技能、特性、职业、性别、体型(`StaticBodyProperties`/`Weight`/`Build`)
  - `BannerlordGameAdapter.GetHeroProfiles()`：导出玩家本体与存活 companion 档案，附逐条 `[HERO]` 日志
- 导入层新增英雄复刻工厂
  - `HeroResurrectionFactory.CreateHeroFromProfile()`：使用 `HeroCreator.CreateSpecialHero` 重建相似游荡英雄（跨存档 StringId 不稳定，采用"重建"而非"找回原对象"）
  - 文化取自模板 `CharacterObject.Culture`（Hero 无 `Culture` 属性，已修正）
  - 通过 `Hero.SetName / SetNewOccupation / SetSkillValue / SetTraitLevel` 还原属性
  - 复刻成功/失败均登记到 `ResurrectedHeroTracker`
- 运行时登记表
  - `ResurrectedHeroTracker`：进程内登记表（重启清空，不参与存档），供验证按钮读取
  - `Entry`：`HeroStringId / Name / Source / CultureId / Level / Status`
- MCM 验证按钮（控制台级）
  - «列出已复刻英雄（验证）»：`LegacyWorldSettingsManager.RunListResurrected` → `LegacyBehavior.DoListResurrected()`
  - `DoListResurrected()`：遍历登记表，`Hero.Find` 查状态，输出信息栏 + `[VERIFY]`/`[BEHAVIOR]` 日志
- 导入日志完善
  - `LegacyImporter` 导入循环加 try/catch，`ImportResult` 新增 `HeroesResurrectFailed`
  - 输出：`英雄模板复刻完成: 成功 X 个 / 失败 Y 个`

### Changed
- `BannerlordGameAdapter` 修复 `Hero.Culture` 不存在问题，改用 `hero.CharacterObject?.Culture?.StringId`
- `HeroResurrectionFactory` 删除 `hero.Culture = culture` 编译错误行，文化由模板决定

### Notes
- 构建零错误
- 新功能让新档世界中出现"原来自己/同伴"的游荡英雄，达成"遇到原来的自己"的效果

---

## [0.4.0] — 2026-07-23

### Added
- **MCM 设置菜单**：基于 `AttributeGlobalSettings` 的完整 MCM 设置面板
  - 菜单路径：游戏 Mod Options → New_ZZZF → LegacyWorld
  - 参照已有模组 `ProjectileTrajectorySystem` 的三层架构（MCM Settings / Data / Manager）
  - 所有设置无需重启游戏，修改即时生效
- MCM 基础控制开关
  - «启用世界状态继承系统»：主开关，关闭后完全禁用导入/导出
  - «存档时自动导出»：关闭后保存游戏不再生成 `Legacy.json`
  - «启用调试日志»：关闭后不再写入 `affix_debug.log`
- MCM 导入类别开关（分组）
  - «恢复王国结构»、«恢复家族数据»、«恢复领地所有权»、«恢复家族经济»、«创建缺失家族»
- MCM 操作按钮（布尔触发标志，拨动后自动复位）
  - «手动导出（拨动即触发）»：立即导出到 `Legacy.json`
  - «手动应用（拨动即触发）»：立即强制导入 `Legacy.json`（忽略同世界检测）
- XML 持久化层：`Modules\New_ZZZF\Settings\LegacyWorldSettings.xml`
  - MCM 面板变更自动同步到 XML 文件
  - MCM 构造时从 XML 载入初始值
- `LegacyService.RefreshSettings()`：从 MCM 数据层加载导入类别设置（每次 Import 前自动调用）

### Changed
- **触发时机修正**：导入仅在新游戏（`OnNewGameCreatedEvent`）时自动触发，载入存档（`OnGameLoadedEvent`）不再触发
- **JSON 配置文件废弃**：`LegacyWorldConfig.cs` + `LegacyWorldConfig.json` 已删除，全部替换为 MCM + XML 持久化
- `LegacySettings` 重构为 Pure POCO，数据来源改为 MCM 数据层（`LegacyWorldSettingsManager`）
- `AffixLogger` 注释修正：日志开关来源改为 `LegacyWorldMCMSettings`
- `SubModule.cs` 移除 Ctrl+F10/F11 手动快捷键，全部改为 MCM 菜单操作

### Notes
- 构建零错误，DLL/PDB 已部署到 `Modules\New_ZZZF\bin\Win64_Shipping_Client\`
- 代码结构参照 `ProjectileTrajectorySystem/Settings/` 的成熟模式

---

## [0.3.0] — 2026-07-22

### Added
- Campaign 运行时集成
  - `LegacyBehavior` CampaignBehavior 实现
  - `LegacyService` 静态服务入口
  - `LegacySubModule` SubModule 入口
- Save/Load 事件挂钩
  - `OnBeforeSaveEvent` → 自动导出
  - `OnGameLoadedEvent` → 自动导入
- 防重复导入机制
  - `applied` 布尔标志，随 `IDataStore` 序列化
  - `appliedWorldId` 记录上次应用的世界 ID
- 完整的生命周期处理
  - 第一次进入地图后执行一次，不在 DailyTick 中重复检查

### Changed
- `LegacyBehavior` 重构：从 DailyTick 触发改为事件驱动

---

## [0.2.0] — 2026-07-20

### Added
- 导入框架（Import Engine）
  - `LegacyImporter` 导入编排入口
  - `KingdomImporter` 王国状态恢复
  - `ClanImporter` 家族状态恢复
  - `SettlementImporter` 领地所有权恢复
- 适配器系统
  - `IGameAdapter` 接口定义
  - `BannerlordGameAdapter` Bannerlord v1.2.x 实现
  - `ObjectFinder` 对象查找辅助类
  - `SettlementChangeFactory` 领地变更工厂
- 导入顺序控制：`Kingdom → Clan → Settlement`，确保依赖合法性
- `LegacySettings` 配置类，支持导入开关控制

### Changed
- 适配器模式引入，Core 层不再直接引用 TaleWorlds.\* 程序集

---

## [0.1.0] — 2026-07-18

### Added
- 项目架构搭建
  - 三层架构设计：Core / Adapter / Runtime
  - 依赖规则：严格单向依赖
- 数据模型定义
  - `LegacyData` 顶层数据容器
  - `KingdomState` 王国状态模型
  - `ClanState` 家族状态模型
  - `SettlementState` 定居点状态模型
- JSON 序列化/反序列化
  - `LegacySerializer` 序列化器
  - 基于 Newtonsoft.Json 实现
- 文件存储管理
  - `LegacyStorage` 存储路径管理
  - 存储路径：`{MyDocuments}\Mount & Blade II Bannerlord\LegacyWorld\Legacy.json`
- 导出系统
  - `LegacyExporter` 导出引擎
  - Kingdom / Clan / Settlement 数据遍历与转换
- 产品文档体系（本文档体系全部创建）

### Notes
- 首个可设计评审版本
- 尚无可编译代码
- 未包含 GUI / MCM 配置
