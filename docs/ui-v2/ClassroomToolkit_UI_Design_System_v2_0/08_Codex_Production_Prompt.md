# Codex Production Prompt — ClassroomToolkit UI 2.0

你正在修改一个真实的 Windows WPF 项目：ClassroomToolkit。

目标：按照仓库中的 `docs/ui-v2/`（或用户提供的 Visual Design System 2.0 文件）完成一次系统性的 UI 2.0 改版。

这是生产代码任务，不是效果图任务。

---

## 任务目标

把整个 ClassroomToolkit UI 统一到一套 Design System 中，并支持：

1. `MidnightTeal`：课堂深色（默认）
2. `Blackboard`：黑板护眼
3. `Light`：明亮

同时保持所有现有业务功能、数据格式、窗口层级、触控、PPT/WPS、图片/PDF、画笔、点名和计时行为不变。

---

## 必须先阅读

按顺序阅读：

1. `00_README.md`
2. `01_Visual_Design_System_v2.0.md`
3. `02_Component_Specification_v2.0.md`
4. `03_Theme_Tokens_v2.json`
5. `05_Theme_Architecture_and_Switching.md`
6. `06_UI_Refactor_Implementation_Plan.md`
7. `07_UI_QA_Acceptance_Checklist.md`
8. 仓库根 README、handover、与 UI/窗口/画笔/Overlay 相关文档

不要只读提示词就开始写代码。

---

## 第一阶段：只审计，不大规模修改

先检查并记录：

- App.xaml / ResourceDictionary
- 现有 Style / ControlTemplate
- Window/UserControl/Popup/ContextMenu 清单
- 硬编码颜色/圆角/字体/字号
- icon 来源
- 特殊 WindowStyle / AllowsTransparency / Topmost / ShowActivated
- 悬浮启动器
- 点名
- 计时
- 画笔/白板
- 图片/PDF
- 文件管理
- PPT/WPS overlay
- 设置与持久化

形成实施清单后再修改。

不要询问用户是否继续，除非发现真正阻断性的歧义或无法安全处理的架构冲突。

---

## 第二阶段：先建立 Design System

必须优先建立：

- Metrics
- Typography
- 3 套 Theme Color Dictionary
- Semantic Brush Dictionary
- Shared Button styles
- IconButton
- Tabs
- Slider
- CheckBox
- ComboBox
- Menus
- Tooltip
- Dialog
- Window Chrome
- Floating Toolbar style
- ThemeManager / Theme switching

### 强制规则

颜色资源使用语义名：

```text
CTK.Brush.Primary
CTK.Brush.Text.Primary
CTK.Brush.Surface
CTK.Brush.Warning
CTK.Brush.Danger
```

禁止：

```text
TealBrush
GreenButtonBrush
OrangeResetBrush
```

Theme-dependent Resource 必须使用 `DynamicResource`。

尺寸、圆角、字体等跨主题不变项优先 `StaticResource`。

---

## 第三阶段：迁移整个项目

推荐迁移顺序：

1. Launcher
2. Random Name Picker
3. Countdown / Stopwatch
4. Floating Annotation Toolbar
5. Pen Settings
6. Roll-call Settings
7. Image/PDF Viewer
8. File/Image Manager
9. Whiteboard
10. PPT/WPS overlay/navigation
11. Remaining dialogs/popups/context menus

要求：

- 不允许每个窗口重新定义自己的 Button Template
- 不允许每个窗口重新定义相同颜色
- Feature XAML 主要负责布局与语义 style
- 公共组件统一从 Design System 获取样式

---

## 视觉要求

### 默认主题

`MidnightTeal`

视觉方向：

- 深石墨/蓝黑
- 青绿 Primary
- 琥珀 Warning/Reset
- 红色 Danger
- 克制，不做 Gaming/RGB
- 少量渐变可以，但不得成为控件识别的必要条件
- 禁止持续 Glow

### 点名

姓名占最大视觉权重。

不要让背景图案、粒子、渐变抢姓名。

### Timer

大数字优先。

装饰环极简，Stroke ≤ 4 DIP。

### Toolbar

- 约 52 DIP 高
- Tool hit target ≥ 40
- Active = Primary icon + PrimarySoft background
- 低频工具溢出到 More
- 白/黑/绿色背景均需清楚

### Dialog

减少 Card 套 Card。

用 section、spacing、divider 建立层级。

---

## 主题切换

在现有设置体系中加入：

```text
课堂深色（推荐）
黑板护眼
明亮
```

要求：

- 默认 MidnightTeal
- 即时切换
- 持久化
- 不重启
- 不重新初始化点名/计时
- 不影响 Topmost/Overlay/PPT-WPS 状态
- 不另造一套危险的配置写入机制

---

## 绝对禁止

1. 不引入新的第三方 UI Framework，除非仓库已经使用并且有明确理由
2. 不引入字体文件
3. 不为了阴影/玻璃效果破坏透明窗口性能
4. 不改变 students.xlsx / student_photos / settings.ini 数据兼容
5. 不改变点名算法
6. 不改变计时业务语义
7. 不改变画笔撤销/历史行为
8. 不改变 PPT/WPS hook 行为
9. 不重写无关模块
10. 不删除/弱化测试来让 CI 变绿
11. 不覆盖用户当前未提交的无关修改
12. 不使用 Emoji 代替正式 icon

---

## 兼容性重点

重点保护：

- Windows 10/11
- 100/125/150/200% DPI
- 触屏
- 数位笔
- 多显示器
- Topmost
- 点击穿透
- First-frame z-order
- WPS/PPT
- 图片/PDF
- 学生照片悬浮层

---

## 验证

完成后至少执行：

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate!=CoreContract&Gate!=Performance"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate=CoreContract"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
```

根据改动范围追加 focused tests / full gates。

运行：

```powershell
git diff --check
```

扫描残留硬编码：

- `#RRGGBB`
- `Brushes.`
- 本地重复 Style/Template

合理例外必须说明。

---

## 最终输出格式

完成后给出：

### 1. Summary
完成了什么。

### 2. Design System
新增/修改哪些资源文件。

### 3. Themes
三套主题的实现与切换路径。

### 4. Migrated UI
逐项列出已迁移窗口/控件。

### 5. Compatibility
说明哪些行为明确保持不变。

### 6. Validation
列出 build/test 命令和结果。

### 7. Residual Issues
仍未迁移的硬编码、视觉风险、现场验收事项。

### 8. Diff
`git diff --stat`

不要声称已经完成真实投影、真实多屏或课堂现场验收，除非实际做过。
