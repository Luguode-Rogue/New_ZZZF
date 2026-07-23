# 已知问题

## 当前版本 (v0.4.0)

| 编号 | 问题描述 | 严重度 | 状态 | 备注 |
|------|----------|--------|------|------|
| KNW-01 | «创建缺失家族» 功能尚未完全实现 — 框架已搭建，但 `CreateClan` 的实际资源分配逻辑需要 Bannerlord v1.2.x 的运行时验证 | 低 | 待验证 | MCM 开关已提供，实际调用路径正确，待集成测试 |
| KNW-02 | 导入过程中如果某个 Kingdom 或 Clan 在新世界中已被消灭，导入后可能重新激活 | 中 | 已知 | 当前设计下这是预期行为（还原旧世界状态），但未来可考虑添加"是否复活已消灭实体"选项 |
| KNW-03 | `SettlementChangeFactory` 变更所有权时，绑定村庄的地图图标刷新可能存在延迟（下一个游戏日才刷新） | 低 | 已知 | 游戏内部渲染 tick 延迟，不影响数据正确性 |
| KNW-04 | MCM 按钮«手动应用»在 Campaign Map 尚未完全加载时拨动可能不生效 | 中 | 待修复 | `HourlyTickEvent` 需要 Campaign 完全初始化后才触发；建议玩家进入地图后再操作 |
| KNW-05 | 导出文件 `Legacy.json` 不包含玩家角色的影响力和声望数据 — 当玩家作为王国统治者时，王国信息无法捕获玩家角色的 ID | 低 | 已知 | 玩家角色通常是 Clan.PlayerClan，不影响 AI 王国恢复 |

## 已解决

| 编号 | 问题描述 | 原版本 | 解决版本 | 解决方案 |
|------|----------|--------|----------|----------|
| FIX-01 | `SettingPropertyButton` 不支持方法，编译错误 | v0.4.0-dev | v0.4.0 | 改用 bool 触发标志属性，`OnPropertyChanged` 中调用管理器方法 |
| FIX-02 | `CampaignTickEvent` 在 Bannerlord v1.2.x 中不存在 | v0.4.0-dev | v0.4.0 | 替换为 `HourlyTickEvent` |
| FIX-03 | `LegacySettings` 被误删除导致引用编译错误 | v0.4.0-dev | v0.4.0 | 恢复 `LegacySettings` 为 Pure POCO |
| FIX-04 | 旧版 `LegacyWorldConfig.cs` + `LegacyWorldConfig.json` 存在配置散乱 | v0.3.0 | v0.4.0 | 删除 JSON 配置，统一为 MCM + XML 持久化 |
| FIX-05 | Ctrl+F10/F11 手动快捷键与游戏内置键位冲突 | v0.3.0 | v0.4.0 | 移除快捷键，改为 MCM 按钮操作 |
| FIX-06 | 载入存档也触发导入，可能污染已有进度 | v0.3.0 | v0.4.0 | 修正为仅 `OnNewGameCreatedEvent` 触发 |
