# CustomSkillScreen 滚动区域 Z层级裁剪修复

**日期**: 2026-06-09
**任务**: 修复技能配置界面中间面板的滚动区域渲染层级问题
**文件**: `Modules\New_ZZZF\GUI\Prefabs\CustomSkillScreen.xml`
**关联**: ViewModel `CustomSkillScreenVM.cs`, CodeBehind `CustomSkillScreen.cs`

---

## 一、问题描述

CustomSkillScreen 界面中，中间主面板包含三个子元素：
1. **顶部表头** —「已配置战志技能」文字
2. **技能滚动区域** — 可滚动的8个技能槽位卡片
3. **底部操作栏** —「快捷键」「完成契刻」「撤销」按钮

**症状**：
- 技能滚动区域超出其设定的 Margin 范围，滚动内容会渲染在表头文字之上
- 滚动内容也会渲染在底部操作栏按钮之上
- 视觉效果：表头文字和底部按钮被技能卡片"遮挡"，层级混乱

---

## 二、根因分析

### GauntletUI 的渲染规则

在 Bannerlord 的 GauntletUI 框架中，**同一个父容器下的子元素按 XML 声明顺序渲染**（后声明的覆盖先声明的）。如果子元素没有设置 `ClipContents="true"`，则其渲染内容可以超出自身边界，绘制到兄弟元素之上。

### 原始层级问题

```
BrushWidget (中间面板, 无ClipContents)
├── BrushWidget (表头, h=40)          ← 第0个孩子，最底层
├── ScrollablePanel (滚动区域)         ← 第1个孩子，中层
│   └── SkillInnerPanel (内容可为任意高度)
└── BrushWidget (底栏, h=52, Bottom)   ← 第2个孩子，最顶层
```

**问题链条**：
1. `ScrollablePanel` 设置了 `MarginTop="46"` 和 `MarginBottom="60"`，但这只是**布局偏移**，不代表渲染裁剪
2. `SkillInnerPanel` 使用 `HeightSizePolicy="CoverChildren"`，内容高度可能远大于可视区域
3. 虽然 ScrollablePanel 内部有 `ClipRect="SkillViewport"` 和 `SkillViewport` 的 `ClipContents="true"`，但 ScrollablePanel 作为滚动控件，其自身的渲染管线在某些情况下不会严格限制子元素绘制边界
4. 中间面板的 BrushWidget 没有 `ClipContents`，无法阻止子元素越界绘制

结果：滚动内容绘制超出了 Margin 定义的范围，覆盖在表头和底栏之上。

---

## 三、解决方案

### 核心思路：双重 ClipContents 裁剪

| 层级 | 位置 | 设置 | 作用 |
|------|------|------|------|
| **外层** | 中间 BrushWidget | `ClipContents="true"` | 禁止任何子元素渲染超出整个中间面板的物理边界 |
| **内层** | ScrollablePanel 外层包裹 Widget | `ClipContents="true"` + Margin | 精确限制滚动内容的可视范围为"表头下方 ~ 底栏上方"的矩形区域 |

### 结构变更

**修改前**：
```xml
<BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="Frame1Brush">
  <Children>
    <BrushWidget ...>表头</BrushWidget>

    <ScrollablePanel ... MarginTop="46" MarginBottom="60">
      ...技能列表...
    </ScrollablePanel>

    <BrushWidget ... VerticalAlignment="Bottom">底栏</BrushWidget>
  </Children>
</BrushWidget>
```

**修改后**：
```xml
<BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
             Brush="Frame1Brush" ClipContents="true">        ← 外层裁剪
  <Children>
    <BrushWidget ... VerticalAlignment="Top">表头</BrushWidget>   ← 固定顶部

    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
            MarginTop="46" MarginBottom="60"                  ← Margin 移到这里
            MarginLeft="12" MarginRight="12"
            ClipContents="true">                               ← 内层裁剪
      <Children>
        <ScrollablePanel ... >                                 ← 占满内层容器
          ...技能列表...
        </ScrollablePanel>
      </Children>
    </Widget>

    <BrushWidget ... VerticalAlignment="Bottom">底栏</BrushWidget>
  </Children>
</BrushWidget>
```

---

## 四、三处具体修改

### 修改1：中间 BrushWidget 添加 ClipContents（行101）

```diff
- <BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="Frame1Brush">
+ <BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="Frame1Brush" ClipContents="true">
```

**作用**: 外层保险——确保整个中间面板所属的任何子元素都不可能渲染到面板之外。

### 修改2：表头添加 VerticalAlignment="Top"（行105）

```diff
- <BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="40" Brush="Frame2Brush" DoNotAcceptEvents="true">
+ <BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="40" VerticalAlignment="Top" Brush="Frame2Brush" DoNotAcceptEvents="true">
```

**作用**: 明确表头固定在面板顶部，不参与剩余空间的弹性分配，保证表头始终在最上方。

### 修改3：ScrollablePanel 外层包裹 Clip 容器（行113-197）

原 ScrollablePanel 自身带的 `MarginTop="46" MarginBottom="60" MarginLeft="12" MarginRight="12"` 移到新外层 Widget 上，ScrollablePanel 改为占满外层容器。

```diff
- <ScrollablePanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
-                  MarginTop="46" MarginBottom="60" MarginLeft="12" MarginRight="12"
-                  AutoHideScrollBars="true"
-                  ClipRect="SkillViewport" InnerPanel="SkillInnerPanel" VerticalScrollbar="SkillScrollbar">
-   <Children>
-     <Widget Id="SkillViewport" ... ClipContents="true" />
-     <ListPanel Id="SkillInnerPanel" ...>
-       ...
-     </ListPanel>
-     <ScrollbarWidget Id="SkillScrollbar" ...>
-       ...
-     </ScrollbarWidget>
-   </Children>
- </ScrollablePanel>

+ <!-- 技能滚动区域（外层Clip容器，防止滚动内容覆盖表头和底栏） -->
+ <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
+         MarginTop="46" MarginBottom="60" MarginLeft="12" MarginRight="12" ClipContents="true">
+   <Children>
+     <ScrollablePanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
+                      AutoHideScrollBars="true"
+                      ClipRect="SkillViewport" InnerPanel="SkillInnerPanel" VerticalScrollbar="SkillScrollbar">
+       <Children>
+         ...内容不变...
+       </Children>
+     </ScrollablePanel>
+   </Children>
+ </Widget>
```

**作用**: 核心修复——新外层 Widget 的 `ClipContents="true"` 将滚动内容严格裁剪在其 Margin 定义的矩形区域内。滚动时卡片渲染内容被限制在表头下方+底栏上方的区间，无法覆盖这两个固定区域。

---

## 五、最终渲染层级

```
BrushWidget (ClipContents="true")           ← 外层裁剪（面板边界）
├── 表头 (VerticalAlignment="Top")          ← Z=0, 固定顶部40px
├── Widget (ClipContents="true", Margin*)   ← Z=1, 内层裁剪（精确可视区）
│   └── ScrollablePanel                     ← 占满裁剪容器
│       ├── SkillViewport (ClipContents)
│       ├── SkillInnerPanel（数据驱动列表）
│       └── SkillScrollbar
└── 底栏 (VerticalAlignment="Bottom")       ← Z=2, 固定底部52px
```

**关键机制**：
- 表头和底栏因 `VerticalAlignment="Top"/"Bottom"` 被固定在两端，不会受中间区域尺寸影响
- 内层 Widget 的 `ClipContents="true"` + Margin 形成了一个**矩形裁剪窗口**，滚动内容只在该窗口内可见
- 外层 BrushWidget 的 `ClipContents="true"` 作为**兜底保护**，即便内层裁剪失效，内容也不会溢出面板边界

---

## 六、经验总结

### GauntletUI ClipContents 的使用原则

1. **Margin 不等于裁剪边界**：Margin 只改变布局位置和尺寸，不影响渲染裁剪。需要滚动区域有严格的可视边界时，必须用 `ClipContents="true"`。

2. **ClipRect 可能不够可靠**：ScrollablePanel 的 `ClipRect` 机制在某些情况下（特别是嵌套复杂时）不能完全限制子元素绘制。在 ScrollablePanel 外层显式添加 Clip 容器是更可靠的做法。

3. **双重裁剪是安全实践**：外层父容器 + 内层 Clip 容器形成双重保护，任何一层生效即可保证视觉效果正确。

4. **固定元素应设 VerticalAlignment**：表头和底栏设置 `VerticalAlignment="Top"/"Bottom"` 可确保它们不参与弹性空间分配，始终固定在预期位置。

### 调试技巧

- 遇到 Z层级/渲染覆盖问题，先检查是否有 `ClipContents` 设置
- 在 GauntletUI 中，"后面声明的子元素覆盖前面" 是正常的，但 `ClipContents` 可以覆盖此行为
- 如果滚动区域的 ClipRect 不生效，尝试在外层加一个 Widget 带 `ClipContents`
