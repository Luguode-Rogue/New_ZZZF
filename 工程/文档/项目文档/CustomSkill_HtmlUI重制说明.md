# CustomSkill HtmlUI 重制说明

## 分支

`feature/tacticalmap-htmlui-redesign`

本分支继续承担 New_ZZZF 的 BannerlordHtmlUI 实际 UI 重制验证。
TacticalMap 已作为第一项 HtmlUI 试验；本次新增新技能配置界面的 HTML 版本。

## 当前目标

将现有 `CustomSkillScreen` 的 v2 技能配置界面改造成 HtmlUI 版本，验证 Framework 在复杂 MVVM UI 上的实际可用性。

旧 Gauntlet UI **暂不删除**。HTML UI 通过 Harmony 接到现有 `CustomSkillScreen` 生命周期，底层继续使用原 `CustomSkillScreenVM`，避免复制技能系统逻辑。

## 当前 HTML 版本覆盖

- 目标类型：队伍成员 / 兵种模板 / 领主 NPC
- 调试模式切换
- 目标列表
- 8 个技能槽位
- 技能目录
- 按槽位类型过滤技能
- 技能搜索
- 技能选择与回填槽位
- 目录键盘导航
- 撤销
- 应用
- 导出配置
- 法术锻造入口
- 当前目标技能熟练度
- 未保存状态显示

## 运行时资源

HTML 源目录：

`工程/New_ZZZF/GUI/Html/CustomSkill/`

运行时目录：

`Modules/New_ZZZF/bin/Win64_Shipping_Client/CustomSkillUI/`

当前 `Directory.Build.targets` 已加入自动复制目标；如果本地构建链未执行该 Target，仍可手动复制整个 `CustomSkill` 目录到上述运行时目录。

## 生命周期

1. `M` 打开原 `CustomSkillScreen`
2. Harmony `OnInitialize` 获取原 `CustomSkillScreenVM`
3. HtmlUI 自动打开 `customskill.html`
4. HTML 命令直接调用原 VM 的公开命令/必要的私有选择方法
5. HTML 每 100ms 检查并发布 VM 状态变化
6. 原 `CustomSkillScreen` finalize 时，HTML 页面同步关闭

## 设计原则

- 不复制 `SkillCatalog` / `HeroSkillData` / `SkillConfigManager` 等业务逻辑。
- 不直接让 New_ZZZF 引用 WebView2 类型；透明 Overlay 仍通过 Framework Host 的反射接入。
- 旧 Gauntlet 文件继续保留，HTML 版本通过独立资源目录运行。
- 当前重点是验证“复杂技能配置 UI 能否以 Framework Consumer 的方式制作”，不是立即删除旧 UI。
