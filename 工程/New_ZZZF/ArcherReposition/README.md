# 射手 AI 防发呆重定位（ArcherReposition）

## 功能

原版问题：射手被友军挡住弹道时（引擎可见性状态 `AITargetVisibilityState.FriendInWay`），C++ 拒绝开火，C# 无任何兜底，士兵原地发呆。

本特性：被挡的射手在 1~3 秒内自主做出反应 ——
1. **换目标**：从敌方随机采样候选，射线验证弹道畅通后手动指定（引擎官方模式，见原版 `TaskForceDetachment`）；
2. **侧移找射角**：垂直于射击方向平移 1.2~2.4m，覆写 formation frame（与引擎自身同线程同上下文），直到恢复通视。

## 对工程的接线（全部）

`SubModule.cs` → `OnMissionBehaviorInitialize` 中的一行：

```csharp
mission.AddMissionBehavior(new ArcherReposition.ArcherRepositionBehavior());
```

Harmony 补丁由 `ArcherRepositionBehavior` 构造函数自行安装（独立 Harmony ID：`New_ZZZF.ArcherReposition`，幂等）。

## 如何删除

1. 删除本文件夹（`工程/New_ZZZF/ArcherReposition/`）
2. 删除上面那一行

零残留：无其它代码引用、无资源文件依赖、无 SubModule.xml 改动。

## 如何独立出当前工程

- 代码只依赖 `HarmonyLib`（0Harmony.dll）与 TaleWorlds 官方程序集，**不引用本工程任何其它类**
- 命名空间 `New_ZZZF.ArcherReposition` 可整体改名为任意命名空间
- 把 5 个 .cs 文件复制到任意 Bannerlord mod 工程、注册一行 MissionLogic 即可运行

## 运行时开关与调参

全部参数在 `ArcherRepositionConfig.cs`（静态字段，改完重编译）：

| 参数 | 默认 | 说明 |
|---|---|---|
| `Enable` | true | 总开关，false 时与原版行为 100% 一致 |
| `OnlyPlayerTeam` | true | 只处理玩家方射手 |
| `DetectionStreak` | 2 | 连续 2 次读到"被挡"才动作（防 native 可见性缓存滞后误判） |
| `StrafeOffsets` | {1.2, 2.4} | 侧移幅度档位（米） |
| `MaxRayUnitsPerFrame` | 12 | 每帧加权射线预算（agent 射线=2 单位，Scene 射线=1 单位） |
| `ExitCooldown` | 0.75s | 退出冷却，防"挡→移→通→再判挡"振荡 |
| `MaxFaults` | 5 | 连续异常自动熔断，行为回退原版 |
| `DebugLog` | false | 写 `Logs/ArcherReposition.log`（换目标/侧移/异常） |

## 性能设计（500v500）

- 检测：读引擎现成的 `GetLastTargetVisibilityState()`（native 缓存值，0 射线），挂在引擎自身的 0.45~0.55s/agent 错峰 tick 上，无自建节流
- 射线：预算 12 加权单位/帧 ≈ 本体 `FocusTick`（每帧常态 7~8 次）的 1.5 倍；结果带 1m 网格缓存（挡=0.5s / 通=0.1s TTL）
- 内存：per-agent struct 数组（2048 槽 × ~80B）+ 512 槽射线缓存，运行期零 GC 分配
- 线程：检测在 TWParallel 工作线程只做"读状态 + 置标志 + 覆写 formation frame"（均有引擎并行先例）；换目标等引擎状态写入全部在主线程
- 熔断：postfix 全 try-catch，连续 5 次异常自动禁用并提示

## 已知边界

- 不处理骑马射手（坐骑移动语义不同，v1 排除）
- 换目标依赖引擎尊重 `SetTargetAgent`；若实测引擎立即改回（DebugLog 中保持时长 < 1 个周期），该 agent 会自然回退为纯侧移路径（侧移不依赖引擎 target）
- 引擎可见性为 native 缓存值，最坏响应延迟 ≈ 2s（2 连读 ~1s + 队列 ~0.3s + 引擎滞后 ~0.5s）
