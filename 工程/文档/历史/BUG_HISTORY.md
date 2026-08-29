# Bug 修复经验库

> 这是全工程 Bug 排错经验的统一入口。**本文件不替代原始 Bug 文档。** 原始记录继续保留，确保以后遇到类似问题时可以直接复用完整排查路径。

## 经验保留标准

每个问题尽量保留完整链路：

`现象 -> 触发条件 -> 日志/堆栈 -> 初始假设 -> 排查过程 -> 失败方案 -> 失败原因 -> 根因 -> 修复 -> 验证 -> 版本/API条件`

不得只保留“最终改了哪几行代码”。失败方案尤其重要，因为它能避免以后重复走已经证伪的路线。

## 目前已确认的经验类别

### UI / Gauntlet / HtmlUI

- Scroll / Clip 裁剪问题
- 技能槽位点击与跳转
- UI 闪退与大地图暂停机制
- UI 层级、点击命中和透明覆盖层
- 不同 Bannerlord 版本的 `GlobalPosition` / Rectangle API 差异
- HtmlUI 生命周期、焦点、输入模式、窗口显隐与 WebView2 初始化

原始来源：`../Bug修复记录/`、`../UI开发文档/`、`../工作日志/`。

### 存档 / 数据持久化

- 词缀/物品系统存档读档丢失
- Debug.Print / 日志替换引发的行为变化
- 世界状态持久化相关问题

### API / 版本迁移

- MissingFieldException / MissingMethodException
- UI 几何 API 类型变化
- 私有 API / 反射字段版本变化
- TaleWorlds 类型行为变化

### TacticalMap

重点保留：

- 战场边界取值错误导致地图比例异常
- Terrain / Texture API 兼容问题
- 地图绘制性能问题
- Agent / Formation 扫描过重
- 点击坐标转换与指令下发
- 镜头 Harmony / 私有字段兼容
- HtmlUI 与原 Gauntlet 生命周期、焦点和输入冲突

#### TacticalMap N 键切换回归（2026-08-29）

```text
问题：
TacticalMap 已打开并处于 CompactPassive，但按 N 无法切换到 FullInteractive。

触发条件：
战斗 Mission 中 TacticalMap 页面已成功打开；CustomSkill 未显示。

现象：
BannerlordHtmlUI 输入追踪能够确认其它键正常；N 的临时兜底日志能够观测到 N，但地图状态始终保持 CompactPassive。

日志/堆栈：
TacticalMap 日志出现：
TacticalMap N fallback input observed: key=N pressed=True pageVisible=True mode=CompactPassive customSkillVisible=False
但未出现：
TacticalMap N fallback toggled mode=FullInteractive

初始假设：
N 没有进入 Bannerlord 输入系统，或 HtmlUI/InputController 吞掉 N。

排查 1：
InputTraceLogger 能正常看到 M、Shift、Alt、鼠标等游戏输入，因此输入系统并非整体失效。

排查 2：
TacticalMap fallback 明确观察到 Input.IsKeyDown(N) 上升沿，因此 N 实际已经进入 New_ZZZF Consumer。

失败方案：
在 TacticalMapNKeyFallback.cs 中同时检查 IsKeyPressed 与 IsKeyDown，并在 Harmony postfix 中补切换。

失败原因：
旧 fallback 在看到 `pressed=true` 时主动 return，把切换责任错误地交给 SubModule 原入口；原入口又没有产生对应的实际切换结果，形成双路径互相等待。

最终根因：
N 键在 `IsKeyPressed` 的读取时序下不能可靠作为 TacticalMap 状态切换依据；同时存在第二套 fallback 输入路径，导致责任边界混乱。

最终修复：
移除 TacticalMapNKeyFallback Harmony 输入补丁；TacticalMap N 键统一由 `New_ZZZF.SubModule.OnApplicationTick` 作为 Consumer hotkey owner，通过 `Input.IsKeyDown(TacticalSettings.Instance.ToggleKey)` 的上升沿一次性调用 `TacticalMapHtmlUi.ToggleInteractive()`。

验证：
当前源码已核对为唯一 Consumer 热键路径；实际切换结果等待下一轮实机验证，当前不得标记为“已实机通过”。

适用版本：
当前 New_ZZZF master；Bannerlord 版本以当前工程实际构建配置为准。

关键词：
TacticalMap、N、Input.IsKeyPressed、Input.IsKeyDown、Consumer hotkey、CompactPassive、FullInteractive、HtmlUI
```

原始核心审计：[2026-08-01 TacticalMap 审计](../UI开发文档/2026-08-01_战术地图TacticalMap_审计文档.md)。

## 原始记录索引

### Bug 修复记录

完整原始记录位于 `../Bug修复记录/`。包括历史 API 迁移、技能 UI、存档、点击、暂停、日志等问题，以及该目录中的其他历史 Bug 文档。

### 工作日志中的 Bug / 踩坑记录

部分问题是在工作日志中形成的完整排错记录，而不是独立 Bug 文件。

**这些原始文件不得删除。** 后续整理只允许建立新的分类索引、将重复结论提升到主文档，不能以摘要替代原始排错过程。

## 快速复用格式

以后修复新问题时，统一增加：

```text
问题：
触发条件：
现象：
日志/堆栈：
初始假设：
排查 1：
排查 2：
失败方案：
失败原因：
最终根因：
最终修复：
验证：
适用版本：
关键词：
```
