# BattleHud 性能优化后不可见修复记录

**日期**：2026-09-21  
**模块**：New_ZZZF / BattleHud / BannerlordHtmlUI  
**结果**：用户实机确认 HUD 已恢复显示  
**改动前备份**：Git 提交 `5ce579c`（`chore: backup working tree before battle HUD optimization`）

## 1. 问题背景

BattleHud 的属性、技能选择和冷却发生变化时，会频繁跨 WebView2 边界更新页面，造成短暂帧时间尖峰，明显拉低 1% Low。优化目标是减少跨线程、跨进程脚本调用和重复的完整状态序列化，同时保持 HUD 首次显示、页面重载和状态变化全部可靠。

第一次优化后出现严重回归：战斗 HUD 完全不可见；切换法术仍有明显卡顿。战术地图可以正常显示，因此问题不是 WebView2 整体失效。

## 2. 现象与关键证据

游戏日志能够证明：

- `New_ZZZF.BattleHud` Surface 注册成功；
- Surface 在 Mission 中成功执行 Show；
- BattleHud iframe 已创建，文档 `readyState=complete`；
- iframe 内存在 `window.game`；
- TacticalMap 会发出 `framework.getStateSnapshot`；
- BattleHud 没有发出对应的状态快照请求，也没有取得可渲染状态。

BattleHud HTML 初始状态为：

```html
<div id="hud" class="hidden">
```

只有 `render(state)` 收到 `available=true` 的状态后才会移除 `hidden`。因此，“Surface 已显示”只说明 iframe 被挂载，不能证明 HUD 已完成业务初始化。

## 3. 错误排查过程

### 3.1 过早把 Surface 日志当成显示成功

错误判断：看到 Surface registered、Surface shown、Frame created 后，认为 HUD 显示链路正常，把注意力全部放在 C# 状态拆分上。

失败原因：这些日志只覆盖宿主和 iframe 生命周期，不覆盖页面脚本是否运行、作用域是否正确、首屏状态是否到达，以及 `hidden` 是否被移除。

### 3.2 根据被截断的诊断 URL 怀疑 owner 参数丢失

共存探针为了便于输出使用了类似 `split('?')[0]` 的结果，因此日志中看不到查询参数。曾据此怀疑 iframe URL 没有携带 owner。

后续检查框架 `BuildSurfaceUriOnHost()` 后确认，共存 URL 会附加：

```text
__bannerlord_htmlui_owner
__bannerlord_htmlui_surface
```

失败原因：诊断输出省略查询参数，不等于真实 URL 缺少查询参数。不得用经过裁剪的日志证明完整 URL 内容。

### 3.3 只优化更新路径，没有保住首屏恢复路径

性能优化将状态拆分为完整结构、属性、计时器和选槽四类，这是正确方向；但页面显示仍依赖隐式默认 owner 和 retained-state 首次快照。一旦首次恢复没有完成，HUD 会永久保持隐藏。

失败原因：只验证了“后续更新更轻”，没有验证“页面刚加载时一定能得到完整状态”。基础 UI 的可见性不能只依赖一次隐式快照。

### 3.4 没有尽早对比正常组件的桥接请求

TacticalMap 正常请求状态，而 BattleHud 没有任何状态请求，这是最有价值的对照证据。此前没有优先沿这条差异定位，导致排查绕到了层级、透明背景、共存宿主等次要方向。

## 4. 根因

BattleHud iframe 虽然成功加载，但前端没有可靠完成首屏状态恢复。页面没有取得 `available=true` 的完整状态，因此 `#hud.hidden` 始终未被移除。

根因不在 Surface 注册、ZIndex 或透明背景，而在“页面已加载”到“页面取得 BattleHud 状态”之间缺少可靠、可独立验证的初始化握手。

## 5. 正确修复

### 5.1 显式绑定 BattleHud scope

前端不再依赖宿主页面推断出的默认 `game.app`：

```javascript
const app = window.game.scope('New_ZZZF.BattleHud');
```

这保证 BattleHud 即使作为其他页面宿主下的共存 iframe，也始终使用自己的命令、请求和状态命名空间。

### 5.2 增加主动完整状态请求

C# 在 BattleHud scope 中注册：

```csharp
_scope.RegisterRequest("getState", _ =>
    Task.FromResult<object>(BuildState(_boundComponent)));
```

页面在脚本加载后以及 `window.game.ready()` 完成后主动请求一次：

```javascript
app.request('getState').then(render);
```

这样 retained-state 快照即使因装载时序没有完成，HUD 仍能通过显式请求取得首屏完整状态并显示。

### 5.3 保留拆分后的轻量更新

首屏使用完整状态；运行期按变化类型更新：

| 状态键 | 内容 | 触发时机 |
|---|---|---|
| `battleHud` | 技能结构与完整首屏状态 | 组件绑定、技能结构变化 |
| `battleHud.vitals` | 耐力、法力、护盾、复活次数 | 对应整数可见值变化 |
| `battleHud.timers` | GCD 与各技能冷却 | 冷却开始、结束或主动变化 |
| `battleHud.selection` | 当前法术槽位 | 选择变化 |

冷却倒计时由 HTML 本地推进，不再每帧跨 WebView2 同步。属性和选槽变化也不再重复构造完整技能状态。

## 6. 正常实施与排查流程

以后修改 BattleHud 或其他 HtmlUI Surface，必须按以下顺序执行：

1. 修改前提交 Git 备份，明确工程源码目录与 Mod 运行目录。
2. 先建立基线：记录 Surface 注册、Show、iframe 创建、页面 ready、状态请求和最终 DOM 显示。
3. 将性能问题按“数据产生、跨桥传输、页面渲染”三层拆开，避免一次同时重写全部链路。
4. 优化更新频率时，必须单独保留首屏完整状态获取路径。
5. 共存 iframe 中显式指定 Consumer scope，不依赖宿主页面默认 owner。
6. 对照正常页面的请求日志；页面加载成功但没有状态请求时，优先检查前端初始化，不要先猜 ZIndex 或 CSS。
7. 分别验证：进入战斗首显、玩家组件延迟创建、页面重载、属性变化、切换法术、冷却开始与结束、TacticalMap 共存。
8. 用户实机确认显示与卡顿均正常后，才把本轮问题标记为解决。

## 7. 必须保留的诊断原则

HtmlUI 显示链路必须逐层证明：

```text
Surface 注册
  -> Surface Show
  -> iframe 创建并完成加载
  -> window.game/runtime 可用
  -> Consumer scope 正确
  -> 状态请求或快照成功
  -> render 收到 available=true
  -> DOM 移除 hidden
  -> 用户实际看见 HUD
```

上一层成功不能代替下一层验证。尤其要牢记：

> `Surface shown` 不等于业务 HUD 已显示；iframe `readyState=complete` 也不等于页面已取得状态。

## 8. 涉及源码

- `工程/New_ZZZF/BattleHud/BattleHudHtmlUi.cs`
- `工程/New_ZZZF/Systems/AgentSkillComponent.cs`
- `工程/New_ZZZF/_Module/UI/BattleHud/index.html`

工程源码是修改入口；根目录 `UI/BattleHud/index.html` 属于编译同步后的 Mod 运行资源，不应作为人工修改源。

## 9. 最终验证

2026-09-21，用户实机确认 BattleHud 已恢复显示。本次记录的结论以该次实机结果为准。

