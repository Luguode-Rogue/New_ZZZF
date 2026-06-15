# GauntletUI ScrollablePanel 滚动条制作完整教程

> 基于 Bannerlord GauntletUI 框架，涵盖从概念到实战的滚动条完整制作指南。
>
> 案例来源：New_ZZZF 项目的 CustomSkillScreen.xml 和 SkillSelectionPopup.xml（均已完成验证）

---

## 一、核心概念：三位一体模式

GauntletUI 的 `ScrollablePanel` 依赖**三个平级直接子节点**协同工作，缺一不可：

```
ScrollablePanel（容器）
  ├── ClipRect="ViewportId"          ← 属性绑定视口
  ├── InnerPanel="ContentPanelId"     ← 属性绑定内容面板
  ├── VerticalScrollbar="ScrollbarId" ← 属性绑定滚动条
  └── <Children>
        ├── Widget Id="ViewportId"           ← 三位一体 · 视口裁剪区
        ├── ListPanel Id="ContentPanelId"    ← 三位一体 · 内容载体
        └── ScrollbarWidget Id="ScrollbarId" ← 三位一体 · 滚动条控件
      </Children>
```

**核心规则：视口、内容面板、滚动条三者必须是 ScrollablePanel 的直接子节点，通过简单 Id 引用关联。**

---

## 二、三个组件详解

### 2.1 视口裁剪区（Viewport / ClipRect）

| 属性 | 值 | 说明 |
|------|-----|------|
| **必须是一个 Widget** | `<Widget>` | 不能是 ListPanel 或其他类型 |
| `ClipContents` | `"true"` | **必须设置**，否则内容溢出视口边界 |
| `WidthSizePolicy` | `StretchToParent` | 通常撑满父容器 |
| `HeightSizePolicy` | `StretchToParent` | 通常撑满父容器 |
| `Id` | 自定义名称 | 用于 ScrollablePanel 的 `ClipRect` 属性引用 |

**作用**：定义可见区域边界，超出部分被裁剪。

```xml
<Widget Id="MyViewport" 
        WidthSizePolicy="StretchToParent" 
        HeightSizePolicy="StretchToParent" 
        ClipContents="true" />
```

### 2.2 内容面板（InnerPanel）

| 属性 | 值 | 说明 |
|------|-----|------|
| **通常是一个 ListPanel** | `<ListPanel>` | 承载列表数据 |
| `HeightSizePolicy` | **`CoverChildren`** | **关键！** 不能用 `StretchToParent`，否则永远不需要滚动 |
| `WidthSizePolicy` | `StretchToParent` | 撑满视口宽度 |
| `Id` | 自定义名称 | 用于 `InnerPanel` 属性引用 |
| `DataSource` | `{BindingList}` | 绑定 ViewModel 的数据列表 |
| `ItemTemplate` | 子元素 | 定义每一项的布局 |

**作用**：实际承载所有列表项的容器，高度随内容动态增长，当超过视口高度时触发滚动。

```xml
<ListPanel Id="MyContentPanel" 
           WidthSizePolicy="StretchToParent" 
           HeightSizePolicy="CoverChildren"
           DataSource="{MyDataList}"
           StackLayout.LayoutMethod="VerticalTopToBottom"
           StackLayout.Spacing="6">
  <ItemTemplate>
    <!-- 每一项的模板 -->
  </ItemTemplate>
</ListPanel>
```

### 2.3 滚动条控件（ScrollbarWidget）

| 属性 | 值 | 说明 |
|------|-----|------|
| `Id` | 自定义名称 | 用于 `VerticalScrollbar` 属性引用 |
| `WidthSizePolicy` | `Fixed` | 固定宽度 |
| `SuggestedWidth` | `6`~`8` | 滚动条宽度 |
| `HeightSizePolicy` | `StretchToParent` | 撑满父容器高度 |
| `HorizontalAlignment` | `Right` | 停靠在右侧 |
| `Alignment` | `Right` | 对齐方式 |
| `Handle` | 滑块 Widget 的 Id | **必须设置**，指向滑块子元素 |

**子元素结构**（最少2个）：

```
ScrollbarWidget
  └── <Children>
        ├── Widget（轨道背景）          ← 半透明底板
        └── Widget Id="HandleId"（滑块） ← 通过 Handle 属性绑定
      </Children>
```

**完整示例**：

```xml
<ScrollbarWidget Id="MyScrollbar" 
                 WidthSizePolicy="Fixed" SuggestedWidth="6" 
                 HeightSizePolicy="StretchToParent" 
                 HorizontalAlignment="Right" 
                 Alignment="Right"
                 Handle="MyScrollbarHandle"
                 MarginRight="-12">
  <Children>
    <!-- 轨道背景 -->
    <Widget WidthSizePolicy="StretchToParent" 
            HeightSizePolicy="StretchToParent" 
            Sprite="BlankWhiteSquare_9" 
            Color="#0E162266" />
    <!-- 滑块 -->
    <Widget Id="MyScrollbarHandle" 
            WidthSizePolicy="StretchToParent" 
            HeightSizePolicy="Fixed" 
            SuggestedHeight="40" 
            Sprite="BlankWhiteSquare_9" 
            Color="#4A90D9FF" />
  </Children>
</ScrollbarWidget>
```

---

## 三、ScrollablePanel 完整属性参考

| 属性 | 类型 | 说明 | 推荐值 |
|------|------|------|--------|
| `ClipRect` | string (Id引用) | 指定视口 Widget 的 Id | 必填 |
| `InnerPanel` | string (Id引用) | 指定内容面板的 Id | 必填 |
| `VerticalScrollbar` | string (Id引用) | 指定滚动条控件的 Id | 需要垂直滚动时必填 |
| `HorizontalScrollbar` | string (Id引用) | 水平滚动条（不常用） | 按需 |
| `AutoHideScrollBars` | bool | 内容不足时自动隐藏滚动条 | `"true"`（开发期可用 `"false"` 强制显示） |
| `InnerPanel.HeightSizePolicy` | SizePolicy | 内容面板高度策略（属性覆写） | `"CoverChildren"` |
| `InnerPanel.WidthSizePolicy` | SizePolicy | 内容面板宽度策略 | `"StretchToParent"` |
| `ScrollbarPadding` | int | 滚动条与内容的间距 | 通过 `MarginRight` 控制更直观 |

---

## 四、完整实战案例

### 4.1 案例A：英雄列表滚动（CustomSkillScreen.xml 已验证通过）

```xml
<!-- 左侧面板内的可滚动英雄列表 -->
<ScrollablePanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
                 MarginTop="50" MarginRight="12"
                 AutoHideScrollBars="true"
                 ClipRect="RosterViewport"
                 InnerPanel="RosterInnerPanel"
                 VerticalScrollbar="RosterScrollbar">
  <Children>

    <!-- 组件1：视口裁剪 -->
    <Widget Id="RosterViewport" 
            WidthSizePolicy="StretchToParent" 
            HeightSizePolicy="StretchToParent" 
            ClipContents="true" />

    <!-- 组件2：内容面板 -->
    <ListPanel Id="RosterInnerPanel" 
               WidthSizePolicy="StretchToParent" 
               HeightSizePolicy="CoverChildren"
               DataSource="{Roster}"
               StackLayout.LayoutMethod="VerticalTopToBottom"
               StackLayout.Spacing="6">
      <ItemTemplate>
        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed"
                SuggestedHeight="48">
          <Children>
            <!-- 项背景 -->
            <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
                    Sprite="BlankWhiteSquare_9" Color="#253040CC"
                    DoNotAcceptEvents="true" />
            <!-- 选中高亮条 -->
            <Widget WidthSizePolicy="Fixed" SuggestedWidth="4"
                    HeightSizePolicy="StretchToParent"
                    Sprite="BlankWhiteSquare_9" Color="#4A90D9FF"
                    HorizontalAlignment="Left"
                    IsVisible="@IsSelected"
                    DoNotAcceptEvents="true" />
            <!-- 点击按钮 -->
            <ButtonWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
                          Command.Click="ExecuteSelect"
                          UpdateChildrenStates="true"
                          ButtonType="Radio"
                          IsSelected="@IsSelected">
              <Children>
                <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
                            Text="@HeroName" Brush="Inventory.Text.Center"
                            FontSize="15"
                            MarginLeft="14" MarginRight="12"
                            VerticalAlignment="Center"
                            DoNotAcceptEvents="true" />
              </Children>
            </ButtonWidget>
          </Children>
        </Widget>
      </ItemTemplate>
    </ListPanel>

    <!-- 组件3：滚动条 -->
    <ScrollbarWidget Id="RosterScrollbar" 
                     WidthSizePolicy="Fixed" SuggestedWidth="6" 
                     HeightSizePolicy="StretchToParent" 
                     HorizontalAlignment="Right" 
                     Alignment="Right"
                     Handle="RosterScrollbarHandle"
                     MarginRight="-12">
      <Children>
        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" 
                Sprite="BlankWhiteSquare_9" Color="#0E162266" />
        <Widget Id="RosterScrollbarHandle" 
                WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" 
                SuggestedHeight="40" 
                Sprite="BlankWhiteSquare_9" Color="#4A90D9FF" />
      </Children>
    </ScrollbarWidget>

  </Children>
</ScrollablePanel>
```

### 4.2 案例B：弹窗技能列表滚动（SkillSelectionPopup.xml 简化版）

弹窗中的技能列表使用了**不需要显式 Viewport/Scrollbar 的简化写法**（自身直接设置 ClipContents）：

```xml
<ScrollablePanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
                 InnerPanel.HeightSizePolicy="CoverChildren"
                 MarginLeft="18" MarginRight="18" MarginTop="10" MarginBottom="10"
                 AutoHideScrollBars="true"
                 ClipContents="true">
  <Children>
    <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
               DataSource="{FilteredSkills}"
               StackLayout.LayoutMethod="VerticalTopToBottom"
               StackLayout.Spacing="6">
      <ItemTemplate>
        <!-- 技能项模板 -->
      </ItemTemplate>
    </ListPanel>
  </Children>
</ScrollablePanel>
```

**说明**：这个简化版适用于不需要自定义滚动条样式、也不需要对内容面板做额外控制的场景。此时 ScrollablePanel 的 `ClipContents="true"` 和 `InnerPanel.HeightSizePolicy="CoverChildren"` 负责自动处理滚动行为。

### 4.3 方案对比

| 特性 | 完整版（三位一体） | 简化版（无显式滚动条） |
|------|-------------------|----------------------|
| 适用场景 | 需自定义滚动条样式 | 接受默认滚动条 |
| ClipRect | 显式 Widget | 自动管理 |
| InnerPanel | 显式 ListPanel/Id | 隐式（首个 ListPanel 子元素） |
| ScrollbarWidget | 必须创建 | 不用创建 |
| 自定义滑块颜色/样式 | 支持 | 不支持 |
| 代码量 | 较多 | 较少 |

---

## 五、常见错误与崩溃原因

### ❌ 错误1：路径引用 → NullReferenceException

```xml
<!-- 错误：使用了 XML 路径引用 -->
<ScrollablePanel ClipRect="ClipRect"
                 InnerPanel="ClipRect\InnerPanel"
                 VerticalScrollbar="..\VerticalScrollbar">
```

**根因**：GauntletUI 在直接使用的 XML 中**不支持路径解析**（如 `\` 和 `..`），这些引用无法找到目标 Widget，导致内部 `_innerPanel` 等字段为 null，触发 `NullReferenceException` 崩溃。

**修复**：使用**简单 Id 名称**，且三者必须是 ScrollablePanel 的直接子节点。

### ❌ 错误2：ScrollbarWidget 放在 ScrollablePanel 外部（兄弟节点）

```xml
<!-- 错误 -->
<Widget>
  <Children>
    <ScrollablePanel ... VerticalScrollbar="MyScrollbar">
      <Children>
        <Widget Id="Viewport" ... />
        <ListPanel Id="Inner" ... />
      </Children>
    </ScrollablePanel>
    <!-- 错误：在 ScrollablePanel 外部，是兄弟节点 -->
    <ScrollbarWidget Id="MyScrollbar" ... />
  </Children>
</Widget>
```

**根因**：`ScrollablePanel` 内部通过遍历自己的 `Children` 集合来查找 `ClipRect`/`InnerPanel`/`VerticalScrollbar` 对应的 Widget。放在外部则无法找到。

**修复**：`ScrollbarWidget` 必须放在 `ScrollablePanel` 的 `<Children>` 内部。

### ❌ 错误3：ClipRect 和 InnerPanel 嵌套关系

```xml
<!-- 错误：InnerPanel 嵌套在 ClipRect 内部 -->
<Widget Id="MyViewport" ClipContents="true">
  <Children>
    <ListPanel Id="MyInnerPanel" ... />
  </Children>
</Widget>
```

**修复**：两者必须是**平级直接子节点**，各是各的 `<Children>` 独立项。`ClipContents="true"` 的 Widget 只负责裁剪，内容面板是独立 Widget，由 `ScrollablePanel` 内部逻辑协调滚动关系。

### ❌ 错误4：InnerPanel 使用 StretchToParent

```xml
<!-- 错误：内容面板高度撑满父容器，永远不会溢出触发滚动 -->
<ListPanel Id="MyInnerPanel" 
           HeightSizePolicy="StretchToParent" ... />
```

**修复**：必须使用 `HeightSizePolicy="CoverChildren"`，让内容面板高度随子元素动态增长，超过视口高度时才会触发滚动。

### ❌ 错误5：ScrollbarWidget 忘记设置 Handle 属性

```xml
<!-- 错误：缺少 Handle 绑定，滑块无法跟随滚动位置 -->
<ScrollbarWidget Id="MyScrollbar" ...>
  <Children>
    <Widget Id="MyHandle" ... />
  </Children>
</ScrollbarWidget>
```

**修复**：必须添加 `Handle="MyHandle"` 属性。

---

## 六、调试检查清单

当滚动条不工作时，按顺序排查：

| # | 检查项 | 操作 |
|---|--------|------|
| 1 | InnerPanel 的 `HeightSizePolicy` 是 `CoverChildren` 吗？ | 不是则改 |
| 2 | Viewport 的 `ClipContents="true"` 设置了吗？ | 没设则加 |
| 3 | ScrollablePanel 的三个属性（`ClipRect`/`InnerPanel`/`VerticalScrollbar`）是否指向正确的 Id？ | 确认 Id 名称一致 |
| 4 | 三个组件是否都是 ScrollablePanel 的直接子节点（不是孙子节点、不是兄弟节点）？ | 检查层级 |
| 5 | 开发期是否设置了 `AutoHideScrollBars="false"` 强制显示滚动条？ | 方便确认滚动条是否渲染 |
| 6 | ScrollablePanel 本身的高度是否足够（不能是 `CoverChildren`）？ | 固定高度或 `StretchToParent` |
| 7 | ScrollbarWidget 的 `Handle` 属性是否指向了滑块 Widget 的 Id？ | 确认绑定 |

---

## 七、滚动条样式自定义

### 7.1 暗色主题滚动条（本项目风格）

```xml
<ScrollbarWidget Id="MyScrollbar" 
                 WidthSizePolicy="Fixed" SuggestedWidth="6" 
                 HeightSizePolicy="StretchToParent" 
                 HorizontalAlignment="Right" Alignment="Right"
                 Handle="MyScrollbarHandle">
  <Children>
    <!-- 轨道：深色半透明 -->
    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" 
            Sprite="BlankWhiteSquare_9" Color="#0E162266" />
    <!-- 滑块：主题亮蓝色 -->
    <Widget Id="MyScrollbarHandle" 
            WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" 
            SuggestedHeight="40" 
            Sprite="BlankWhiteSquare_9" Color="#4A90D9FF" />
  </Children>
</ScrollbarWidget>
```

### 7.2 使用 Brush 的滚动条（官方风格）

```xml
<ScrollbarWidget Id="VerticalScrollbar"
    WidthSizePolicy="Fixed" HeightSizePolicy="StretchToParent"
    SuggestedWidth="8" HorizontalAlignment="Right"
    AlignmentAxis="Vertical" Handle="VerticalScrollbarHandle"
    MaxValue="100" MinValue="0">
  <Children>
    <Widget WidthSizePolicy="Fixed" HeightSizePolicy="StretchToParent"
            SuggestedWidth="4" HorizontalAlignment="Center"
            Sprite="BlankWhiteSquare_9" Color="#5a4033FF" AlphaFactor="0.2" />
    <ImageWidget Id="VerticalScrollbarHandle"
        WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
        SuggestedHeight="10" SuggestedWidth="8"
        Brush="FaceGen.Scrollbar.Handle" />
  </Children>
</ScrollbarWidget>
```

**区别**：滑块使用 `ImageWidget` + `Brush` 引用画刷资源，视觉效果更精致。前提是 Brushes XML 中已定义 `FaceGen.Scrollbar.Handle`。

### 7.3 滚动区域 spacing 技巧

通过 `MarginRight` 给滚动条和内容之间留呼吸空间：

```xml
<ScrollablePanel MarginRight="14">  <!-- 14px 右侧间距给滚动条 -->
  <!-- 内容面板同时给右侧留空间，避免文字贴边 -->
  <ListPanel ... MarginRight="4" />
  <!-- 滚动条通过 MarginRight="-14" 补偿定位 -->
  <ScrollbarWidget ... MarginRight="-14" />
</ScrollablePanel>
```

---

## 八、与 Standard.ScrollablePanel 预制体的关系

Native 提供了 `Standard.ScrollablePanel` 预制体，参数化封装了常见滚动面板：

```xml
<Standard.ScrollablePanel
    Parameter.OverlayShadowBrush="InnerFrameShadow1Brush"
    Parameter.ScrollbarVisible="true"
    Parameter.InnerPanelVerticalAlignment="Top">
  <Children>
    <!-- 内容通过 LogicalChildrenLocation 注入 -->
  </Children>
</Standard.ScrollablePanel>
```

**何时用 Standard 组件 vs 自己写**：
- 需要完全自定义滚动条样式 → 自己写（参考本文案例）
- 只需要标准滚动功能 → 用 `Standard.ScrollablePanel` 更省事

注意：`Standard.ScrollablePanel` 内部也是基于同样的三位一体模式实现的，只是封装了 Id 命名和样式。

---

## 九、关键属性速查

| 控件 | 关键属性 | 必须值 |
|------|----------|--------|
| **ScrollablePanel** | `ClipRect`, `InnerPanel`, `VerticalScrollbar` | 指向子节点 Id |
| **ScrollablePanel** | `AutoHideScrollBars` | `"true"` (生产) / `"false"` (调试) |
| **Viewport Widget** | `ClipContents` | `"true"` |
| **InnerPanel ListPanel** | `HeightSizePolicy` | **`"CoverChildren"`** |
| **ScrollbarWidget** | `Handle` | 指向滑块子控件 Id |
| **ScrollbarWidget** | `Alignment`, `HorizontalAlignment` | `"Right"` |
| **滑块 Widget** | `HeightSizePolicy` | `"Fixed"` + `SuggestedHeight` |

---

## 十、总结公式

要创建一个可滚动的列表，按以下公式操作：

```
1. 创建 ScrollablePanel（给足高度，别 CoverChildren）
2. 在 Children 中放三个平级子节点：
   a. Widget (Id="V", ClipContents="true")       ← 视口
   b. ListPanel (Id="I", HeightSizePolicy="CoverChildren")  ← 内容
   c. ScrollbarWidget (Id="S", Handle="H")        ← 滚动条
       └── Widget (Id="H", 滑块)
3. ScrollablePanel 上设置：
   ClipRect="V"  InnerPanel="I"  VerticalScrollbar="S"
4. 调试期设 AutoHideScrollBars="false" 确认渲染正常
```

记住一句话：**视口裁剪、内容面板、滚动条，三者平级、Id关联、都在 Children 里面。**

---

*编写时间：2026-06-07*
