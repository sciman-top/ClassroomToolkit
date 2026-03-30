# ClassroomToolkit UI/窗口体系终态重构设计

日期：2026-03-14  
状态：Approved Design Baseline  
范围：仅 UI/窗口体系；不调整业务架构主干  
平台：Windows WPF (.NET 8)  

## 1. 背景与目标

ClassroomToolkit 当前已具备一套可用的暗色主题与多窗口体系，但其视觉与结构存在以下问题：

- 不同窗口的视觉语言不完全统一，像多轮演进叠加后的结果
- 主窗口、工作窗口、设置窗口、管理窗口、全屏窗口之间缺少明确的模板边界
- 按钮、图标、字号、圆角、阴影、间距缺少严格的尺寸体系
- 局部存在偏霓虹、偏发光、偏展示型的表达，不完全符合课堂高频长时使用场景
- 设置与管理类窗口的信息层级仍然偏“字段堆叠”，产品成熟感不足

本设计的目标不是做一次换皮，而是建立可长期维护的终态 UI 架构：

- 建立统一的设计令牌层
- 建立统一的控件样式层
- 建立统一的窗口壳层和场景模板层
- 对现有窗口做系统级重构，使其在视觉、信息架构和交互层级上收敛为同一产品
- 在重构过程中严格控制性能，不引入明显卡顿、内存膨胀或透明窗口渲染压力

## 2. 设计范围

### 2.1 包含

- 颜色、字号、圆角、边框、阴影、间距、图标尺寸等视觉基础规则
- Button、ToggleButton、IconButton、TextBox、ComboBox、CheckBox、Slider、TabControl、ListView、TreeView、ContextMenu、ToolTip 等通用样式
- 主窗口、点名/倒计时工作窗、画笔工具条、全屏展示窗、PDF/图片管理窗、设置窗、轻对话框等窗口体系
- 标题栏、关闭按钮、底部操作栏、滚动区、空状态、状态标签、分组卡片等结构模板

### 2.2 不包含

- RollCall、Paint、Photo、Diagnostics 等业务逻辑重写
- ViewModel、服务层、应用模块边界重构
- 数据模型、设置存储格式、外部行为协议调整
- 新增高成本渲染特效或第三方 UI 框架替换
- 命令语义、业务流程语义、状态机语义、快捷键语义变更

## 3. 设计原则

### 3.1 产品气质

目标气质为：深色、专业、克制、稳定。

不采用当前偏霓虹/偏展示的视觉路线，也不走明亮传统教学软件路线。终态应体现：

- 专业而不是炫技
- 长时间观看不疲劳
- 高对比但不过饱和
- 紧凑但不压迫
- 有产品感但不过度装饰

### 3.2 终态 UI 架构原则

- Foundation：所有视觉基础规则语义化，禁止继续散落硬编码
- Controls：控件样式族最小化，尽量用少量标准样式覆盖全项目
- WindowShell：非全屏窗口统一外壳结构
- SceneTemplate：每个窗口必须归属于明确模板
- PerformanceGuard：所有视觉决策必须受性能红线约束

### 3.3 性能优先原则

课堂场景要求稳定、轻量、可持续使用，UI 重构必须满足：

- 不新增实时模糊
- 不新增大面积半透明叠层
- 不新增高频复杂 Storyboard
- 不把 DropShadowEffect 扩散到大量小控件和列表项
- 全屏场景优先内容清晰，不靠装饰制造“高级感”
- 缩略图、列表、树形、WrapPanel 场景优先静态视觉

### 3.4 非视觉行为不变式

本次重构只改变 UI/窗口体系，不改变窗口系统既有行为契约。以下行为默认为不变式，除非后续单独立项：

- Owner/OwnedWindow 关系不因美化而改变
- Topmost、ShowInTaskbar、ShowActivated、WindowState、ResizeMode 等窗口行为语义不改变
- 模态/非模态边界不改变
- Alt+Tab、ESC、Enter、鼠标拖拽、窗口关闭、焦点恢复等行为不改变
- 透明窗口的命中、穿透、输入转发策略不改变
- DPI、PerMonitorV2、跨屏、投影场景中的尺寸与定位语义不改变
- 现有 Windowing 协调层继续负责行为编排；UI 重构不得把这些职责偷偷迁入 XAML 视觉层

## 4. 终态视觉规范

### 4.1 颜色体系

终态颜色体系改为“深石墨 + 冷蓝主色 + 低饱和功能色”。

建议语义层级：

- App Background：深石墨蓝黑，用于应用级最底层
- Surface Background：窗口主面板底色，比底色亮一层
- Card Background：卡片与分组容器底色，比主面板再亮半层
- Input Background：输入控件和可编辑区域专用底色
- Primary：冷蓝，作为主操作与焦点色
- Success：低饱和青绿
- Warning：琥珀
- Danger：砖红
- Text Primary / Secondary / Tertiary：三级文本对比
- Border Default / Strong / Focus：三级边框强度

约束：

- 单一窗口内只保留一个主强调色
- 危险色只用于退出、关闭、清空、删除等操作
- 不同时使用蓝、紫、青作为同级强调色
- 全屏展示窗口中，非必要不使用彩色强调

### 4.2 字体体系

延续系统字体策略，不引入额外字体依赖。

建议：

- 主字体：Microsoft YaHei UI
- 数字/计时：Consolas，仅用于计时与技术性文本
- 主标题：18
- 区块标题：15
- 正文：13
- 辅助：12
- 微说明：11

约束：

- 正文不低于 13
- 字重以 Normal / SemiBold / Bold 三档为主
- 不随窗口各自定义字号体系

### 4.3 圆角体系

建议统一为 5 档：

- Radius-6：小控件
- Radius-8：普通按钮/输入框
- Radius-12：卡片/分组面板
- Radius-16：普通窗口壳
- Radius-18：主窗口/重点工作窗

约束：

- 同一窗口最多使用 3 档圆角
- 禁止无差别大圆角导致视觉发软

### 4.4 尺寸体系

按钮与图标采用统一尺寸体系。

按钮：

- 微型图标按钮：28x28
- 标准图标按钮：32x32
- 重要图标按钮：36x36
- 标准文本按钮：高度 32
- 主按钮：高度 36
- 工具条按钮：36
- 主窗口 Hero 功能按钮：64-68

图标：

- 微型按钮内图标：12
- 标准按钮内图标：14-16
- 标题栏/工具条图标：16
- 主功能图标：18-20
- 主功能图标：20

约束：

- 同类按钮统一尺寸
- 同类图标统一视觉重量
- 取消过多离散尺寸档位

### 4.5 边框、阴影与动态

边框：

- 默认 1px
- 强调主要靠边框颜色变化，不靠发光

阴影：

- Shadow-0：无阴影
- Shadow-1：轻层级阴影
- Shadow-2：浮窗阴影

动态：

- Hover：轻底色变化或轻透明度变化
- Press：1px 内轻位移
- 常规过渡：100-150ms
- 对话框进入：轻淡入或极轻位移

禁用：

- 呼吸式 glow
- 大范围彩色阴影
- 连续脉冲
- 复杂动画链

## 5. 终态 UI 架构

### 5.1 Foundation 层

职责：

- 颜色令牌
- 字体与字号令牌
- 圆角令牌
- 边框令牌
- 阴影令牌
- 间距令牌
- 图标尺寸令牌

要求：

- 所有新样式必须基于语义令牌而不是直接写死值
- 允许兼容过渡期，但终态不允许窗口内继续大量硬编码

### 5.2 Controls 层

核心控件家族：

- 主按钮
- 次按钮
- 危险按钮
- 图标按钮
- 图标切换按钮
- 轻量预设按钮
- 标准输入框
- 下拉框
- 复选框
- 滑杆
- TabControl
- ListView/ListBox
- TreeView
- ContextMenu
- ToolTip

要求：

- 控件风格以少量样式族覆盖多数场景
- 场景差异应尽量通过容器和模板解决，不应无限增加按钮变体

#### 5.2.1 控件令牌基线映射（ui-controls-token-baseline）

令牌源文件：

- `src/ClassroomToolkit.App/Assets/Styles/Colors.xaml`
- `src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml`

核心令牌与家族映射：

| 控件家族 | 语义令牌（Colors.xaml） | 样式落点（WidgetStyles.xaml） |
|---|---|---|
| Button（主/次/危险/图标） | `Brush_Primary` / `Gradient_Primary` / `Gradient_Danger` / `Brush_Surface_Hover` / `Brush_Border` | `Style_PrimaryButton`、`Style_SecondaryButton`、`Style_DangerButton`、`Style_IconButton`、`Style_IconButton_Active` |
| Input（输入/下拉/勾选/滑杆） | `Brush_Background_L3` / `Brush_Border` / `Brush_Text_Primary` / `Brush_Primary` | `TextBox`、`ComboBox`、`CheckBox`、`Slider` |
| List & Navigation（Tab/List/Tree/Menu/Tooltip） | `Brush_Background_L2` / `Brush_Surface_Active` / `Brush_Text_Secondary` / `Brush_Border_Glass` | `TabControl`、`ListView`、`Style_CleanListBox`、`TreeView`、`ContextMenu`、`MenuItem`、`ToolTip` |

阶段约束：

- Shell 阶段任务必须依赖 controls 子链路完成（`ui-controls-token-baseline -> ui-controls-button-and-input-family -> ui-controls-list-and-menu-family`），不得提前开始 `window-shell`。

### 5.3 WindowShell 层

WindowShell 定义的是“壳层类型 + 槽位契约”，不是要求所有非全屏窗口都带完整标题栏和底部按钮栏。

Shell 类型与槽位/行为契约如下：

| Shell | 必选槽位 | 可选槽位 | 禁用槽位 | 拖拽热区 | Resize hit-test | 关闭来源 | 透明命中 | 激活策略 | 任务栏策略 | owner/modal 约束 | 验收方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| DialogShell | TitleBar, Content, ActionBar | Subtitle, ScrollRegion | SideRail | TitleBar | 按窗口现状保留 | TitleBar Close + ActionBar | 否 | 正常激活 | 按现状保留 | owner/modal 维持现状 | 人工 |
| ManagementShell | TitleBar, Toolbar, Content, StatusBar | SideRail, EmptyState | FloatingHandle | TitleBar | 按窗口现状保留 | TitleBar Close | 否 | 正常激活 | 按现状保留 | owner 维持现状 | 人工 |
| FloatingShell | Content | FloatingHandle, MiniToolbar | Full TitleBar, Standard ActionBar | FloatingHandle 或容器显式热区 | 默认禁用，除非现状已有 | 显式按钮或外部关闭 | 仅按现状允许 | 可激活或非激活按现状保留 | 不进入任务栏 | owner 维持现状 | 人工 |
| WorkShell | TitleBar, Content, ActionBar | Subtitle, StatusBar | SideRail | TitleBar | 按窗口现状保留 | TitleBar Close + ActionBar | 否 | 正常激活 | 按现状保留 | owner 维持现状 | 人工 |
| FullscreenShell | Content | EdgeTools, OverlayTitle, OverlayStatus | Standard ActionBar, Standard Window Border | 仅显式 OverlayTitle 或现状热区 | 按现状保留 | EdgeTools 或显式关闭按钮 | 可按现状允许透明命中/非命中 | 按现状保留 | 不进入任务栏 | owner 维持现状 | 人工 |
| EphemeralOverlay | OverlayStatus | None | TitleBar, Toolbar, ActionBar | 无 | 禁用 | 外部时序或显式关闭 | 非命中 | 非激活 | 不进入任务栏 | owner/锚定维持现状 | 人工 |

各 shell 都必须明确其槽位是必选、可选还是禁用，不允许在规划阶段模糊处理。

各 shell 的 chrome 契约如下：

- TitleBar 是否承担拖拽热区必须明确
- Resize hit-test 是否可用必须明确
- 关闭按钮是否存在、是否复用系统语义必须明确
- Transparent hit-test 是否参与输入必须明确
- Topmost、ShowInTaskbar、ShowActivated 等窗口级行为必须在 shell 验收时复核

统一壳层结构：

- 标题栏
- 标题与副标题区域
- 关闭按钮
- 内容区边距
- 可滚动内容区
- 底部按钮栏
- 分组卡片区
- 空状态面板

目标：

- 轻对话框壳用于设置窗、关于窗、选择窗、单项配置窗
- 管理窗壳用于资源浏览、名单、诊断等较大型工作窗
- 悬浮壳用于工具条、调色板、浮球等轻量浮窗
- 工作窗壳用于点名/计时等高频主工作区
- 全屏壳用于覆盖式展示、边缘工具和非命中提示子类

### 5.4 SceneTemplate 层

终态保留六类模板：

- 主控窗模板
- 工作窗模板
- 悬浮工具窗模板
- 管理窗模板
- 轻对话框模板
- 全屏展示模板

所有窗口必须归属于其中之一。

### 5.5 实现载体与职责边界

为保证后续计划可拆分，UI 架构单元需映射到具体实现载体：

- `Foundation`
  - 建议拆分为 `Assets/Styles/Foundation/Colors.xaml`、`Typography.xaml`、`Metrics.xaml`、`Effects.xaml`
  - 负责纯令牌，不直接定义业务窗口样式
- `Controls`
  - 建议拆分为 `Assets/Styles/Controls/Buttons.xaml`、`Inputs.xaml`、`Navigation.xaml`、`DataViews.xaml`、`Feedback.xaml`
  - 负责 Button、ToggleButton、TextBox、ComboBox、ListView、TreeView、TabControl、ContextMenu 等通用样式
- `WindowShell`
  - 建议拆分为 `Assets/Styles/Shells/DialogShell.xaml`、`FloatingShell.xaml`、`ManagementShell.xaml`、`WorkShell.xaml`、`FullscreenShell.xaml`
  - 负责标题栏、按钮栏、内容区、信息栏、空状态槽位等外壳结构
- `SceneTemplate`
  - 负责各窗口套用哪个 shell、哪些标准分区、哪些局部变体
  - 不负责行为编排
  - 建议载体为 `Assets/Styles/Scenes/*.xaml` 中的模板约定词典，辅以各窗口 XAML 中的标准区块命名约定
  - 例如 `LauncherScene.xaml`、`WorkScene.xaml`、`FloatingScene.xaml`、`ManagementScene.xaml`、`DialogScene.xaml`、`FullscreenScene.xaml`
- `Windowing`
  - 保留在现有 `src/ClassroomToolkit.App/Windowing` 和各窗口 `.Windowing.cs`、`.Input.cs`、`.State.cs` 中
  - 继续负责 owner、topmost、焦点恢复、输入路由、窗口协调和 DPI/跨屏等行为

要求：

- UI 层只替换视觉资源、结构模板与壳层复用，不接管现有行为协调职责
- `App.xaml` 仍作为全局资源合并入口
- 过渡期允许保留兼容入口词典，但终态资源拆分要可被单独测试和复用
- Shell 级别必须定义拖拽热区、Resize hit-test、关闭语义和输入命中语义，不能只写高层“不变式”
- `SceneTemplate` 既是资源字典层的模板约定，也是窗口级标准分区命名约定，不是纯文档概念

## 6. 窗口分型与终态定义

### 6.1 主控窗模板

对应：

- MainWindow

目标：

- 像控制中枢，而不是普通对话框或小组件
- 极简、稳定、有识别度

结构：

- 细标题/拖拽区
- 两个主功能入口
- 底部次级工具带

### 6.2 工作窗模板

对应：

- RollCallWindow

目标：

- 高可读
- 当前任务信息绝对中心
- 操作路径短

结构：

- 顶部场景工具组
- 中部唯一主内容区
- 底部操作带

### 6.3 悬浮工具窗模板

对应：

- PaintToolbarWindow
- QuickColorPaletteWindow
- BoardColorDialog
- LauncherBubbleWindow

目标：

- 紧凑
- 轻量
- 快速点击
- 不挡内容

### 6.4 管理窗模板

对应：

- ImageManagerWindow
- StudentListDialog
- DiagnosticsDialog

目标：

- 成熟桌面应用感
- 信息架构清晰
- 浏览效率高

### 6.5 轻对话框模板

对应：

- PaintSettingsDialog
- RollCallSettingsDialog
- InkSettingsDialog
- TimerSetDialog
- RemoteKeyDialog
- AutoExitDialog
- ClassSelectDialog
- AboutDialog

目标：

- 统一壳层
- 表单和说明层级明确
- 结构稳定

### 6.6 全屏展示模板

对应：

- PaintOverlayWindow
- PhotoOverlayWindow
- RollCallGroupOverlayWindow

目标：

- 内容优先
- 控件弱存在
- 操作可达但不过分抢眼

说明：

- `PaintOverlayWindow` 与 `PhotoOverlayWindow` 属于全屏展示主类
- `RollCallGroupOverlayWindow` 属于 `EphemeralOverlay` 轻量提示子类，保留“小尺寸、非命中、瞬时状态提示”语义，不按完整全屏工作窗处理

### 6.7 窗口归类总表

| 窗口 | 模板 | Shell | 行为契约 |
|---|---|---|---|
| MainWindow | 主控窗 | FloatingShell | Topmost、非任务栏、手动定位、轻量拖拽 |
| RollCallWindow | 工作窗 | WorkShell | Topmost、可拖拽、可调整、模式切换不改行为语义 |
| PaintToolbarWindow | 悬浮工具窗 | FloatingShell | Topmost、非任务栏、轻量点击、拖拽 |
| QuickColorPaletteWindow | 悬浮工具窗 | FloatingShell | Topmost、非任务栏、临时浮出、不改变输入路由 |
| BoardColorDialog | 悬浮工具窗 | FloatingShell | Topmost、轻量配置、不改变 owner 关系 |
| LauncherBubbleWindow | 悬浮工具窗 | FloatingShell | 非激活、非焦点、轻量唤起 |
| PaintOverlayWindow | 全屏展示 | FullscreenShell | Topmost、透明命中规则不变、内容优先 |
| PhotoOverlayWindow | 全屏展示 | FullscreenShell | Topmost、全屏、关闭与翻页语义不变 |
| RollCallGroupOverlayWindow | 全屏展示（EphemeralOverlay 子类） | EphemeralOverlay | 非命中、小尺寸、瞬时状态提示 |
| ImageManagerWindow | 管理窗 | ManagementShell | 普通可调整窗口、资源浏览语义不变 |
| StudentListDialog | 管理窗 | ManagementShell | 模态/owner 语义维持现状、名单切换逻辑不变 |
| DiagnosticsDialog | 管理窗 | ManagementShell | 诊断阅读与复制语义不变 |
| PaintSettingsDialog | 轻对话框 | DialogShell | owner、按钮语义、Tab 语义不变 |
| RollCallSettingsDialog | 轻对话框 | DialogShell | owner、按钮语义、Tab 语义不变 |
| InkSettingsDialog | 轻对话框 | DialogShell | owner 和确认/取消语义不变 |
| TimerSetDialog | 轻对话框 | DialogShell | owner 和确认/取消语义不变 |
| RemoteKeyDialog | 轻对话框 | DialogShell | owner 和确认/取消语义不变 |
| AutoExitDialog | 轻对话框 | DialogShell | owner 和确认/取消语义不变 |
| ClassSelectDialog | 轻对话框 | DialogShell | owner 和选择语义不变 |
| AboutDialog | 轻对话框 | DialogShell | owner、外链与复制语义不变 |

## 7. 逐窗改造建议

### 7.1 MainWindow

现状问题：

- Hero 磁贴有辨识度但略偏组件化与玩具感
- 上下两段结构语言不完全统一
- 发光与软圆角偏多，稳定感不足

终态方向：

- 改成紧凑控制台
- Hero 按钮缩小并变利落
- 底部工具带更薄更规则
- 图标体系统一
- 退出按钮保留危险感但不抢主功能

最小验收标准：

- 结构固定为拖拽区、主功能区、次级工具带三段
- 两个主功能入口使用同一按钮族与图标尺寸体系
- 底部工具按钮不再出现独立视觉语言
- Topmost、非任务栏、手动定位行为不变

### 7.2 RollCallWindow

现状问题：

- 点名模式、照片区、底部操作区存在轻微抢戏
- 计时模式依赖 neon 感
- 顶部/主体/底部同时较强，画面偏满

终态方向：

- 点名模式进一步强调姓名主视觉
- 照片区退居辅助信息
- 模式切换与班级选择统一为场景工具组
- 底部条重构为更清晰的操作带
- 倒计时模式改成专业计时板，弱化 glow

最小验收标准：

- 顶部、主体、底部形成稳定三段式结构
- 点名模式下姓名始终是第一视觉中心
- 倒计时模式下大数字是第一视觉中心
- owner、topmost、可拖拽、可调整与模式切换行为不变

### 7.3 PaintToolbarWindow

现状问题：

- 功能齐全但秩序感不足
- 分组逻辑还不够强
- 局部尺寸可接受，但整体尚未形成终态尺寸体系

终态方向：

- 改为三段式工具条：模式段、颜色段、操作段
- 统一工具按钮大小
- 颜色按钮更精致，选中态更克制
- 减少多余阴影和装饰

最小验收标准：

- 工具条明确分为模式段、颜色段、操作段
- 全部工具按钮统一高度与图标尺寸
- Toggle 和普通按钮反馈属于同一控件家族
- 拖拽、Topmost 和输入工具语义不变

### 7.4 PaintOverlayWindow / PhotoOverlayWindow / RollCallGroupOverlayWindow

现状问题：

- 边缘控件稍显拼接感
- 操作控件存在重复和不对称
- 全屏场景的“内容优先”原则尚未完全贯彻

终态方向：

- 工具整合为统一边缘工具组
- 顶部标题区轻量化
- 关闭与翻页控件统一语言
- 状态提示层更薄、更系统化
- 群组浮层更像即时标签而非独立小窗

最小验收标准：

- 全屏主类窗口只保留一套边缘操作语言
- 标题区、边缘工具、状态提示三者层级清楚
- `RollCallGroupOverlayWindow` 保持小尺寸、非命中、瞬时提示，不被误改成完整控制窗
- 透明命中、翻页、关闭、导出、适宽等行为语义不变

### 7.5 PaintSettingsDialog

现状问题：

- 参数量大，存在堆叠感
- Tab 内层级仍不够稳定
- 预设、自定义、推荐、高级等概念竞争注意力

终态方向：

- 作为设置窗模板样板
- Tab 内再用分组卡片组织
- 组头包含标题、说明、状态与局部重置
- 高级项只允许在不改变默认信息显露层级的前提下做视觉弱化，不在本次 spec 内强制改成默认折叠

最小验收标准：

- Tab 内部以分组卡片组织，而非连续字段堆叠
- 状态提示与局部重置归入统一 section header
- 主/次按钮、标题栏、滚动区使用 DialogShell 标准结构
- 原有设置项语义、确认/取消语义不变

### 7.6 RollCallSettingsDialog

现状问题：

- 模块划分合理，但视觉仍像传统表单
- 各页结构相似，辨识度一般

终态方向：

- 保留显示/语音/遥控/倒计时分区
- 每页改成设置组卡片
- 每组增加一句用途说明
- 顶部状态与重置并入统一 section header

最小验收标准：

- 四个 Tab 使用统一分组卡片结构
- 每页存在明确的状态区与局部重置区
- 输入控件、滑杆、下拉框全部使用统一控件家族
- 设置项语义与确认/取消行为不变

### 7.7 InkSettingsDialog / TimerSetDialog / RemoteKeyDialog / AutoExitDialog / ClassSelectDialog

现状问题：

- 语言基本接近，但壳层、边距、按钮区还不够完全统一

终态方向：

- 收敛为同一轻对话框模板
- 标题栏高度、关闭按钮尺寸、内容区边距、底部按钮栏全部统一
- TimerSetDialog 作为快速设定弹窗重点打磨
- ClassSelectDialog 更像快速选择器
- RemoteKeyDialog 更像预设+自定义配置卡

最小验收标准：

- 标题栏高度、关闭按钮尺寸、内容区边距、底部按钮栏全部统一
- 至少一个单项配置弹窗和一个选择弹窗可证明模板复用
- 对话框不引入新的行为语义
- owner 与模态语义不变

### 7.8 ImageManagerWindow

现状问题：

- 已可用，但仍像功能拼接的资源管理工具
- 左右结构合理，层级表达不足
- 工具栏、路径栏、内容区的产品感不够成熟

终态方向：

- 做成资源浏览工作台
- 左侧统一成导航侧栏
- 右侧顶部拆分为路径层、工具层、状态层
- 缩略图卡片、列表模式、徽标体系统一化
- 空状态、未选择目录状态、加载状态统一成信息面板

边界：

- 不改变目录导航语义
- 不改变文件打开、双击、前进/后退/向上、显示笔迹等命令语义
- 不改变 PDF/图片的业务状态机和加载策略

最小验收标准：

- 右侧固定为路径层、工具层、状态层、内容层四段
- 左侧导航区三块结构使用统一侧栏语言
- 缩略图卡片和列表模式使用同一数据展示家族
- 目录导航与内容打开行为不变

### 7.9 StudentListDialog

现状问题：

- 已有设计感，但仍偏展示卡片排布
- 状态表达还能更稳

终态方向：

- 调整为名册工作台
- 学生卡更薄、更规整
- 已点/未点状态同时用颜色与轻背景差异表达
- 底部说明区改为更专业的信息栏

边界：

- 不改变点击切换状态语义
- 不改变名单数据来源和点名状态逻辑

最小验收标准：

- 名单区与底部信息栏分层明确
- 学生卡尺寸、文本层级、状态表达统一
- 已点/未点状态既能快速识别，又不引入夸张装饰
- 点击切换状态语义不变

### 7.10 AboutDialog

现状问题：

- 已有基础品牌表达，但还不像正式 About 页

终态方向：

- 改成小型品牌信息窗
- 顶部应用标识、名称、版本分层清楚
- 信息项更整齐
- 外链区更规整

最小验收标准：

- 顶部应用标识、名称、版本三层关系明确
- 信息项和外链区采用统一信息展示样式
- 复制信息与关闭使用 DialogShell 标准按钮栏
- 外链和复制语义不变

### 7.11 DiagnosticsDialog

现状问题：

- 功能够用，但视觉偏工具页

终态方向：

- 改成小型系统报告窗
- 摘要区更明确
- Tab 更像报告页签
- 文本区统一报告阅读风格

边界：

- 不改变诊断项目、建议文本生成和复制行为语义

最小验收标准：

- 摘要区、报告页签、文本区三层关系明确
- 文本阅读区使用统一管理窗/报告样式
- 复制与关闭按钮使用标准按钮栏
- 诊断内容与复制行为语义不变

## 8. 实施顺序

实施采用“兼容别名 + 渐进替换”策略，而不是一次性全局替换。

### Phase 1：Foundation

- 重做颜色令牌
- 重做字体/字号/字重令牌
- 重做圆角/边框/阴影/间距/图标尺寸令牌

Artifacts：

- Foundation 词典拆分方案
- 令牌命名规范
- 兼容映射清单

Definition of Done：

- 新令牌可独立合并加载
- 旧资源键仍可通过兼容映射继续工作
- 不出现全局文本颜色或控件边距回退

Verification：

- 应用启动无资源解析错误
- 主窗口与至少一个对话框可在不改布局的情况下正确吃到新基础令牌

### Phase 2：Controls

- 统一按钮家族
- 统一输入家族
- 统一 Tab/List/Tree/Menu/Tooltip 家族

Artifacts：

- Controls 词典拆分
- 样式家族清单
- 旧样式到新样式映射表

Definition of Done：

- 主/次/危险/图标按钮样式统一
- 输入控件、Tab、列表、树、菜单样式统一
- 不需要在窗口内重复定义同类基础样式

Verification：

- 关键窗口控件状态正常
- Hover/Pressed/Disabled/Selected 反馈统一
- 无明显渲染性能下降

### Phase 3：WindowShell

- 提炼轻对话框壳
- 提炼管理窗壳
- 提炼悬浮窗壳
- 提炼工作窗壳
- 提炼全屏壳

Artifacts：

- 各 shell 模板
- 标题栏、底部按钮栏、空状态槽位规范

Definition of Done：

- 至少一个轻对话框、一个管理窗、一个悬浮窗完成 shell 落地
- 标题栏、按钮栏、边距、信息栏不再各窗各写

Verification：

- 拖拽、关闭、owner、topmost、任务栏显示行为未变
- DPI/跨屏下壳层未产生裁切或定位异常

### Phase 4：主场景窗口

- MainWindow
- RollCallWindow
- PaintToolbarWindow
- PaintOverlayWindow
- PhotoOverlayWindow

Artifacts：

- 主场景窗口重构稿
- 各窗口模板对照表

Definition of Done：

- 主控、工作、悬浮、全屏四类高频场景都至少有一个完成体
- 图标、按钮、色彩、间距和阴影表现统一

Verification：

- 常用课堂流程可完整跑通
- 无新增卡顿、焦点错乱、透明命中异常

### Phase 5：管理与设置窗口

- ImageManagerWindow
- PaintSettingsDialog
- RollCallSettingsDialog

Artifacts：

- 管理窗模板完成体
- 设置窗模板完成体

Definition of Done：

- 管理窗与复杂设置窗都完成结构重整
- 分组卡片、状态区、按钮栏具备统一模板

Verification：

- 图片/PDF 管理流程无回归
- 设置项可读性提升，原有设置行为未变

### Phase 6：轻对话框与收尾窗口

- TimerSetDialog
- InkSettingsDialog
- RemoteKeyDialog
- AutoExitDialog
- ClassSelectDialog
- AboutDialog
- StudentListDialog
- DiagnosticsDialog
- QuickColorPaletteWindow
- BoardColorDialog
- RollCallGroupOverlayWindow
- LauncherBubbleWindow

Artifacts：

- 轻对话框与尾部窗口统一收口
- 视觉差异清单清零

Definition of Done：

- 所有在范围内窗口都完成模板归类
- 无残留明显旧风格孤岛窗口

Verification：

- 关键窗口人工巡检完成
- 所有窗口在常用分辨率下表现一致

### 8.1 迁移兼容策略

- `App.xaml` 继续作为资源总入口
- 过渡期保留旧 `Colors.xaml`、`WidgetStyles.xaml` 作为兼容入口或 facade
- `Icons.xaml` 保持独立合并项，不并入 Foundation；其归属定义为共享图标资源层，在迁移期继续独立加载
- 新词典先并入，再按窗口逐步切换到新样式键
- 允许阶段性混用，但必须满足：单个窗口内部不同时混用两套冲突按钮/输入/标题栏语言
- 兼容别名只用于迁移期，最终需收敛为新令牌与新样式入口
- 资源合并顺序固定为：Foundation -> Icons -> Controls -> Shells -> Scenes -> Compatibility Facade -> Window-local overrides
- 同名 key 优先级以最后合并的 facade/override 为准，但 facade 只允许转发旧键，不允许重新定义终态令牌
- 兼容别名退出条件：当范围内窗口全部切换到新 key，且无旧 key 引用后，才允许删除 facade

### 8.2 必测窗口协同场景

以下协同场景必须进入后续计划和回归验证：

| 场景 | 对应 Phase | 验证方式 |
|---|---|---|
| `LauncherBubbleWindow -> MainWindow` 的唤起、焦点、非激活浮窗行为 | Phase 4 / 6 | 人工 |
| `MainWindow -> PaintToolbarWindow -> PaintOverlayWindow` 的 owner、topmost、拖拽与输入协同 | Phase 4 | 人工 + 现有相关单测回归 |
| `RollCallWindow -> StudentListDialog / ClassSelectDialog / RollCallGroupOverlayWindow` 的 owner、模态、状态提示协同 | Phase 4 / 6 | 人工 |
| `PaintOverlayWindow -> ImageManagerWindow -> PhotoOverlayWindow` 的资源浏览、全屏展示、焦点返回协同 | Phase 4 / 5 | 人工 |
| 透明命中与非命中窗口的输入路由协同 | Phase 3 / 4 | 人工 + 现有相关单测回归 |
| 多窗口 Topmost 修复与失焦恢复协同 | Phase 3 / 4 | 人工 + 现有相关单测回归 |
| PerMonitorV2、4K + 投影、跨屏移动下的 DPI 与定位协同 | Phase 3 / 4 / 5 | 人工 |
| Tab 顺序、默认/取消按钮、ESC/Enter、焦点可视态、非激活浮窗键盘行为 | Phase 3 / 4 / 5 / 6 | 人工 |

## 9. 性能保护策略

### 9.1 渲染策略

- 控件状态反馈以底色、描边、透明度、轻位移为主
- 不新增实时毛玻璃
- 不新增大面积透明层叠
- 不新增大规模彩色 glow
- DropShadowEffect 只保留窗口级或少量关键容器级

### 9.2 全屏与内容窗口策略

- 全屏窗口优先内容可读性与操作清晰度
- 图片/PDF 内容区不承担装饰任务
- 边缘操作条静态化、轻量化
- 不使用复杂进入或切换动画

### 9.3 数据密集窗口策略

- TreeView/ListView/WrapPanel 场景避免复杂模板特效
- 缩略图卡片重点优化结构与边框，不依赖特效
- 列表项状态变化优先使用颜色与边框，而不是阴影动画

## 10. 风险与对策

### 风险 1：重构中途风格混乱

对策：

- 先完成 Foundation 与 Controls，再动主窗口
- 不在旧样式上无限打补丁
- 使用新样式族逐步替换旧引用

### 风险 2：透明窗口性能回退

对策：

- 控制透明层数量
- 收缩阴影和特效数量
- 全屏场景优先静态视觉

### 风险 3：设置窗复杂度高，容易越改越乱

对策：

- 先用 PaintSettingsDialog 打样
- 成熟后平移到 RollCallSettingsDialog 与其他对话框

### 风险 4：管理窗只换皮不换结构

对策：

- ImageManagerWindow 单独做信息架构重整
- 路径层、工具层、状态层、内容层必须明确分区

## 11. 验证建议

设计实施后建议至少做以下验证：

- 主窗口、点名窗口、工具条在常用分辨率下的紧凑度和可读性
- 4K + 投影/跨屏场景下的窗口层次和字体可读性
- 全屏图片/PDF 场景下的控件可达性与内容清晰度
- 缩略图、树、列表在大数据量下的流畅性
- 长时间课堂使用下的视觉疲劳感

## 12. 结论

本设计选择“令牌化主题 + 统一窗口壳层 + 场景模板化”的路线，将 ClassroomToolkit 的 UI 从“多轮演进下的局部美化”升级为“可长期维护的终态窗口体系”。

该路线的关键不是个别窗口做得多漂亮，而是：

- 所有窗口属于同一产品
- 所有常用控件属于同一系统
- 所有视觉决策服从同一套令牌与性能约束
- 后续新增窗口不再重新发明样式

只有在此基础上，项目才称得上达到 UI/窗口体系的“终态最佳架构”。

## 13. 实施主计划

本设计按“体系重构”落地，而不是按“分散美化”落地。执行顺序固定为：

1. Phase 1：设计令牌重构
2. Phase 2：壳层模板定型
3. Phase 3：核心业务窗口重构
4. Phase 4：设置窗口 IA 重构
5. Phase 5：管理与浏览窗口重构
6. Phase 6：轻浮层与瞬时弹层收口
7. Phase 7：全项目一致性清理
8. Phase 8：终态验收与冻结

### 13.1 Phase 1：设计令牌重构

目标：建立终态 UI 的唯一事实来源。

输出：

- 颜色、Brush、阴影、圆角、尺寸、间距、字体令牌
- 主/次/危险按钮族
- 图标按钮与切换按钮族
- 输入、菜单、卡片、状态徽标等基础样式

完成标准：

- 新窗口可直接复用公共层实现 80% 视觉
- 不再需要窗口内新增基础按钮样式
- 主强调与教学强调已经彻底分离

### 13.2 Phase 2：5 类壳层模板定型

目标：先决定窗口属于谁，再决定窗口长什么样。

壳层固定为：

- `WorkShell`
- `OverlayShell`
- `ManagementShell`
- `DialogShell`
- `BubbleShell`

完成标准：

- 任意窗口都能无歧义归类
- 标题栏、动作区、底栏、内容容器不再各写各的
- 后续新增窗口必须先选择壳层，再进入实现

### 13.3 Phase 3：P0 核心业务窗口重构

优先窗口：

- `MainWindow`
- `RollCallWindow`
- `PaintToolbarWindow`
- `PaintOverlayWindow`
- `PhotoOverlayWindow`
- `PaintSettingsDialog`

目标：

- 先解决用户第一眼和高频使用感受
- 收拢主次关系
- 压缩过满装饰
- 建立终态主场景语言

### 13.4 Phase 4：设置窗口 IA 重构

优先窗口：

- `RollCallSettingsDialog`
- `InkSettingsDialog`
- `RemoteKeyDialog`
- `TimerSetDialog`
- `AutoExitDialog`
- `ClassSelectDialog`

目标：

- 基础项前置
- 高级项后置
- 页头状态统一
- 输入反馈统一

### 13.5 Phase 5：管理与浏览窗口重构

优先窗口：

- `ImageManagerWindow`
- `StudentListDialog`
- `DiagnosticsDialog`
- `AboutDialog`

目标：

- 强化 Header / ContextBar / MainContent / FooterStatus 的固定层次
- 让管理窗更像工作台，而不是主题化弹窗
- 让状态表达不再只依赖颜色

### 13.6 Phase 6：轻浮层与瞬时弹层收口

优先窗口：

- `QuickColorPaletteWindow`
- `BoardColorDialog`
- `LauncherBubbleWindow`
- `RollCallGroupOverlayWindow`

目标：

- 统一 BubbleShell 与轻 OverlayShell 交互
- 秒开秒关、低干扰、低成本
- 禁止尺寸抖动式 hover

### 13.7 Phase 7：全项目一致性清理

清理目标：

- 硬编码颜色
- 硬编码阴影
- 局部重复字体声明
- 漂移尺寸与圆角
- 本可上收的局部样式
- 非统一风格图标

完成标准：

- 公共样式成为唯一视觉事实来源
- 窗口局部资源仅保留业务特例

### 13.8 Phase 8：终态验收与冻结

验收维度：

- 视觉一致性
- 信息架构
- 交互清晰度
- 紧凑与可用平衡
- 性能与稳定性

冻结内容：

- 设计令牌
- 壳层模板
- 控件规范
- 变更准入规则

## 14. 全窗口终态映射总表

### 14.1 核心业务窗口

| 窗口 | 壳层 | 终态角色 | 主焦点 | 次焦点 | 教学强调 | 优先级 |
|---|---|---|---|---|---|---|
| `MainWindow` | `WorkShell` | 课堂启动台 | 画笔、点名两个主入口 | 设置、关于、退出 | 禁用 | P0 |
| `RollCallWindow` | `WorkShell` | 课堂主展示面板 | 姓名卡或计时数字 | 模式切换、照片区、底栏动作 | 允许 | P0 |
| `PaintToolbarWindow` | `WorkShell` 浮动工具变体 | 专业浮动工具条 | 当前工具模式 | 颜色、撤销、资源、设置 | 少量允许 | P0 |
| `PaintOverlayWindow` | `OverlayShell` | 低干扰书写覆盖层 | 内容与书写 | 单主侧控制区、临时提示 | 仅白板场景允许 | P0 |
| `PhotoOverlayWindow` | `OverlayShell` | 极简暗场展示层 | 图片内容 | 关闭入口、姓名标签 | 禁用 | P0 |
| `LauncherBubbleWindow` | `BubbleShell` | 轻量常驻入口 | 单一启动动作 | hover 激活反馈 | 禁用 | P1 |

### 14.2 设置类窗口

| 窗口 | 壳层 | 终态角色 | 主焦点 | 次焦点 | 教学强调 | 优先级 |
|---|---|---|---|---|---|---|
| `PaintSettingsDialog` | `DialogShell` | 书写系统设置中枢 | 基础书写与总预设 | 覆盖项、兼容项、高级项 | 少量允许 | P0 |
| `RollCallSettingsDialog` | `DialogShell` | 点名/计时业务设置窗 | 显示与逻辑基础项 | 高级项、恢复机制 | 禁用 | P1 |
| `InkSettingsDialog` | `DialogShell` | 可靠性与存储设置窗 | 路径、清理策略 | 调试项 | 禁用 | P1 |
| `RemoteKeyDialog` | `DialogShell` | 遥控映射配置窗 | 预设与自定义生效逻辑 | 冲突提示、示例输入 | 禁用 | P2 |
| `TimerSetDialog` | `DialogShell` | 高频快速配置弹窗 | 常用预设、精确时间 | 分钟/秒钟调整方式 | 禁用 | P1 |
| `AutoExitDialog` | `DialogShell` | 单参数配置窗 | 输入合法值 | 规则说明 | 禁用 | P2 |
| `ClassSelectDialog` | `DialogShell` | 快速切换弹窗 | 班级列表 | 当前班级状态、搜索 | 禁用 | P2 |

### 14.3 管理与浏览类窗口

| 窗口 | 壳层 | 终态角色 | 主焦点 | 次焦点 | 教学强调 | 优先级 |
|---|---|---|---|---|---|---|
| `ImageManagerWindow` | `ManagementShell` | 资源浏览工作台 | 当前路径与主内容区 | 导航、视图切换、缩略图控制 | 禁用 | P1 |
| `StudentListDialog` | `ManagementShell` | 班级状态面板 | 学生状态网格 | 图例、统计 | 禁用 | P1 |
| `DiagnosticsDialog` | `ManagementShell` | 诊断工作台 | 推荐动作 | 详细日志 | 禁用 | P2 |
| `AboutDialog` | `ManagementShell` 或轻 `DialogShell` | 品牌与作者信息卡 | 应用信息、作者信息 | 复制信息、链接 | 禁用 | P3 |

### 14.4 瞬时弹层与轻浮层

| 窗口 | 壳层 | 终态角色 | 主焦点 | 次焦点 | 教学强调 | 优先级 |
|---|---|---|---|---|---|---|
| `QuickColorPaletteWindow` | `BubbleShell` | 画笔颜色瞬时选择器 | 颜色项 | 当前选中态 | 允许 | P2 |
| `BoardColorDialog` | `BubbleShell` | 白板背景快速选择 | 背景色项 | 当前背景态 | 允许 | P2 |
| `RollCallGroupOverlayWindow` | 轻 `OverlayShell` | 课堂临时展示层 | 群组展示内容 | 关闭/弱提示 | 允许 | P3 |

### 14.5 全项目统一禁止项

- 窗口内随意写颜色
- 窗口内随意写阴影
- hover 改尺寸
- 多处常驻 glow
- 同一窗口多个主焦点
- 设置窗展示化
- 管理窗教学主题化
- 全屏窗控件压内容
- 状态只靠颜色
- 同类按钮与图标尺寸漂移

## 15. 设计状态与文档边界

本文件自本次确认起作为 `ui-window-system` 的正式设计基线，用于后续实施与验收对照。

说明：

- `docs/validation/ui-window-system-progress.md` 与 `docs/validation/ui-window-system-acceptance.md` 记录的是 repo-local loop 与验收门禁痕迹，不替代本设计文档的终态规范权威性。
- 后续若实施结果与本文件冲突，应优先回到本设计文档复核，再决定是否需要新增 ADR 或设计修订。
