# GauntletUI XML 结构完整分析

> 基于对 `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules` 下所有含 GUI 的 Mod 的 XML 文件系统性分析。
>
> 参考 Mod：Native、SandBox、SandBoxCore、StoryMode、Multiplayer、NavalDLC、CharacterReload、RBM、Bannerlord.MBOptionScreen、Bannerlord.UIExtenderEx、SmeltingOverhaul

---

## 一、文件顶层结构（强制规范）

```xml
<Prefab>
  <Constants>
    <!-- 从 Brush 定义中提取的常量值，用于计算动态尺寸 -->
  </Constants>
  <Parameters>
    <!-- 预制体可接收的外部参数 -->
  </Parameters>
  <Variables>
  </Variables>
  <VisualDefinitions>
    <!-- 动画过渡状态定义 -->
  </VisualDefinitions>
  <Window>
    <!-- 实际 UI 树 -->
  </Window>
</Prefab>
```

### 关键规则
1. **`<Prefab>` 是必须的根元素** — 没有它 GauntletUI 引擎无法解析
2. **`<Children>` 是必须的子控件容器** — 任何有子控件的 Widget 都必须用 `<Children>` 包裹子元素，**直接嵌套子 Widget 会被引擎忽略**（这就是之前 CustomSkillScreen 不显示的原因）
3. `<Constants>`、`<Parameters>`、`<Variables>`、`<VisualDefinitions>` 都是可选的，但位置必须在这个顺序
4. `<Variables>` 大多数预置体留空但仍保留标签

---

## 二、Constants 常量系统

### 2.1 基础常量（从 Brush 提取尺寸）

```xml
<Constant Name="SidePanel.Width" BrushLayer="Default" BrushName="Inventory.Tuple.Right" BrushValueType="Width" />
<Constant Name="TopBackground.Height" BrushLayer="Default" BrushName="Inventory.TopLeft.Background" BrushValueType="Height" />
```

- `BrushName`: 对应 Brushes XML 中定义的画刷名
- `BrushLayer`: 画刷图层（`Default`、`Frame`、`DefaultFill` 等）
- `BrushValueType`: 取 `Width` 或 `Height`

### 2.2 常量运算

```xml
<!-- 加法 -->
<Constant Name="CloseButtons.Margin.Top" Additive="45" Value="!ArmyManagement.Frame.Height" />

<!-- 乘法 -->
<Constant Name="SidePanel.Width.Scaled" MultiplyResult="1" Value="!SidePanel.Width" />

<!-- 取负 -->
<Constant Name="SidePanel.NegativeWidth" MultiplyResult="-1" Value="!SidePanel.Width" />
```

### 2.3 条件常量

```xml
<Constant Name="PreviousButtonBrush" BooleanCheck="*IsFlatDesign" OnTrue="Flat.Dropdown.Left.Button" OnFalse="SPOptions.Dropdown.Left.Button" />
```

### 2.4 常量引用（`!` 前缀）

在 Widget 属性中使用 `!` 引用常量：
```xml
SuggestedWidth="!SidePanel.Width"
SuggestedHeight="!Standard.TripleDialogCloseButtons.Background.Height"
```

---

## 三、Parameters 参数系统

预制体通过 `<Parameters>` 声明可接收的外部参数：

```xml
<Parameters>
  <Parameter Name="CancelButtonText" DefaultValue="- Missing -" />
  <Parameter Name="DoneButtonDisabled" DefaultValue="false" />
  <Parameter Name="IsFlatDesign" DefaultValue="false" />
  <Parameter Name="SelectorDataSource" DefaultValue="SelectorDataSource" />
</Parameters>
```

参数引用使用 `*` 前缀：
```xml
<TextWidget Text="*CancelButtonText" />
<ButtonWidget Command.Click="*CancelButtonAction" />
<TextWidget IsDisabled="*DoneButtonDisabled" />
```

### 标准组件参数模式

```xml
<!-- 调用 Standard.DialogCloseButtons -->
<Standard.DialogCloseButtons
    Parameter.CancelButtonAction="ExecuteCancel"
    Parameter.CancelButtonText="@CancelLbl"
    Parameter.DoneButtonAction="ExecuteDone"
    Parameter.DoneButtonText="@DoneLbl"
    Parameter.DoneInputKeyDataSource="{DoneInputKey}"
    Parameter.ShowCancel="false" />
```

---

## 四、VisualDefinitions 动画系统

```xml
<VisualDefinitions>
  <VisualDefinition Name="LeftMenu" EaseType="EaseOut" EaseFunction="Quint" TransitionDuration="0.45">
    <VisualState PositionXOffset="0" State="Default" />
  </VisualDefinition>
  <VisualDefinition Name="BottomMenu" EaseType="EaseOut" EaseFunction="Quint" TransitionDuration="0.45">
    <VisualState PositionYOffset="0" State="Default" />
  </VisualDefinition>
</VisualDefinitions>
```

用法：在 Widget 上指定 `VisualDefinition="Name"` 和初始偏移值：
```xml
<Widget VisualDefinition="BottomMenu" PositionYOffset="100">
```

---

## 五、Window 内 UI 树结构

### 5.1 基本 Widget 层级

```xml
<Window>
  <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
    <Children>
      <!-- 全屏半透明背景 -->
      <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
              Sprite="BlankWhiteSquare_9" Color="#000000CC" />

      <!-- 内容面板 -->
      <BrushWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
                   SuggestedWidth="637" SuggestedHeight="809"
                   HorizontalAlignment="Center" VerticalAlignment="Center"
                   Brush="Frame1Brush">
        <Children>
          <!-- 标题 -->
          <TextWidget ... Text="@TitleText" />
          <!-- 内容列表 -->
          <ListPanel ... />
          <!-- 底部按钮 -->
          <Standard.TripleDialogCloseButtons ... />
        </Children>
      </BrushWidget>

    </Children>
  </Widget>
</Window>
```

### 5.2 布局策略

| 策略 | 说明 |
|------|------|
| `WidthSizePolicy="StretchToParent"` | 撑满父容器 |
| `WidthSizePolicy="Fixed"` | 固定尺寸（配合 `SuggestedWidth`） |
| `WidthSizePolicy="CoverChildren"` | 适应子元素大小 |

### 5.3 对齐

```xml
HorizontalAlignment="Center"   <!-- Left / Center / Right -->
VerticalAlignment="Center"     <!-- Top / Center / Bottom -->
```

### 5.4 边距和偏移

```xml
MarginTop="20" MarginBottom="20" MarginLeft="10" MarginRight="10"
PositionXOffset="-550"   <!-- 相对对齐位置的偏移 -->
PositionYOffset="100"
```

---

## 六、数据绑定

### 6.1 属性绑定

| 绑定语法 | 说明 | 示例 |
|----------|------|------|
| `DataSource="{VMProperty}"` | 绑定 ViewModel 子属性 | `DataSource="{ItemList}"` |
| `Text="@TextProperty"` | 绑定文本 | `Text="@ItemName"` |
| `IntText="@IntProperty"` | 绑定整数文本 | `IntText="@CurrentGold"` |
| `IsVisible="@Condition"` | 绑定可见性 | `IsVisible="@HasMount"` |
| `IsEnabled="@CanClick"` | 绑定启用状态 | `IsEnabled="@CanLearnSkill"` |

### 6.2 命令绑定

```xml
Command.Click="ExecuteAction"
Command.HoverBegin="ExecuteBeginHint"
Command.HoverEnd="ExecuteEndHint"
```

还可以使用 `CommandParameter.Click` 传参：
```xml
Command.Click="SetSelectedCategory" CommandParameter.Click="0"
```

### 6.3 子 DataSource 绑定

```xml
<!-- 从父级 DataSource 的子属性绑定 -->
<ButtonWidget DataSource="{CurrentSkill}" Text="@SkillName" />
<TextWidget DataSource="{..}" Text="@FromGrandParent" />

<!-- 来自 Hint 的 DataSource -->
<HintWidget DataSource="{..\NextCharacterHint}" />
```

---

## 七、常用 Widget 类型速查表

### 7.1 基础容器

| Widget | 用途 |
|--------|------|
| `Widget` | 通用容器，可用 `Sprite` 做背景 |
| `BrushWidget` | 使用 Brush（画刷）做背景的容器 |
| `ListPanel` | 布局容器，通过 `StackLayout.LayoutMethod` 控制方向 |
| `ScrollablePanel` | 滚动区域，需配合 `ClipRect` 和 `InnerPanel` |

### 7.2 文本

| Widget | 用途 |
|--------|------|
| `TextWidget` | 普通文本 |
| `RichTextWidget` | 富文本 |
| `ScrollingTextWidget` | 超长文本自动滚动 |
| `ScrollingRichTextWidget` | 超长富文本自动滚动 |
| `WarningTextWidget` | 带警告状态的文本（参数 `IsWarned`） |
| `AutoHideRichTextWidget` | 文本为空时自动隐藏 |
| `AutoHideZeroTextWidget` | 值为 0 时自动隐藏 |
| `EditableTextWidget` | 可编辑文本输入框 |

### 7.3 按钮

| Widget | 用途 |
|--------|------|
| `ButtonWidget` | 普通按钮 |
| `ToggleButtonWidget` | 切换按钮（参数 `WidgetToClose` 控制目标 Widget 的显隐） |

`ButtonWidget` 重要属性：
- `Brush="ButtonBrush"` — 背景画刷
- `ButtonType="Radio"` — 单选模式（配合 `IsSelected`）
- `DoNotPassEventsToChildren="true"` — 防止事件穿透到子元素
- `UpdateChildrenStates="true"` — 按钮状态变化时更新子元素
- `IsSelected="@Condition"` — 选中状态

### 7.4 列表和网格

```xml
<!-- 数据列表 -->
<NavigatableListPanel DataSource="{Items}" WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
    StackLayout.LayoutMethod="VerticalTopToBottom" MinIndex="0" MaxIndex="50">
  <ItemTemplate>
    <!-- 每个项的模板 -->
  </ItemTemplate>
</NavigatableListPanel>

<!-- 网格（如技能网格） -->
<NavigatableGridWidget Id="SkillsGrid" DataSource="{Skills}"
    DefaultCellWidth="178" DefaultCellHeight="137" ColumnCount="3">
  <ItemTemplate>
    <SkillGridItem />
  </ItemTemplate>
</NavigatableGridWidget>
```

### 7.5 进度条 / 滑动条

```xml
<!-- 填充条 -->
<FillBarWidget Id="SkillProgressFillBarWidget" DataSource="{CurrentSkill}"
    SuggestedWidth="304" SuggestedHeight="17"
    Sprite="little_progressbar_frame_9"
    ContainerWidget="ContainerWidget" FillWidget="FillBarParent\FillWidget"
    InitialAmount="@CurrentSkillXP" MaxAmount="@XpRequiredForNextLevel">
  <Children>
    <Widget Id="FillBarParent" ...>
      <Children>
        <Widget Id="FillWidget" ... Sprite="progress_fill" />
      </Children>
    </Widget>
    <Widget Id="ContainerWidget" ... Sprite="progress_frame" />
  </Children>
</FillBarWidget>

<!-- 滑块 -->
<SliderWidget SuggestedWidth="338" SuggestedHeight="42"
    Filler="Filler" Handle="SliderHandle"
    MaxValueInt="63" MinValueInt="1" ValueInt="@LevelValue"
    DoNotUpdateHandleSize="true">
  <Children>
    <Widget Id="Filler" ... Sprite="slider_fill" />
    <BrushWidget Id="SliderHandle" ... Brush="SPOptions.Slider.Handle" />
  </Children>
</SliderWidget>
```

### 7.6 特殊 Widget

| Widget | 用途 | 来源 |
|--------|------|------|
| `HintWidget` | 悬浮提示（`Command.HoverBegin="ExecuteBeginHint"`） | Native |
| `InputKeyVisualWidget` | 显示按键图标（`KeyID="@KeyID"`） | Native |
| `CharacterTableauWidget` | 3D 角色预览 | SandBox |
| `AnimatedDropdownWidget` | 下拉菜单 | Native |
| `NavigationScopeTargeter` | 手柄导航区域定义 | Native |
| `FillBarWidget` | 进度条 | Native |
| `MaskedTextureWidget` | 遮罩纹理（旗帜等） | Native |
| `SkillIconVisualWidget` | 技能图标 | SandBox |
| `TutorialHighlightItemBrushWidget` | 教程高亮 | SandBox |
| `ElementNotificationWidget` | 教程通知 | SandBox |
| `ValueBasedVisibilityWidget` | 基于值的可见性控制 | SandBox |

---

## 八、标准预制体组件（`Standard.*`）

位于 `Native/GUI/Prefabs/Standard/`，所有 Mod 通用。

### 8.1 Standard.Background
带粒子烟雾动画的全屏背景：
```xml
<Standard.Background />
```
参数：`IsParticleVisible`, `IsSmokeVisible`, `IsAnimEnabled`, `IsFullscreenImageEnabled`, `SmokeColorFactor`, `ParticleOpacity`

### 8.2 Standard.TopPanel
顶部标题面板：
```xml
<Standard.TopPanel Parameter.Title="@TitleText" />
```

### 8.3 Standard.DialogCloseButtons
底部双按钮（取消 + 确认）：
```xml
<Standard.DialogCloseButtons
    Parameter.CancelButtonAction="ExecuteCancel"
    Parameter.CancelButtonText="@CancelLbl"
    Parameter.DoneButtonAction="ExecuteDone"
    Parameter.DoneButtonText="@DoneLbl"
    Parameter.DoneInputKeyDataSource="{DoneInputKey}"
    Parameter.ShowCancel="false"    <!-- 可选：隐藏取消按钮 -->
    Parameter.IsDoneEnabled="true" />
```

### 8.4 Standard.TripleDialogCloseButtons
底部三按钮（取消 + 重置 + 确认），用于角色编辑等需要重置的界面：
```xml
<Standard.TripleDialogCloseButtons
    Parameter.CancelButtonAction="ExecuteCancel"
    Parameter.CancelButtonText="@CancelLbl"
    Parameter.CancelInputKeyDataSource="{CancelInputKey}"
    Parameter.DoneButtonAction="ExecuteDone"
    Parameter.DoneButtonText="@DoneLbl"
    Parameter.DoneInputKeyDataSource="{DoneInputKey}"
    Parameter.ResetButtonAction="ExecuteReset"
    Parameter.ResetInputKeyDataSource="{ResetInputKey}"
    Parameter.ResetButtonHintDataSource="{ResetHint}"
    Parameter.DoneButtonDisabled="@IsDoneDisabled" />
```

### 8.5 Standard.TriplePopupCloseButtons
顶部弹窗三按钮（取消 + 重置 + 确认），Button 从顶部对齐：
```xml
<Standard.TriplePopupCloseButtons
    Parameter.CancelButtonAction="ExecuteCancel"
    Parameter.CancelButtonText="@CancelLbl"
    Parameter.DoneButtonAction="ExecuteDone"
    Parameter.DoneButtonText="@DoneLbl"
    Parameter.ResetButtonAction="ExecuteReset" />
```

### 8.6 Standard.PopupCloseButton
单独的关闭按钮：
```xml
<Standard.PopupCloseButton
    Parameter.ButtonText="@CloseText"
    Parameter.ButtonAction="ExecuteClose"
    Parameter.IsEnabled="true" />
```

### 8.7 Standard.DropdownWithHorizontalControl
下拉选择器（需要 SelectorVM 类型的 DataSource）：
```xml
<Standard.DropdownWithHorizontalControl
    HorizontalAlignment="Left"
    Parameter.SelectorDataSource="{RBMCombatEnabled}"
    Parameter.IsEnabled="@IsEnabled" />
```

### 8.8 Standard.ScrollablePanel
可复用的滚动面板：
```xml
<Standard.ScrollablePanel
    Parameter.OverlayShadowBrush="InnerFrameShadow1Brush"
    Parameter.ScrollbarVisible="true"
    Parameter.InnerPanelVerticalAlignment="Top">
  <Children>
    <!-- 内容放在这里，会通过 LogicalChildrenLocation 注入 -->
  </Children>
</Standard.ScrollablePanel>
```

### 8.9 Standard.Window
基础窗口（Frame1Brush + 标题）：
```xml
<Standard.Window Parameter.Title="@TitleText" Parameter.Brush="Frame1Brush">
  <Children>
    <!-- 内容 -->
  </Children>
</Standard.Window>
```

---

## 九、完整界面案例对照

### 9.1 简单独占界面（EscapeMenu）

特点：全屏半透明背景 + 居中面板 + 数据列表 + 无底部标准按钮。

```xml
<Prefab>
  <Constants>...</Constants>
  <Window>
    <!-- 全屏半透明遮罩 -->
    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
            Sprite="BlankWhiteSquare_9" Color="#000000FF" AlphaFactor="0.4">
      <Children>
        <!-- 居中面板 -->
        <Widget Id="EscapeMenu" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
                SuggestedWidth="!EscapeMenu.Background.Width"
                SuggestedHeight="!EscapeMenu.Background.Height"
                HorizontalAlignment="Center" VerticalAlignment="Center"
                Sprite="SPGeneral\EscapeMenu\escape_panel">
          <Children>
            <!-- 数据绑定按钮列表 -->
            <NavigatableListPanel Id="ButtonsContainer" DataSource="{MenuItems}"
                StackLayout.LayoutMethod="VerticalTopToBottom"
                WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren"
                MarginTop="100" MarginBottom="115">
              <ItemTemplate>
                <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" ...>
                  <Children>
                    <EscapeMenuButtonWidget Command.Click="ExecuteAction" ... />
                  </Children>
                </Widget>
              </ItemTemplate>
            </NavigatableListPanel>
          </Children>
        </Widget>
      </Children>
    </Widget>
  </Window>
</Prefab>
```

### 9.2 带标准背景和动画的界面（ClanScreen）

特点：`Standard.Background` + `Standard.DialogCloseButtons` + `VisualDefinition` 动画。

```xml
<Prefab>
  <Constants>...</Constants>
  <VisualDefinitions>
    <VisualDefinition Name="BottomMenu" EaseType="EaseOut" EaseFunction="Quint" TransitionDuration="0.45">
      <VisualState PositionYOffset="0" State="Default" />
    </VisualDefinition>
    <VisualDefinition Name="TopPanel" EaseType="EaseOut" EaseFunction="Quint" TransitionDuration="0.45">
      <VisualState PositionYOffset="0" State="Default" />
    </VisualDefinition>
  </VisualDefinitions>
  <Window>
    <ClanScreenWidget Id="ClanScreenWidget" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
      <Children>
        <Standard.Background />

        <!-- 下半部分内容 -->
        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
                MarginTop="188" MarginBottom="75">
          <Children>
            <ClanMembers DataSource="{ClanMembers}" IsVisible="false" />
            <ClanParties DataSource="{ClanParties}" IsVisible="true" />
            <!-- 通过 IsVisible 实现 Tab 切换 -->
          </Children>
        </Widget>

        <!-- 图块按钮栏 -->
        <Standard.DialogCloseButtons
            VisualDefinition="BottomMenu" PositionYOffset="100"
            Parameter.DoneButtonAction="ExecuteClose"
            Parameter.DoneButtonText="@DoneLbl"
            Parameter.DoneInputKeyDataSource="{DoneInputKey}"
            Parameter.ShowCancel="false" />
      </Children>
    </ClanScreenWidget>
  </Window>
</Prefab>
```

### 9.3 分屏界面（InventoryScreen）

特点：自定义特殊 Widget 作为根 + 左右面板 + Tab 过滤 + 3D 角色 + 拖放。

（见 `SandBox/GUI/Prefabs/Inventory/Inventory.xml`，560 行完整参考）

### 9.4 滚动列表界面（CampaignOptions）

特点：`ScrollablePanel` + `NavigatableListPanel` + `ItemTemplate`。

（见 `SandBox/GUI/Prefabs/CampaignOptions.xml`，169 行完整参考）

### 9.5 Mod 独立界面（RBMConfig）

特点：不使用 `Standard.*` 背景，直接 Widget 起步 + `Standard.TopPanel` + `Standard.DialogCloseButtons` + `Standard.DropdownWithHorizontalControl`。

```xml
<Prefab>
  <Constants>...</Constants>
  <Window>
    <Widget Id="InterfaceScreen" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
      <Children>
        <Standard.Background />

        <!-- 主内容（左右分栏） -->
        <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
                   MarginTop="200" MarginBottom="60" MarginLeft="50" MarginRight="150">
          <Children>
            <!-- 左栏：设置项列表 -->
            <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren">
              <Children>
                <ListPanel LayoutImp.LayoutMethod="VerticalBottomToTop" ...>
                  <Children>
                    <!-- 每个设置项：标签 + 下拉框 -->
                    <ListPanel LayoutImp.LayoutMethod="HorizontalLeftToRight">
                      <Children>
                        <RichTextWidget Text="@LabelText" ... />
                        <Standard.DropdownWithHorizontalControl
                            Parameter.SelectorDataSource="{Selector}" />
                      </Children>
                    </ListPanel>
                  </Children>
                </ListPanel>
              </Children>
            </ListPanel>
          </Children>
        </ListPanel>

        <!-- 顶部标题栏 -->
        <Standard.TopPanel Parameter.Title="@TitleText" />

        <!-- 底部按钮 -->
        <Standard.DialogCloseButtons
            Parameter.CancelButtonAction="ExecuteCancel"
            Parameter.CancelButtonText="@CancelText"
            Parameter.DoneButtonAction="ExecuteDone"
            Parameter.DoneButtonText="@DoneText" />
      </Children>
    </Widget>
  </Window>
</Prefab>
```

---

## 十、常见模式与最佳实践

### 10.1 总是需要 `<Children>` 标签

**错误（子元素不会被渲染）：**
```xml
<Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
  <ListPanel ...>
    <TextWidget ... />
  </ListPanel>
</Widget>
```

**正确：**
```xml
<Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
  <Children>
    <ListPanel ...>
      <Children>
        <TextWidget ... />
      </Children>
    </ListPanel>
  </Children>
</Widget>
```

### 10.2 Tab 切换

通过 `IsVisible` / `IsHidden` 绑定布尔属性实现面板切换，不涉及销毁/创建：

```xml
<ClanMembers DataSource="{ClanMembers}" IsVisible="@IsMembersSelected" />
<ClanParties DataSource="{ClanParties}" IsVisible="@IsPartiesSelected" />
<ClanFiefs DataSource="{ClanFiefs}" IsVisible="@IsFiefsSelected" />
```

### 10.3 按钮状态控制

```xml
<!-- 启用/禁用 -->
<ButtonWidget IsEnabled="@CanLearn" UpdateChildrenStates="true">
  <Children><TextWidget ... /></Children>
</ButtonWidget>

<!-- 隐藏时显示禁用的 Hint -->
<Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" IsDisabled="*IsEnabled">
  <Children>
    <HintWidget DataSource="*DisabledHintDataSource"
        Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" />
  </Children>
</Widget>
```

### 10.4 ScrollbarWidget 标准模式

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

### 10.5 搜索/输入框模式

```xml
<BrushWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
    SuggestedWidth="570" SuggestedHeight="55"
    Brush="SaveLoad.Search.Button" IsVisible="@IsSearchAvailable">
  <Children>
    <EditableTextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
        MarginTop="10" MarginBottom="10" MarginLeft="10" MarginRight="10"
        Brush="SaveLoad.Search.InputText"
        DefaultSearchText="@SearchPlaceholderText"
        Text="@SearchText" />
  </Children>
</BrushWidget>
```

---

## 十一、尺寸策略速查

| SizePolicy | 宽度行为 | 典型属性搭配 |
|------------|----------|--------------|
| `StretchToParent` | 撑满父容器 | （不需要 SuggestedWidth） |
| `Fixed` | 固定尺寸 | `SuggestedWidth="200"` 或 `SuggestedWidth="!ConstName"` |
| `CoverChildren` | 适应子元素大小 | （自动计算） |

---

## 十二、修饰/行为属性

| 属性 | 说明 |
|------|------|
| `DoNotAcceptEvents="true"` | Widget 不接收鼠标事件（穿透） |
| `DoNotPassEventsToChildren="true"` | 不向子元素传递事件（通常在 Button 上） |
| `DoNotUseCustomScale="true"` | 不受 UI 缩放影响 |
| `DoNotUseCustomScaleAndChildren="true"` | 自身及子元素都不缩放 |
| `UpdateChildrenStates="true"` | 自身状态变化时更新子元素 |
| `ClipContents="true"` | 裁剪超出边界的内容 |
| `RenderLate="true"` | 延迟渲染（面板叠加时控制层级） |
| `ExtendCursorAreaLeft="10"` | 扩展点击区域 |
| `ForcePixelPerfectRenderPlacement="true"` | 像素完美渲染 |
| `IsDisabled="true"` / `IsEnabled="false"` | 禁用态 |
| `IsVisible="true/false"` | 可见态 |
| `IsHidden="true/false"` | 隐藏态（独立于 IsVisible，来自 ViewModel） |
| `AlphaFactor="0.5"` | 透明度倍率 |
| `ColorFactor="1.5"` | 颜色倍率 |
| `AcceptDrop="true"` | 接受拖放 |
| `GamepadNavigationIndex="0"` | 手柄导航顺序 |

---

## 十三、Brush vs Sprite

| 属性 | 类型 | 来源 | 示例 |
|------|------|------|------|
| `Brush="Name"` | 画刷引用 | Brushes XML 文件 | `Brush="Frame1Brush"` |
| `Sprite="path"` | 直接精灵路径 | Asset 资源 | `Sprite="StdAssets\triple_button_frame"` |
| `Brush.Color="#RRGGBBAA"` | 画刷颜色覆写 | 内联 | `Brush="InventoryWeightFont" Brush.FontSize="20"` |
| `Color="#000000CC"` | Widget 颜色叠加 | 内联 | `Color="#000000CC" AlphaFactor="0.4"` |

---

## 十四、从零创建 GauntletUI 界面 Checklist

参照以上所有案例，创建一个新界面的步骤：

1. **创建 XML 文件** 在 `GUI/Prefabs/` 下
2. **包裹 `<Prefab>`** 作为根元素
3. **定义 `<Constants>`**（如果需要从 Brush 提取尺寸）
4. **定义 `<Parameters>`**（如果需要外部传入参数）
5. **在 `<Window>` 内构建 UI 树**：
   - 根 Widget（`StretchToParent` 撑满全屏）
   - 始终用 `<Children>` 包裹子元素
   - 选择背景：`<Standard.Background />` 或手动 Sprite
   - 添加内容面板（居中 `BrushWidget`）
   - 添加 `Standard.TopPanel`（可选标题栏）
   - 布局内容列表/网格
   - 添加底部按钮：`Standard.DialogCloseButtons` 或 `Standard.TripleDialogCloseButtons`
   - 连接 DataSource 和数据绑定
6. **在对应 Brush XML 中定义需要的 Brushes**（`GUI/Brushes/` 下）
7. **在 C# 代码中通过 GauntletLayer 加载**：
   ```csharp
   _gauntletLayer.LoadMovie("PrefabName", viewModel)
   ```

---

## 十五、与旧版 CustomSkillScreen.xml 的对比

| 问题 | 旧版错误 | 正确做法 |
|------|----------|----------|
| 根元素 | 直接 `<Window>` | 必须用 `<Prefab>` 包裹 |
| 子元素容器 | 直接嵌套：`<Widget><ListPanel>...` | 必须用 `<Children>` 包裹 |
| 背景 | 自绘 Widget + 纯色 | 可用 `<Standard.Background />` |
| 按钮 | 自绘 `ButtonBrush1` 按钮 | 使用 `Standard.DialogCloseButtons` |
| 尺寸定义 | 硬编码数值 | 可从 Brush 提取 `!ConstantName` |
| 数据绑定 | `Command.Click="Done"` | `Command.Click="ExecuteDone"`（推荐命名规范） |
