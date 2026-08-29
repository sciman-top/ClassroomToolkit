# UI 2.0 改版执行记录与交付报告（Phase 0 / 8 / 9 补录）

**日期**: 2026-08-29
**范围**: `docs/ui-v2/ClassroomToolkit_UI_Design_System_v2_0/` 规范驱动的全项目 UI 改版
**性质**: 补录文档。首轮执行缺少本记录，本文件由独立复审 + 修复切片后补齐；标注 `[repo]` 的结论有仓库内可复现证据，标注 `[pending-manual]` 的项尚无现场证据。

---

## Phase 0 — UI 审计清单（补录）

首轮执行时未留档，以下为复审时基于 diff `4545e98` 与仓库现状确认的审计事实：

- **ResourceDictionary**：改版前 `App.xaml` 合并 `Assets/Styles/{Colors,Icons,WidgetStyles}.xaml`；改版后合并 `UI/Themes/{Metrics,Typography,ThemeResources}.xaml` + `Assets/Styles/{LegacyAliases,Icons,WidgetStyles}.xaml` + `UI/Styles/Components.xaml`。
- **Theme 字典**：`UI/Themes/Colors.{MidnightTeal,Blackboard,Light}.xaml` 三套，键集与 `03_Theme_Tokens_v2.json` 一致，另加 `SelectionOverlay/SelectionOverlaySoft/Shadow` 三个扩展键。
- **特殊窗口**：`PaintOverlayWindow`、`PhotoOverlayWindow`、`RegionSelectionOverlayWindow`、`LauncherBubbleWindow`、`PaintToolbarWindow` 等涉及 Topmost/AllowsTransparency/点击穿透；本轮 diff 未触及这些 Win32 行为属性。
- **自绘 TitleBar**：各窗口 `Style_DialogShell*` / `Style_WorkShellTitleBar`；标题栏高度统一到 `CTK.Size.TitleBar`(46)/`CTK.GridLength.TitleBar`。
- **icon 来源**：`Assets/Styles/Icons.xaml` 统一 Geometry，无 Emoji 混入。
- **设置持久化**：`settings.ini`（AppSettingsService，`[UI] theme`，缺失/未知值回退 MidnightTeal）。
- **改版前硬编码**：功能 XAML 中 HEX 颜色在改版后清零；旧 `Colors.xaml`（100+ 处 HEX）于 2026-08-29 修复切片中退役删除。

## 各阶段执行状态

| 阶段 | 状态 | 证据 |
|---|---|---|
| Phase 0 审计 | 完成（补录） | 本文件；git diff 4545e98 |
| Phase 1 Design System | 完成 | `UI/Themes/*`、`UI/Styles/Components.xaml`；ThemeContractTests |
| Phase 2 公共组件 | 基本完成 | Button/IconButton/ToolButton 为完整共享模板；ComboBox 下拉圆角 8、Slider thumb 14、Tab active=PrimarySoft+Primary 已按 02 规范收口（6352f8d） |
| Phase 3 核心窗口 | 完成 | 全部 24 个窗口/对话框 XAML 已迁移到 DynamicResource + 语义键；工具栏命中区 40、高度 52 |
| Phase 4 主题切换 | 完成（2026-08-29 修复） | `AppearanceDialog`（启动器调色板入口），选择即切换并持久化，不再耦合 AutoExitDialog（29bb135） |
| Phase 5 硬编码清理 | 完成 | 功能 XAML HEX=0；保留例外均有注释（Consolas 等宽、WindowChrome/Footer 0 圆角） |
| Phase 6 兼容性验证 | `[repo]` 部分 | settings.ini 新键兼容有测试；students.xlsx/student_photos 代码路径零改动。运行时行为见 Phase 8 |
| Phase 7 构建与测试 | 完成 | 见下方门禁记录 |
| Phase 8 人工视觉验收 | `[pending-manual]` | 见下矩阵 |
| Phase 9 交付报告 | 完成（补录） | 本文件 |

## Phase 8 — 人工视觉验收矩阵 `[pending-manual]`

以下维度当前只有 repo 侧静态证据（XAML 结构、DynamicResource 契约、三主题键集一致性测试），**没有**真实运行证据。课堂使用前必须人工过一遍：

| 维度 | 覆盖项 | 状态 |
|---|---|---|
| DPI | 100% / 125% / 150% / 200% | `[pending-manual]` |
| 分辨率 | 1366×768 / 1920×1080 / 2560×1440 / 4K | `[pending-manual]` |
| 主题 | MidnightTeal / Blackboard / Light | 三主题切换有契约测试 `[repo]`；实际渲染 `[pending-manual]` |
| 背景 | 白色 PPT / 深色 PPT / 图片 / PDF / 黑板 / 白板 / 桌面 | `[pending-manual]`（重点：Light 主题工具栏在白色 PPT 上的边界） |
| 输入 | 鼠标 / 触控 / 笔 | `[pending-manual]`（工具栏命中区已达 40 DIP `[repo]`） |
| 动画×主题 | 主题切换时 LauncherBubble/MainWindow 的 ColorAnimation(DynamicResource) 并行行为 | `[pending-manual]` |

## Phase 9 — 最终交付报告

### Design System 文件
- `src/ClassroomToolkit.App/UI/Themes/`：Metrics / Typography / ThemeResources / SemanticBrushes / Colors.{MidnightTeal,Blackboard,Light} / ThemeManager.cs / ThemePreferenceService.cs / AppTheme.cs
- `src/ClassroomToolkit.App/UI/Styles/Components.xaml`：共享组件样式与 legacy 样式别名
- `src/ClassroomToolkit.App/Assets/Styles/LegacyAliases.xaml`：legacy 资源键 → CTK token 兼容缝

### 三套主题
MidnightTeal（默认）/ Blackboard / Light；运行时切换、不重启、不重建业务窗口；持久化于 `settings.ini [UI] theme`。

### 已迁移窗口
全部 24 个窗口/对话框 XAML（改动于 4545e98）：MainWindow、LauncherBubbleWindow、RollCallWindow、PaintToolbar/Settings/Overlay/BoardColor/QuickColorPalette/RegionSelection、ImageManagerWindow、PhotoOverlayWindow、RollCallGroupOverlayWindow、About/AutoExit/Appearance/ClassSelect/RemoteKey/StudentList/TimerSet/RollCallSettings/InkSettings/Diagnostics/StartupCompatibilityWarning。

### 尚存 UI hardcode（均为规范允许例外，已注释）
- `FontFamily="Consolas"`：Diagnostics 两对话框 ×2 处（技术等宽明细）
- `CornerRadius="0"`：PaintSettingsDialog WindowChrome、ImageManagerWindow 页脚（Win32/贴边约束）
- C# 内 `new SolidColorBrush`/`Brushes.*`：画笔墨迹、板色对比文字等业务色（RULE 9 允许）

### legacy 键迁移清单（技术债，不影响主题切换）
兼容缝 `LegacyAliases.xaml` 当前承接以下功能文件的 legacy 引用（2026-08-29 统计，`{Static|Dynamic}Resource Brush_*` 与 `Style_*Button*` 别名出现次数）：

| 文件 | 引用数 |
|---|---:|
| Paint/PaintSettingsDialog.xaml | 76 |
| Photos/ImageManagerWindow.xaml | 44 |
| RollCallSettingsDialog.xaml | 39 |
| RollCallWindow.xaml | 28 |
| Paint/PaintToolbarWindow.xaml | 21 |
| Paint/PaintOverlayWindow.xaml | 19 |
| AboutDialog.xaml | 16 |
| StudentListDialog.xaml | 10 |
| Paint/QuickColorPaletteWindow.xaml | 8 |
| Diagnostics 两对话框 | 各 8 |
| Ink/InkSettingsDialog.xaml | 6 |
| TimerSetDialog / RemoteKeyDialog / BoardColorDialog / MainWindow | 各 4 |
| PhotoOverlayWindow / ClassSelectDialog / AutoExitDialog | 各 3 |
| RegionSelectionOverlayWindow / LauncherBubbleWindow / AppearanceDialog | 各 2 |
| RollCallGroupOverlayWindow | 1 |

收敛策略：新代码一律使用 `CTK.*`；存量按文件在低风险切片中逐步替换，契约由 `LegacyAliasSeamTests` 把关（缝断裂即测试红）。

### 门禁记录（2026-08-29 修复收口）
- `dotnet build -c Debug`：0 警告 0 错误
- standard tests：3034/3034 通过；CoreContract：29/29 通过
- hotspot 线行预算：PASS；`git diff --check`（工作树与 `origin/main...HEAD` 提交范围）：通过

### 可能风险
1. Phase 8 全部人工维度未验证（见上矩阵），不可声称投影/多屏/触控现场可用。
2. 约半数窗口的视觉效果来自 legacy 键的间接映射；`LegacyAliasSeamTests` 防回归，但直接迁移前视觉等价性靠缝契约保证。
3. `ColorAnimation To="{DynamicResource ...}"`（LauncherBubble/MainWindow 悬停动画）在主题切换瞬间的行为未实测。

## 相关修复提交（2026-08-29）
- `f1d3e2b` chore: 移除已跟踪的课堂运行时文件并补 ignore 规则
- `29bb135` feat: 主题入口迁移至独立外观设置对话框并支持即时切换
- `6352f8d` refactor: 收口 Design System 残留与 Colors.xaml 退役
