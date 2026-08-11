# ActionExplorer M0 技术验证（探针）

> 对应规划：ActionExplorer重制_Draft2.2 §十三 M0
> 性质：纯技术探针（不进正式 ActionExplorer，独立 Demo）
> 目标：证明"每 UI Item 内嵌独立 SceneWidget Preview"架构能否成立，回答 6 个问题 + A/B 互不干扰
> 代码位置：New_ZZZF/工程/New_ZZZF/ActionExplorer/M0_Probe/

---

## 一、已证实的源码事实（进入 M0 前已从骑砍2源码确认）

| 事实 | 证据 | 结论 |
| --- | --- | --- |
| `SceneWidget` 自带 `SceneTextureProvider` | 构造函数 `TextureProviderName="SceneTextureProvider"` | 用 SceneWidget 即获 provider |
| 设 `Scene` 属性 → 独立 `SceneTableau` | `SetTextureProviderProperty("Scene",value)` → `new SceneTableau()` | **每 Widget 独立 Scene/Tableau/纹理（机制潜力已证实）** |
| `SceneTableau` 相机默认读场景 `customcamera` entity | `GetCameraParamsFromCameraScript` 取 tag=`customcamera` | **独立 Camera 注入能力待 M0 实测（关键风险点）** |
| Widget 释放钩子 = `OnDisconnectedFromRoot` | Widget 生命周期（非 OnRelease/OnFinalize） | M0 用此作为 PreviewItem 唯一 Dispose 入口 |
| 旧实现加载 `character_menu_new` + `_warrior` ActionSet 可用 | ActionPreview.cs 经验 | 可直接复用，但作为非硬依赖 fallback |

### 关键待验证风险点（M0 核心）
engine `SceneTableau.SetCamera` **非公共**，相机参数只读场景内 `customcamera` entity。
→ 若要从外部注入真正独立的 `PreviewCamera`，可能需要：
(a) 确保预览场景含 `customcamera` entity（由场景决定，不自由）；
(b) 自定义 `SceneTableau` 子类绕过 `SceneWidget` 默认 provider；
(c) 放弃外部 Camera，接受场景默认相机。
**这是路线 A 是否成立的最大未知数，M0 必须实测。**

---

## 二、M0 探针代码结构

```text
M0_Probe/
├── M0PreviewWidget.cs   # 自定义 SceneWidget：桥梁/唯一 Owner，OnConnectedToRoot 建、OnDisconnectedFromRoot  Dispose（幂等）
├── M0PreviewItem.cs     # Preview 逻辑对象：Scene/Agent/Camera/Controller 最小闭环，唯一 Owner=Widget
├── M0ProbeVM.cs         # 测试 VM（不引用 3D）：ActionA/ActionB/Log + 命令状态
├── M0ProbeScreen.cs     # 独立 Screen：GauntletLayer + 按钮绑定，不持有 PreviewItem
├── M0Probe.xml          # 双 M0PreviewWidget + 控制按钮
└── M0ProbeLauncher.cs   # 临时入口（热键打开，验证后移除）
```

### 所有权链（钉死）
```text
M0ProbeScreen（只管 GauntletLayer/开关）
   └── GauntletLayer
         └── M0PreviewWidget  ← PreviewItem 唯一 Owner
               └── M0PreviewItem（Scene/Agent/...）
```
Screen 不持有 PreviewItem；Dispose 入口唯一 = Widget.OnDisconnectedFromRoot（幂等）。

---

## 三、M0 必须回答的 7 个问题（含 A/B 互不干扰）

| # | 问题 | 探针操作 | 预期结果 |
| --- | --- | --- | --- |
| ① | 3D 能否真正放进 Gauntlet Widget？ | 打开即见角色在 Widget 矩形内 | Widget 移动→角色跟随，无世界坐标重算，无 SceneLayer.SetScreenArea |
| ② | 缩放后角色是否被裁剪？ | BtnResize 改变 Widget 尺寸 | 角色不跑出 Widget 外 |
| ③ | 两 Widget 是否真互不影响？ | BtnChangeA 改 A 动作 | **B 的角色/动作/相机/Scene/Tableau 全部不变**（核心验收，非普通测试） |
| ④ | 销毁后资源是否真释放？ | BtnRecreate 反复 Destroy+Recreate；BtnReport 看 NativeResourceCount | 不累计 Agent/Scene；重复 20 次后数量稳定 |
| ⑤ | GridPanel 重排后 Preview 是否跟随？ | BtnMove 改 Margin/Width | Preview 准确跟着 Item，无脱离 |
| ⑥ | 点击能否沿 Item→VM 拿 Action ID？ | 按钮日志显示 Action 名 | 经 Item→VM→ActionInfo，而非"点屏幕猜模型" |
| ⑦ | A/B Action 完全独立（拆项验收） | A=act_x, B=act_y，改 A | □ B 角色不变 □ B 动作不变 □ B 相机不变 □ B Scene/Tableau 不变 |

> 测试动作不硬编码依赖 act_kick：VM.ResolveTestAction 优先 act_kick，不存在则回退 act_idle（M0 只验证链路）。

---

## 四、验证清单（实测后填写 PASS/FAIL + 备注）

```text
□ ① Widget 内嵌 3D 成功，移动跟随
□ ② 缩放在 Widget 内裁剪正确
□ ③ A 改动作，B 完全不受影响
□ ④ Destroy+Recreate 无资源累计（NativeResourceCount 稳定）
□ ⑤ GridPanel 重排 Preview 跟随
□ ⑥ 点击经 Item→VM 拿 Action ID
□ ⑦ A/B 四项（角色/动作/相机/Scene）互不干扰
□ 附加：独立 Camera 能否注入（风险点）
□ 附加：character_menu_new 含 customcamera？无则 Camera 方案？
□ 附加：TV-06 目标版本 API 可用性（SceneWidget/SceneTextureProvider/SceneTableau/AgentVisuals/MBAgentRendererSceneController/SetAgentActionChannel）
```

---

## 五、通过后下一步

M0 PASS → 路线 A 成立 → 进入 M1（数据层，不碰 3D）→ M2（UI Placeholder）→ M3（正式组件接起）。
M0 FAIL（尤其独立 Camera 注入失败）→ 启用路线 B（Legacy fallback）或自定义 SceneTableau 方案，更新 Draft 2.3。
