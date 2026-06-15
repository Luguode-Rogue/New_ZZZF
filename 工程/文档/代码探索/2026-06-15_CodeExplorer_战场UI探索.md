# CodeExplorer 工作记录 - 战场UI探索

## 调用原因
用户需要制作战场魔法和耐力条UI，需要了解原生血量条UI的实现方式作为参考。

## 探索目标
1. 找到Native模块中战场HUD/血量条相关XML
2. 了解AgentHealthWidget的完整结构
3. 查找项目中已有的自定义UI实现参考

## 发现的文件和目录结构

### Native 战场HUD体系（三层结构）

| 层级 | 文件 | 职责 |
|------|------|------|
| 主入口 | Native\GUI\Prefabs\Mission\MainAgentHUD.xml | HUD根容器，引用AgentStatus和AgentFocus子Prefab |
| 核心血量条 | Native\GUI\Prefabs\Mission\AgentStatus.xml (28.94KB) | 英雄血量、坐骑血量、护盾耐久、弹药UI |
| 交互提示 | Native\GUI\Prefabs\Mission\AgentFocus.xml | 交互提示 |
| 受伤指示 | Native\GUI\Prefabs\Mission\AgentTakenDamage.xml | 受伤方向指示 |
| 画刷样式 | Native\GUI\Brushes\Mission.xml | Mission相关Brush定义 |
| 通用画刷 | Native\GUI\Brushes\Brush.xml | HealthBarBrush定义 |

### 关键文件路径
- `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\GUI\Prefabs\Mission\MainAgentHUD.xml`
- `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\GUI\Prefabs\Mission\AgentStatus.xml`
- `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\GUI\Brushes\Mission.xml`
- `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\GUI\Brushes\Brush.xml`

## 关键代码片段位置

### AgentStatus.xml - 英雄血量条结构
`Native\GUI\Prefabs\Mission\AgentStatus.xml`

核心结构：
```xml
<AgentHealthWidget Id="HeroHealthWidget"
    Health="@AgentHealth" MaxHealth="@AgentHealthMax"
    HealthBar="Canvas\FillBar"
    ShowHealthBar="@ShowAgentHealthBar"
    HealthDropContainer="Canvas\HealthDropContainer"
    HealthDropBrush="Mission.MainAgentHUD.HeroHealthBar.FillChange">
  <Children>
    <BrushWidget Id="Canvas" Brush="Mission.MainAgentHUD.HeroHealthBar.Canvas">
      <Children>
        <Widget Id="HealthDropContainer" ... />
        <FillBarWidget Id="FillBar" ClipContents="true" FillWidget="FillVisual">
          <Children>
            <BrushWidget Id="FillVisual" HorizontalAlignment="Left"
                Brush="Mission.MainAgentHUD.HeroHealthBar.Fill" />
          </Children>
        </FillBarWidget>
      </Children>
    </BrushWidget>
  </Children>
</AgentHealthWidget>
```

### 三个血条位置关系（MarginBottom）：
- 英雄血量条：MarginBottom="90"
- 坐骑血量条：MarginBottom="80"
- 护盾耐久条：在英雄血量条上方

## 功能模块间的依赖关系

1. MainAgentHUD.xml 是容器，通过 `<AgentStatus />` 引用 AgentStatus.xml
2. AgentStatus.xml 使用自定义Widget `AgentHealthWidget`（C#类实现）
3. `AgentHealthWidget` 接受数据绑定参数（Health、MaxHealth、ShowHealthBar等）
4. 画刷引用格式：`Mission.MainAgentHUD.HeroHealthBar.Fill` 等，定义在 `Brushes\Mission.xml`
5. `FillBarWidget` 是通用的进度条Widget，`HealthBarBrush` 定义在 `Brushes\Brush.xml`

## 可复用的分析结论

1. **推荐策略：直接复用 AgentHealthWidget**
   - 魔法和耐力条的结构与血量条完全一样（进度条 + 背景Canvas + 受伤动画）
   - 只需要提供不同的数据绑定（Mana、Stamina）和不同的Brush样式
   - 不需要自定义新的Widget类

2. **XML结构模板**：
   - 在 MainAgentHUD.xml 中添加新的子Widget（放在AgentStatus上面）
   - 或直接在 AgentStatus.xml 中添加（像坐骑血量条那样）
   - 使用 AgentHealthWidget，绑定魔法和耐力数据

3. **位置摆放**：在英雄血量条上方（缩小MarginBottom值，如95-100区间）

4. **画刷资源**：需要定义新Brush（魔法条蓝色、耐力条黄色），引用sprite图片

## 未解决的问题

1. 魔法和耐力的数据源（@ManaHealth、@StaminaHealth 等）是否已有C#端数据绑定支持
2. 是否需要自定义sprite图片做魔法条/耐力条的外观
3. 是直接修改AgentStatus.xml还是在独立的Prefab中做
