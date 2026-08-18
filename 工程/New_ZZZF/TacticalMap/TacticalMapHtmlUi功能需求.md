# TacticalMap HTMLUI 功能需求

> 版本：v1.0
> 分支：`feature/tacticalmap-htmlui-redesign`
> 用途：TacticalMap HTMLUI 重制版的唯一功能基线。

## 1. 项目目标

将 TacticalMap 原有 Gauntlet 小地图重制为 HTMLUI。HTMLUI 负责地图显示、界面状态和鼠标交互；TacticalMap Core 负责地形、单位、编队、命令和镜头等游戏逻辑。

旧版 TacticalMap 的交互方案不作为本需求依据。

## 2. 默认状态

进入支持 TacticalMap 的战场后默认：

- 地图显示。
- 使用小地图布局。
- 不占用鼠标。
- Bannerlord 正常接收原生输入。

状态：`CompactPassive`。

## 3. N 键状态机

### 3.1 短按 N

短按 N 在以下两个状态之间切换：

`CompactPassive <-> CompactInteractive`

进入 `CompactInteractive` 后允许地图接收鼠标，但不得让 Bannerlord 因失去前台焦点而静音。

### 3.2 长按 N

长按 N 用于切换地图显隐和全屏：

`Compact -> Full -> Hidden -> Compact`

对应状态：

- `CompactPassive`
- `CompactInteractive`
- `FullPassive`
- `FullInteractive`
- `Hidden`

从隐藏状态重新显示时恢复为 `CompactPassive`。

操作状态进入全屏时保留操作状态；非操作状态进入全屏时保持非操作状态。

## 4. ESC

ESC 只退出地图操作状态，不关闭地图：

- `CompactInteractive -> CompactPassive`
- `FullInteractive -> FullPassive`

## 5. 鼠标交互

仅 `CompactInteractive` / `FullInteractive` 接收地图鼠标。

### 左键：移动命令

点击地图位置，将 UV 转换为世界坐标并执行移动命令。

### 中键：镜头

点击地图位置，将对应世界坐标交给 CameraController，切换战场镜头目标。

### 右键：朝向命令

点击地图位置，将对应世界坐标转换为朝向命令。

本项目不存在攻击移动功能。

## 6. 地图显示

地图静态层应包含：

- 战场地形。
- 高度差表现。
- 森林。
- 悬崖。
- 水域。
- 风险/地形辅助层。

地形数据在战场建立后烘焙并复用。

## 7. 单位显示规则

根据玩家与单位的距离决定显示精度。

### 近距离

小于 `AgentDetailDistance`：显示全部 Agent。

### 远距离

超过 `AgentDetailDistance`：不显示单个 Agent，改为显示 Formation。

## 8. Agent 信息

Agent 至少提供：

- UV 位置。
- 阵营。
- 玩家阵营标识。
- 中立标识。

## 9. Formation 信息

编队直接提供完整信息，不采用仅显示简化图标的方案。至少包括：

- 编队名称。
- 编队编号。
- 阵营/敌我关系。
- 人数。
- 平均位置。
- 朝向。
- 地图位置。

全屏地图可以提供编队列表及选中编队详细信息。

## 10. 玩家与镜头目标

玩家：青色外环 + 黄色中心，并显示玩家朝向。

镜头目标：橙色菱形。

## 11. 图例

- 青环 + 黄心：玩家。
- 绿色框：友军编队。
- 红色框：敌军编队。
- 绿色点：友军 Agent。
- 红色点：敌军 Agent。
- 中性色点：中立 Agent。
- 橙色菱形：镜头目标。

## 12. 小地图

默认位于右下区域，显示核心地图、玩家、编队/Agent、镜头目标、简要图例和当前模式提示。

不提供：

- 地图缩放。
- 地图拖动。
- 自动居中。

## 13. 全屏地图

用于战术规划。建议布局：

- 顶部：标题与状态。
- 左侧：编队列表。
- 中央：战术地图。
- 右侧：选中编队详细信息。
- 底部：当前模式与操作提示。

## 14. 明确取消的功能

以下功能不得重新加入：

- 地图缩放。
- 地图拖动。
- 自动居中。
- 攻击移动。
- 独立攻击命令。
- 多余的镜头模式开关。
- 多余的功能设置面板。
- 为节省空间而隐藏必要信息。

## 15. 技术职责

### TacticalMap Core

负责 `TerrainCache`、`FormationTracker`、`AgentTracker`、`OrderSystem`、`CameraController` 以及世界坐标/UV、地图数据、命令执行和镜头控制。

### TacticalMap HtmlUI Consumer

负责页面注册、生命周期、状态机、状态发布、输入模式以及 HTML Command。

### HTML

负责地图绘制、单位绘制、编队列表、详情、图例、全屏布局和鼠标事件，不直接调用 Bannerlord API。

## 16. 最终状态机

```text
                         ┌─────────────────────┐
                         │ CompactPassive      │
                         │ 小地图 · 观察        │
                         └─────────┬───────────┘
                                   │ N 短按
                                   ▼
                         ┌─────────────────────┐
                         │ CompactInteractive  │
                         │ 小地图 · 操作        │
                         └─────────────────────┘
```

长按循环：

```text
CompactPassive      -> FullPassive      -> Hidden -> CompactPassive
CompactInteractive  -> FullInteractive  -> Hidden -> CompactPassive
```

ESC：

```text
CompactInteractive -> CompactPassive
FullInteractive    -> FullPassive
```

## 17. 第一阶段验收顺序

1. HTML 页面正常显示。
2. 默认小地图正常显示。
3. 地形正常显示。
4. 玩家正常显示。
5. 编队正常显示。
6. 近距离 Agent / 远距离 Formation 正常切换。
7. N 短按进入/退出地图操作。
8. 操作状态不触发游戏失焦静音。
9. 左键移动。
10. 中键镜头。
11. 右键朝向。
12. ESC 退出操作。
13. N 长按进入全屏。
14. N 长按隐藏。
15. 隐藏后 N 长按恢复小地图。
