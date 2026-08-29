# zcode / GLM-5.3-Flash Production Prompt — ClassroomToolkit UI 2.0

任务：对 ClassroomToolkit 的 WPF UI 做统一改版。

你必须严格按阶段执行，不允许边看边随意改。

---

# RULE 0：先读规范

必须先完整阅读：

- 00_README.md
- 01_Visual_Design_System_v2.0.md
- 02_Component_Specification_v2.0.md
- 03_Theme_Tokens_v2.json
- 05_Theme_Architecture_and_Switching.md
- 06_UI_Refactor_Implementation_Plan.md
- 07_UI_QA_Acceptance_Checklist.md
- 仓库 README / handover

如果规范与现有实现冲突，以“保持业务行为和兼容性”为第一优先，并在最终报告说明。

---

# RULE 1：第一步只做 UI 审计

先找出：

1. ResourceDictionary
2. Styles
3. Templates
4. Windows
5. UserControls
6. Popups
7. ContextMenus
8. hardcoded colors
9. hardcoded CornerRadius
10. hardcoded font/font size
11. icon source
12. Topmost/transparent/click-through windows

先建立清单，再开始系统性修改。

---

# RULE 2：先做公共 Design System

必须先建立统一资源。

主题：

```text
MidnightTeal = 默认
Blackboard
Light
```

统一语义资源：

```text
CTK.Brush.Window
CTK.Brush.Surface
CTK.Brush.SurfaceAlt
CTK.Brush.Text.Primary
CTK.Brush.Text.Secondary
CTK.Brush.Primary
CTK.Brush.Warning
CTK.Brush.Danger
CTK.Brush.Border.Default
CTK.Brush.Border.Focus
```

不要使用：

```text
TealBrush
GreenBrush
OrangeBrush
```

颜色必须可被主题替换。

---

# RULE 3：禁止窗口各自造样式

所有重复控件必须共用：

- Button
- IconButton
- Tab
- Slider
- CheckBox
- ComboBox
- Menu
- ContextMenu
- Tooltip
- Dialog
- WindowChrome
- FloatingToolbar

如果发现已有重复模板，逐步收敛到公共样式，不要复制一份新模板。

---

# RULE 4：视觉方向

默认主题：

```text
深石墨/蓝黑背景
青绿 Primary
琥珀 = Reset/Warning
红色 = Danger
```

必须克制。

不要：

- RGB
- 强霓虹
- 大量 Glow
- 复杂粒子
- 过度渐变
- 大量玻璃模糊
- 超大圆角
- 手机 App 风格

---

# RULE 5：核心页面

## Random Name Picker

视觉顺序：

```text
姓名 > 学号 > 开始/停止 > 分组 > 名单/重置 > 设置
```

## Countdown

视觉顺序：

```text
时间 > 开始/暂停 > 重置 > 设置
```

Timer 装饰环必须简化。

## Floating Toolbar

- 高约 52 DIP
- Tool hit target >= 40
- Active = Primary + PrimarySoft
- 不持续发光
- 白/黑/绿背景都要清楚

## Dialog

减少 Card 套 Card。

---

# RULE 6：主题切换

设置中加入：

```text
课堂深色（推荐）
黑板护眼
明亮
```

要求：

- MidnightTeal 默认
- 即时切换
- 持久化
- 不重启
- 不打断计时/点名
- 不改变 Overlay/Topmost/PPT/WPS 状态

---

# RULE 7：绝对不要改业务

禁止改：

- students.xlsx 格式
- student_photos
- settings.ini 兼容
- 点名算法
- Timer 逻辑
- 画笔撤销历史
- PDF 业务
- PPT/WPS hook
- 窗口层级逻辑
- 配置安全写入策略

只改 UI 与必要的 Theme infrastructure。

---

# RULE 8：每完成一个阶段立即编译

至少：

```powershell
dotnet build ClassroomToolkit.sln -c Debug
```

大阶段后运行相关 tests。

不要积累几十个文件错误后最后才编译。

---

# RULE 9：最终扫描

搜索：

```text
#RRGGBB
Brushes.
SolidColorBrush
CornerRadius=
FontFamily=
```

将无理由硬编码迁移到 Design System。

用户选择的画笔颜色等业务颜色可保留。

---

# RULE 10：最终验证

执行：

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate!=CoreContract&Gate!=Performance"
dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "Gate=CoreContract"
powershell -File scripts/quality/check-hotspot-line-budgets.ps1
git diff --check
```

---

# FINAL REPORT

最终只需清楚报告：

1. 新增 Design System 文件
2. 三套 Theme
3. 已迁移窗口
4. 仍存在的 UI hardcode
5. Build/tests 结果
6. 可能风险
7. 需要人工视觉验收的位置
8. git diff --stat

不要把“代码测试通过”表述为“投影/触控/多屏已经现场验证”。
