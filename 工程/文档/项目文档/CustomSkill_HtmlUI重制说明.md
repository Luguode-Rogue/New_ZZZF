# CustomSkill HtmlUI 重制说明

## 分支

`feature/tacticalmap-htmlui-redesign`

本分支继续承担 New_ZZZF 的 BannerlordHtmlUI 实际 UI 重制验证。
TacticalMap 已作为第一项 HtmlUI 试验；CustomSkill 现在升级为第二项、并采用 HTML-first 接管。

## 统一资源放置规则

本项目涉及 `_Module`、Mod-root 资源、程序集旁 HtmlUI 运行时资源时，统一遵循 BannerlordHtmlUI 的：

[`Project/BUTR_PROJECT_LAYOUT_RULES.md`](https://github.com/Luguode-Rogue/BannerlordHtmlUI/blob/dev/Project/BUTR_PROJECT_LAYOUT_RULES.md)

本文不再自行定义“HTML 源码应该放哪里/Build 后应该复制到哪里”的第二套规则。实际路径必须同时核对：

```text
最终 Modules/<ModId>/ 路径
→ Consumer .csproj Deployment Target
→ 代码中的 Assembly.Location / ContentRoot
```

## 当前架构

`Shift+M` 直接打开 `CustomSkillHtmlUi`。

运行链：

`Shift+M`
→ `CustomSkillHtmlUi.TryOpen()`
→ `new CustomSkillScreenVM()`
→ `HtmlUiService.Pages.Open(customskill.html)`
→ HTML Command / Request / State
→ 现有技能业务逻辑

`CustomSkillScreen` 不再参与运行时入口，也不再通过 Harmony Bridge 托管 HtmlUI。
原 Gauntlet `CustomSkillScreen.cs` 与 XML 暂时保留在工程中作为历史实现与后续清理参考，但不会由技能入口创建。

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
- 当前目标技能熟练度
- 未保存状态显示

法术锻造属于独立 UI，目前不再从这个 HTML 选择器唤起旧 Gauntlet 页面；需要继续 HTML 化时应单独制作 HtmlUI 页面。

## 业务逻辑复用原则

HTML 不复制技能系统规则。

仍直接复用：

- `CustomSkillScreenVM`
- `SkillCatalog`
- `HeroSkillData`
- `SkillUIData`
- `SkillConfigManager`
- 原有技能熟练度计算/读取逻辑

HtmlUI 只承担：

- 页面布局
- 状态展示
- 鼠标/键盘输入
- Command / Request 调用
- State 发布与渲染

## 输入与生命周期

打开时：

- 创建独立 `CustomSkillScreenVM`
- 注册 `GameStateManager` active-state disable request，暂停大地图时间推进
- HtmlUI 进入 `Captured` 输入模式
- HTML 页面接管全部前台交互

关闭时：

- Close HTML page
- 释放 active-state disable request
- `CustomSkillScreenVM.OnFinalize()`
- 不创建/不 Pop `CustomSkillScreen`

## 运行时资源

资源的工程源位置与最终部署位置不在本文单独规定；统一查：

[`BUTR_PROJECT_LAYOUT_RULES.md`](https://github.com/Luguode-Rogue/BannerlordHtmlUI/blob/dev/Project/BUTR_PROJECT_LAYOUT_RULES.md)

并结合本 Consumer 的 `.csproj`、`SubModule.cs` / HtmlUI Consumer 代码确认 `Assembly.Location` 对应的 ContentRoot。

本项目不要求用户手工复制 HTML/CSS/JS；构建/部署目标负责把运行时资源放到代码实际读取的位置。

## 后续清理方向

当 HtmlUI 版本完成实机验收后：

1. 继续把技能系统中仍依赖 Gauntlet 的 UI-only 代码迁出。
2. 单独 HTML 化法术锻造界面。
3. 删除 `CustomSkillHtmlUiBridgePatch`（当前已删除）。
4. 最终删除废弃的 `CustomSkillScreen` / Gauntlet XML UI 文件。

当前原则：**业务逻辑复用，UI 层完全 HTML 化；旧 UI 只作为代码参考，不再作为运行时依赖。**
