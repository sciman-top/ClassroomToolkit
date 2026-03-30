# ClassroomToolkit UI 视觉终态重构设计

日期：2026-03-15  
状态：Approved Design Baseline  
范围：仅 UI 视觉、窗口壳层、交互文案与公共样式体系；不调整业务主干  
平台：Windows WPF (.NET 10)  

## 1. 背景与目标

ClassroomToolkit 已具备一套可用的深色主题和多窗口体系，但当前状态仍属于“已美化、未收口”：

- 多数窗口已接入 `Colors.xaml + WidgetStyles.xaml + Shell`，但主窗口、工具条、浮层和部分设置窗仍保留局部样式岛
- 颜色、圆角、阴影、图标尺寸、按钮密度尚未形成严格统一的视觉尺度
- 同一类操作在不同窗口中的控件样式和文案表达存在偏差
- 主工作窗、管理窗、设置窗、全屏浮层的产品层级感尚未完全拉开
- 一些 Tooltip、空状态、按钮文字和设置说明可进一步精简与统一

本次设计的目标不是“局部换皮”，而是把项目收口为一套稳定的终态视觉系统：

- 建立统一的深色专业课堂台视觉语言
- 建立统一的 Foundation Token、控件样式族和窗口壳层
- 对全部窗口进行统一归类，消除局部样式孤岛
- 同步优化文案、Tooltip 和状态提示，让表达精简、准确、清晰
- 严格控制性能，不因美化引入明显卡顿、额外渲染压力或全屏交互迟滞

## 2. 设计范围

### 2.1 包含

- 颜色、字体、字号、圆角、边框、阴影、间距、图标尺寸等视觉基础规则
- Button、ToggleButton、IconButton、TextBox、ComboBox、CheckBox、Slider、TabControl、ListView、TreeView、ContextMenu、ToolTip 等通用样式
- 主窗口、点名/倒计时工作窗、设置窗、管理窗、全屏展示窗、工具浮窗、提示浮层等全部窗口体系
- 标题栏、关闭按钮、底部操作栏、可点击卡片、分组卡片、侧边浮动控制条、空状态与状态提示
- 按钮标题、Tooltip、设置项说明、空状态提示、引导提示、状态反馈等文案体系

### 2.2 不包含

- RollCall、Paint、Photo、Diagnostics 等业务逻辑重写
- Windowing、Session、Interop 等行为编排重构
- 数据模型、设置存储格式、外部行为协议调整
- 新增第三方 UI 框架
- 任何高成本实时特效、复杂持续动画或行为语义变更

## 3. 终态视觉方向

本次采用的路线为：`深色专业课堂台`。

关键词：

- 深石板底色
- 青蓝主强调
- 教学绿辅助
- 琥珀提示
- 柔和红风险态
- 紧凑图标
- 轻玻璃感
- 低动画负担

不采用以下路线：

- 强玻璃和强辉光路线：容易破坏稳定感，并增加渲染负担
- 浅色专业面板路线：与现有体系差异过大，改造成本和风险都更高

终态观感应体现：

- 专业而不是炫技
- 紧凑但不拥挤
- 长时间课堂使用不疲劳
- 投屏、悬浮、全屏场景下焦点明确
- 不需要靠重特效制造“高级感”

## 4. 设计原则

### 4.1 Foundation First

所有视觉差异优先在 Token 和公共样式层收口，不允许继续在窗口内零散写局部视觉逻辑。

### 4.2 Shell Before Screen

每个窗口必须先归属窗口壳层，再做局部内容美化；禁止绕开壳层直接在窗口里堆样式。

### 4.3 Compact But Clear

本项目是课堂高频工具，优先追求操作效率、信息可读性和结构稳定性，不追求展示型大卡片和松散布局。

### 4.4 Copy Is Part Of UX

按钮文字、Tooltip、状态提示和设置说明属于产品体验的一部分，必须与视觉系统一并收口。

### 4.5 Performance Guard

所有美化都必须服从性能红线：画笔、全屏、工具条、缩略图管理等高频场景不得因 UI 视觉升级而产生明显卡顿。

## 5. 终态视觉规范

### 5.1 颜色体系

终态颜色采用深色石板工作台结构：

- `App Background`：最底层深石板蓝黑
- `Surface Primary`：窗口主面板
- `Surface Secondary`：卡片/次级容器
- `Input Background`：输入区与可编辑区
- `Overlay Mask`：遮罩与提示层

语义强调色：

- `Primary`：青蓝，用于主操作、焦点、选中态
- `Teaching` / `Success`：教学绿，用于开始、启用、正向状态
- `Warning`：琥珀，用于提醒、重置、次高关注操作
- `Danger`：柔和红，用于关闭、退出、清空、危险动作

约束：

- 单一窗口内只保留一个主强调色
- 危险色只用于真正危险动作
- 全屏展示类窗口尽量减少彩色强调，优先保证内容清晰
- 继续保留必要 alias 以兼容现有 XAML，但新增视觉逻辑只使用语义 token

### 5.2 字体体系

- 主字体：`Microsoft YaHei UI`
- 数字显示：`Consolas` 或等宽字体，仅用于倒计时和技术性数字

字号层级：

- 微说明：11
- 小说明：12
- 正文：13
- 对话框标题：14
- 管理窗标题：15
- 强调标题：18 及以上

约束：

- 正文默认 13，不允许因窗口不同而漂移
- 字重以 `Normal / SemiBold / Bold` 为主
- 不为单个窗口建立独立字体体系

### 5.3 圆角体系

统一为 4 档主尺度：

- `6`：小控件
- `8`：按钮、输入框、列表项
- `12`：卡片、次级面板
- `16-20`：窗口壳层、主工作卡片、浮动底栏

约束：

- 同一窗口最多使用 3 档圆角
- 重点工作窗允许更大的壳层圆角
- 小控件禁止使用过大圆角导致视觉发软

### 5.4 图标与按钮尺寸

图标统一为 4 档：

- `XS = 10`
- `SM = 12`
- `MD = 14`
- `LG = 18`

按钮统一为 5 类：

- `PrimaryButton`
- `SecondaryButton`
- `DangerButton`
- `IconButton`
- `ActiveIconButton / ToggleButton`

尺寸建议：

- 标准动作按钮高度：32
- 紧凑动作按钮高度：30
- 图标按钮：30 / 32 / 36
- 全屏侧栏按钮：40
- 标题栏关闭按钮统一引用公共尺寸令牌

约束：

- 同类操作必须使用同类按钮
- 不再在窗口内自由写 `10 / 15 / 17 / 20` 等离散图标尺寸
- 高密度工具区优先使用紧凑图标按钮

### 5.5 阴影、边框与动态

保留轻中两档阴影：

- `Shadow-Subtle`：卡片与轻浮层
- `Shadow-Medium`：对话框与重点浮层

边框：

- 默认 1px
- 强调以边框颜色和背景层变化为主，不靠厚描边和大 glow

动态：

- Hover：轻背景变化、轻边框高亮、轻微位移或缩放
- Press：轻量按下反馈
- Focus：聚焦下划线或边框强调
- 常规过渡：100-200ms

禁用：

- 大面积实时模糊
- 呼吸式辉光
- 大范围彩色阴影
- 高频复杂 Storyboard 链

## 6. 窗口壳层终态

### 6.1 WorkShell

适用窗口：

- [MainWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/MainWindow.xaml)
- [RollCallWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/RollCallWindow.xaml)

规则：

- 标题栏更轻、更矮，突出内容区
- 底部操作区统一为胶囊式悬浮条
- 主操作卡片聚焦明确，但不使用厚重 glow
- 作为项目终态视觉基准窗口

### 6.2 DialogShell

适用窗口：

- [AboutDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/AboutDialog.xaml)
- [AutoExitDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/AutoExitDialog.xaml)
- [RemoteKeyDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/RemoteKeyDialog.xaml)
- [TimerSetDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/TimerSetDialog.xaml)
- 以及设置类对话框

规则：

- 标题栏、分隔线、操作栏、关闭按钮完全统一
- 内容区边距与按钮尺寸统一
- 禁止继续保留手写壳层边框和自定义标题栏

### 6.3 ManagementShell

适用窗口：

- [ImageManagerWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml)
- [StudentListDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/StudentListDialog.xaml)
- [DiagnosticsDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml)

规则：

- 强化“信息管理台”感，而不是弹窗感
- 左右分栏、工具栏、内容区、底栏统一边界层级
- 列表、树、缩略图、状态栏全部走统一视觉规则

### 6.4 FullscreenShell

适用窗口：

- [PhotoOverlayWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml)
- [PaintOverlayWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml)
- 相关浮层提示窗

规则：

- 只保留必要控件
- 关闭按钮、侧边操作条、提示徽标统一
- 全屏遮罩和辅助提示尽量轻，不抢内容焦点

## 7. 窗口盘点与改造分组

### 7.1 已基本接入统一壳层

- `WorkShell`：MainWindow、RollCallWindow
- `DialogShell`：AboutDialog、AutoExitDialog、RemoteKeyDialog、TimerSetDialog
- `ManagementShell`：ImageManagerWindow、DiagnosticsDialog、StudentListDialog
- `FullscreenShell`：PaintOverlayWindow、PhotoOverlayWindow 已部分接入

### 7.2 仍存在局部样式岛或手写壳层

重点窗口：

- [MainWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/MainWindow.xaml)
  当前含本地 `HeroTile / MiniTool / MiniDanger`
- [PaintToolbarWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml)
  当前含本地 `Style_ColorBubble`
- [QuickColorPaletteWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml)
  当前含本地 `ColorBlockButtonStyle`
- [ImageManagerWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml)
  当前含本地 `ListViewItem` 变体
- [StudentListDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/StudentListDialog.xaml)
  当前含本地学生卡片样式
- [RollCallWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/RollCallWindow.xaml)
  当前仍有内联局部 Style
- [PaintSettingsDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml)
  当前仍有局部卡片 hover 边框样式与手写壳层
- [RollCallSettingsDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/RollCallSettingsDialog.xaml)
- [InkSettingsDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Ink/InkSettingsDialog.xaml)
- [ClassSelectDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/ClassSelectDialog.xaml)
- [BoardColorDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Paint/BoardColorDialog.xaml)
- [LauncherBubbleWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/LauncherBubbleWindow.xaml)
- [RollCallGroupOverlayWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Photos/RollCallGroupOverlayWindow.xaml)

## 8. 文案与 Tooltip 终态规范

本次将文案收口视为正式设计范围。

### 8.1 覆盖对象

- 按钮文字
- 菜单项文字
- Tooltip
- 空状态提示
- 加载状态提示
- 设置项标题与说明
- 全屏或浮窗中的引导提示

### 8.2 写法原则

- 精简：按钮优先 2 到 4 个字
- 准确：直接表达动作或结果
- 清晰：让教师一眼明白“这是什么、点了会怎样”
- 一致：同类动作使用同类表述
- 分层：按钮最短，Tooltip 稍补充，设置说明只解释必要边界

### 8.3 按钮文字规范

- 优先使用动作名词或短动词
- 避免口语化长句
- 避免同义词混用，例如“关闭 / 退出窗口 / 关掉”混杂

示例方向：

- “打开 PDF/图片管理”可在按钮层保留为“资源”或“管理”，Tooltip 再补全
- “打开名单”在按钮层收口为“名单”
- “开始/暂停倒计时”应根据状态分开呈现

### 8.4 Tooltip 规范

统一格式优先采用：

- `动作`
- `动作（补充条件）`
- `动作：结果`

示例方向：

- “开启画笔（长按可调设置）”
- “打开 PDF/图片管理”
- “按宽度适配并居中”
- “清空当前页笔迹”

### 8.5 空状态与加载状态

统一格式为：`当前状态 + 建议下一步`。

示例方向：

- `当前没有可显示内容，请先在左侧选择文件夹。`
- `正在加载名单...`
- `正在准备预览...`

约束：

- 不使用宣传式语言
- 不写感叹句
- 不使用模糊措辞

## 9. 实施落点

### 9.1 第一优先：Foundation 与公共样式

核心文件：

- [Colors.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Assets/Styles/Colors.xaml)
- [WidgetStyles.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml)
- [App.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/App.xaml)

动作：

- 调整色板、阴影、尺寸与圆角 token
- 收口按钮族、图标族、壳层、卡片、面板、浮层样式
- 为现有局部样式岛提供可替代的公共资源

### 9.2 第二优先：主工作窗

- [MainWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/MainWindow.xaml)
- [RollCallWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/RollCallWindow.xaml)

动作：

- 清除局部样式岛
- 用统一 WorkShell 建立主基准
- 同步优化主入口和工作底栏文案

### 9.3 第三优先：设置窗与管理窗

- [PaintSettingsDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Paint/PaintSettingsDialog.xaml)
- [RollCallSettingsDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/RollCallSettingsDialog.xaml)
- [InkSettingsDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Ink/InkSettingsDialog.xaml)
- [ClassSelectDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/ClassSelectDialog.xaml)
- [BoardColorDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Paint/BoardColorDialog.xaml)
- [ImageManagerWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml)
- [StudentListDialog.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/StudentListDialog.xaml)

动作：

- 手写壳层统一接入 DialogShell 或 ManagementShell
- 收口列表、卡片、说明文本、空状态和说明文案

### 9.4 第四优先：浮窗、工具条与全屏层

- [PaintToolbarWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml)
- [QuickColorPaletteWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml)
- [LauncherBubbleWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/LauncherBubbleWindow.xaml)
- [PhotoOverlayWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml)
- [PaintOverlayWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml)
- [RollCallGroupOverlayWindow.xaml](/E:/PythonProject/ClassroomToolkit/src/ClassroomToolkit.App/Photos/RollCallGroupOverlayWindow.xaml)

动作：

- 统一全屏关闭按钮、侧边控制、提示徽标和轻提示文案
- 统一工具条图标密度、颜色气泡和选中反馈
- 严控高频交互场景的视觉成本

## 10. 性能与行为红线

本次重构必须保持以下不变：

- 不改变 `Topmost`、`ShowInTaskbar`、`ResizeMode`、`WindowState` 等窗口行为语义
- 不改变透明窗口命中、穿透、输入转发等既有机制
- 不改变 `Windowing`、`Session`、`Interop` 的职责归属
- 不改业务流程、快捷键、数据结构和设置兼容性

严格禁止：

- 大面积实时模糊
- 高频复杂动画
- 对缩略图项、学生卡片、树节点等大量元素施加重阴影
- 以视觉改造名义将行为逻辑挪回热点窗口代码

## 11. 验证策略

实施后至少执行：

- `dotnet build ClassroomToolkit.sln -c Debug`

重点人工抽样窗口：

- MainWindow
- RollCallWindow
- PaintToolbarWindow
- PaintSettingsDialog
- ImageManagerWindow
- PhotoOverlayWindow / PaintOverlayWindow

重点人工观察项：

- 资源是否存在丢失引用
- 图标尺寸与按钮密度是否统一
- 标题栏、底栏、卡片、侧栏是否统一收口
- Tooltip、空状态和设置说明是否精简准确
- 全屏与工具条是否出现渲染迟滞或阴影过重

## 12. 回滚策略

回滚按层进行：

- Foundation 回滚：`Colors.xaml`、`WidgetStyles.xaml`、`App.xaml`
- 主工作窗回滚：`MainWindow.xaml`、`RollCallWindow.xaml`
- 管理与设置窗回滚：对应窗口 XAML
- 浮窗与全屏层回滚：对应窗口 XAML

回滚原则：

- 先回滚资源层，再看是否需要回滚窗口层
- 不触碰本次范围外的规则文件和业务代码

## 13. 结论

本设计将 ClassroomToolkit 的 UI 重构目标明确为：

- 视觉上收口为深色专业课堂台
- 结构上收口为 Foundation + Controls + Shell + Scene 的可维护体系
- 表达上收口为精简、准确、清晰的一致文案系统
- 实施上优先做公共样式，再分批回收局部样式岛
- 约束上严格服从课堂高频使用的性能红线

该设计获用户确认后，下一步应基于本设计编写实施计划，再进入代码改造阶段。
