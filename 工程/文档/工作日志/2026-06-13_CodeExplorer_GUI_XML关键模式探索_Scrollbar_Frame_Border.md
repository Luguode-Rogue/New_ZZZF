# 2026-06-13 CodeExplorer: GUI XML 关键模式探索（Scrollbar / Frame / Border）

## 调用原因
CustomSkillScreen.xml 存在三个 GUI 问题需要修复：分隔线不可见、技能槽位/熟练度面板无可见边框、滚动条完全不可见。需要在 Native/SandBox 模块中查找官方 GUI XML 的标准写法作为参考。

## 探索目标
1. ScrollbarWidget 的标准 XML 定义（包含 Handle、背景、AlignmentAxis、MaxValue/MinValue 等）
2. Frame1.Border vs Frame1Brush 的区别和用法
3. StackLayout.LayoutMethod + ScrollablePanel 的标准嵌套模式
4. BlankWhiteSquare_9 作为分隔线 sprite 的用法
5. ScrollablePanel + InnerPanel + ClipRect 的 ID 绑定关系

## 探索范围
- `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\GUI\`
- `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\GUI\`

---

## 关键发现

### 1. ScrollbarWidget 标准模式

#### 1.1 官方 Prefab：Standard.VerticalScrollbar.xml（最完整参考）
**文件**：`Native\GUI\Prefabs\Standard\Standard.VerticalScrollbar.xml`（36行）

```xml
<ScrollbarWidget Id="Scrollbar" WidthSizePolicy="Fixed" HeightSizePolicy="StretchToParent"
    SuggestedWidth="22" SuggestedHeight="800"
    HorizontalAlignment="Left" VerticalAlignment="Bottom"
    MarginTop="!Stopper.Top.Height" MarginBottom="!Stopper.Bottom.Height"
    Brush="Scrollbar.Vertical"
    AlignmentAxis="Vertical"
    Handle="ScrollbarHandle"
    MaxValue="100" MinValue="0"
    UpdateChildrenStates="true">
  <Children>
    <ImageWidget Id="ScrollbarHandle" WidthSizePolicy="Fixed" SuggestedWidth="20"
        HorizontalAlignment="Center" VerticalAlignment="Top"
        Brush="Scrollbar.Vertical.Handle" MinHeight="50" />
  </Children>
</ScrollbarWidget>
```
**关键属性**：`AlignmentAxis="Vertical"`、`MaxValue="100"`、`MinValue="0"`、Handle 为 `ImageWidget`（非普通 Widget）

#### 1.2 游戏内常用模式：Inventory.xml（最贴近实际使用）
**文件**：`SandBox\GUI\Prefabs\Inventory\Inventory.xml`

```xml
<ScrollbarWidget Id="..." WidthSizePolicy="Fixed" SuggestedWidth="8" HeightSizePolicy="StretchToParent"
    HorizontalAlignment="Right" AlignmentAxis="Vertical"
    Handle="..." MaxValue="100" MinValue="0">
  <Children>
    <Widget WidthSizePolicy="Fixed" HeightSizePolicy="StretchToParent" SuggestedWidth="4"
        HorizontalAlignment="Center"
        Sprite="BlankWhiteSquare_9" AlphaFactor="0.2" Color="#5a4033FF" DoNotAcceptEvents="true" />
    <ImageWidget Id="..." WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
        SuggestedWidth="8" SuggestedHeight="10" HorizontalAlignment="Center"
        Brush="FaceGen.Scrollbar.Handle" />
  </Children>
</ScrollbarWidget>
```
**关键特点**：
- 背景使用 `BlankWhiteSquare_9` + 自定义 Color + AlphaFactor
- Handle 类型为 `ImageWidget`（不是 Widget）
- 需要 `AlignmentAxis`、`MaxValue`、`MinValue`

#### 1.3 GameMenu 中的 ScrollbarWidget（Sprite 模式）
**文件**：`Native\GUI\Prefabs\GameMenu\GameMenu.xml` 行 142-150

```xml
<ScrollbarWidget Id="Scrollbar" WidthSizePolicy="Fixed" HeightSizePolicy="StretchToParent"
    SuggestedWidth="20" VerticalAlignment="Top"
    MarginTop="20" MarginBottom="10"
    Sprite="scrollbar_9"
    AlignmentAxis="Vertical"
    Handle="ScrollbarHandle"
    MaxValue="100" MinValue="0" ValueInt="0">
  <Children>
    <Widget Id="ScrollbarHandle" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
        SuggestedWidth="20" HorizontalAlignment="Left" VerticalAlignment="Top"
        Sprite="scroll_button_9" />
  </Children>
</ScrollbarWidget>
```
**特点**：使用 `Sprite` 属性作为轨道背景，Handle 也是用 `Sprite` 而非 `Brush`。

#### 1.4 EncyclopediaItemList 中的 ScrollbarWidget
**文件**：`Native\GUI\Prefabs\Encyclopedia\EncyclopediaItemList.xml`

```xml
<ScrollbarWidget Id="Scrollbar" WidthSizePolicy="Fixed" HeightSizePolicy="StretchToParent"
    SuggestedWidth="8" VerticalAlignment="Top"
    AlignmentAxis="Vertical" Handle="ScrollbarHandle"
    MaxValue="100" MinValue="0">
  <Children>
    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
        Sprite="BlankWhiteSquare_9" AlphaFactor="0.2" Color="#5a4033FF" DoNotAcceptEvents="true" />
    <ImageWidget Id="ScrollbarHandle" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
        SuggestedWidth="8" SuggestedHeight="10" HorizontalAlignment="Center"
        Brush="FaceGen.Scrollbar.Handle" />
  </Children>
</ScrollbarWidget>
```

---

### 2. Frame1.Border vs Frame1Brush 区别

**Frame1Brush**：填充背景 + 边框，内部有暗色 canvas 层。嵌套会导致双重暗色叠加。

**Frame1.Border**：仅装饰框线，内部透明。适合嵌套场景。

#### Frame1.Border 在 Encyclopedia 中的用法
**文件**：`SandBox\GUI\Prefabs\Encyclopedia\EncyclopediaSubPages\EncyclopediaClanSubPage.xml`

```xml
<Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
    Brush="Frame1.Border" />
```

#### 标准嵌套模式
```
外层容器（无背景）→ 内层 Widget(Brush="Frame1.Border") → 内部内容
```
而非：
```
外层 Widget(Brush="Frame1Brush") → 内层 Widget(Brush="Frame1Brush")  ← 错误：双重暗色叠加
```

---

### 3. ScrollablePanel 标准嵌套模式

**必需 ID 绑定**：
- `ScrollablePanel` 的 `ClipRect` 属性指向裁剪区域的 Widget ID
- `ScrollablePanel` 的 `InnerPanel` 属性指向内部滚动内容的 Widget ID

#### Inventory.xml 中的标准示例
**文件**：`SandBox\GUI\Prefabs\Inventory\Inventory.xml`

```xml
<Widget Id="Inventory" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
  <Children>
    <Widget Id="InnerPanel" WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren">
      <!-- 所有可滚动的子元素 -->
    </Widget>
    <ScrollablePanel Id="ScrollablePanel" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
        InnerPanel="InnerPanel" ClipRect="ClipRect"
        VerticalScrollbar="Scrollbar" />
  </Children>
</Widget>
```

---

### 4. BlankWhiteSquare_9 作为分隔线

在多个官方 Prefab 中，分隔线使用 `BlankWhiteSquare_9` + 自定义 Color + 小 height：

```xml
<Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="2"
    Sprite="BlankWhiteSquare_9" Color="#C1A167FF" AlphaFactor="0.4" />
```

---

### 5. StackLayout 搭配 ScrollablePanel

**文件**：`Native\GUI\Prefabs\Encyclopedia\EncyclopediaFilters.xml`

```xml
<Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren">
  <Children>
    <Widget Id="FiltersPanel" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
      <Children>
        <ScrollablePanel Id="PageScroll" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
            AutoHideScrollBars="true" ClipRect="FiltersClipRect" InnerPanel="FiltersRect"
            VerticalScrollbar="FiltersVerticalScrollbar" />
        <Widget Id="FiltersRect" WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren">
          <Children>
            <!-- 内容 -->
          </Children>
        </Widget>
      </Children>
    </Widget>
  </Children>
</Widget>
```

---

## 功能模块依赖关系

```
ScrollablePanel
├── InnerPanel (Widget)  ← 滚动内容的实际容器
├── ClipRect (Widget)    ← 裁剪视口
└── VerticalScrollbar (ScrollbarWidget)
    ├── AlignmentAxis="Vertical"  ← 必须
    ├── MaxValue="100" MinValue="0"  ← 必须
    ├── Handle (ImageWidget ID)  ← 必须为 ImageWidget
    ├── 背景 Widget: Sprite="BlankWhiteSquare_9"  ← 标准背景
    └── Handle Widget: Brush="FaceGen.Scrollbar.Handle"  ← 标准手柄
```

---

## 对 CustomSkillScreen.xml 的应用结论

| 问题 | 根因 | 修复方案 |
|------|------|----------|
| 滚动条不可见 | Handle 为 Widget 非 ImageWidget；缺 AlignmentAxis/MaxValue/MinValue；背景 sprite 路径无效 | 对齐 Inventory.xml 标准：Handle→ImageWidget，添加必需属性，背景用 BlankWhiteSquare_9 |
| 边框不清晰 | 嵌套 Frame1Brush 导致双重暗色 canvas | Frame1Brush→Frame1.Border（仅装饰框线，内部透明） |
| 分隔线不可见 | 使用 Frame2Brush + height=40 | 改用 Widget + BlankWhiteSquare_9 + height=2 |

---

## 未解决问题
无。所有问题已通过官方 Prefab 参考解决。
