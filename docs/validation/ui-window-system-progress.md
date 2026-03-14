# UI Window System Progress

- 日期：2026-03-14
- 当前阶段：visual-regression
- 当前状态：`window-shell`、`main-scenes`、`management-and-settings`、`dialog-tail`、`visual-regression` 自动任务已完成。
- 范围：仅处理 repo-local loop plumbing，不在本轮修改 generic `autonomous-execution-loop` family/bootstrap。

## Notes

- Generic `autonomous-execution-loop` family bootstrap changes remain external/system-owned follow-up and are not part of this repo-local implementation pass.
- `docs/ui-refactor/tasks.json` has been expanded from stage placeholders to executable child slices for `controls`, `window-shell`, and `main-scenes`, with gate dependencies preserved.
- `scripts/refactor/test-ui-mode-smoke.ps1` default 场景已改为按当前 state/task 动态断言首个可执行任务，避免硬编码初始任务导致的假失败。
- Foundation 与 controls 阶段已完成。
- `ui-window-shell-work-main-rollcall-shell` 与 `ui-window-shell-fullscreen-overlay-shell` 已完成并通过任务验证。
- 任务图归一：父任务 `ui-window-shell-work-and-fullscreen-shell` 已按子任务闭环结果回填为 completed（由 deferred 归并收口）。
- 本轮补强：`Colors.xaml`/`WidgetStyles.xaml` 增补 Stage A 语义令牌契约，`MainWindow.xaml` 与 `RollCallWindow.xaml` 完成令牌消费与共享阴影替换，新增 XAML 契约测试覆盖主壳层与点名计时壳层。
- 本轮补强（Overlay）：`PaintOverlayWindow.xaml` 与 `PhotoOverlayWindow.xaml` 去除内联 `DropShadowEffect`，统一改为共享阴影资源并补齐遮罩/表面语义令牌引用，新增 Overlay XAML 契约测试。
- 本轮补强（管理/关于）：`AboutDialog`、`StudentListDialog`、`ImageManagerWindow`、`DiagnosticsDialog` 将 `Brush_Background_L2/L3` 收口到 `Brush_Surface_Secondary`/`Brush_InputBackground`，并去除 About 内联阴影；新增管理窗口 XAML 契约测试。
- 本轮补强（设置家族）：`PaintSettingsDialog` 将遗留 `Brush_Background_L2/L3` 收口到 `Brush_Surface_Secondary`/`Brush_Surface_Primary`，并新增设置窗口 XAML 契约测试，锁定“无 legacy 背景键、无内联阴影”。
- 本轮补强（对话框尾部）：`TimerSetDialog` 的分钟步进按钮移除局部 `Button.Template`，改为复用共享按钮样式，新增 `TimerSetDialog` XAML 契约测试锁定该规范。
- 本轮补强（密度规则）：`WidgetStyles.xaml` 新增按钮/图标尺寸密度令牌，并将 `IconButton`、`Dialog/Overlay close button`、`Dialog/Management action button` 样式改为统一引用；新增密度契约测试防止尺寸回退为分散硬编码。
- 本轮补强（图标字形）：新增 `Size_Icon_Glyph_XS/SM/MD/LG` 字形尺寸令牌，并在 `About/RollCallSettings/PaintSettings/RollCall/PaintOverlay` 的关闭图标与 Overlay 翻页箭头中落地引用；新增字形 token 使用契约测试。
- 本轮补强（间距/圆角）：`WidgetStyles.xaml` 新增壳层 spacing/radius 令牌，并将 `Dialog/Management/Work/Overlay` 核心 shell 样式切到令牌引用，新增 spacing/radius 契约测试锁定壳层密度规则。
- 本轮补强（颜色硬编码清零）：核心窗口（Main/RollCall/About/TimerSet/RollCallSettings/PaintSettings/PaintOverlay/PhotoOverlay/ImageManager/StudentList/Diagnostics）已清零 `#RRGGBB/#AARRGGBB` 字面量，统一改为语义 brush/color 资源；新增“核心窗口禁止 hex 字面量”契约测试。
- 本轮补强（字体层级）：`WidgetStyles.xaml` 新增 `FontSize_Body_S/M` 与 `FontSize_Title_Dialog/Management` 字体层级 token，并让按钮族/对话框标题/管理窗口标题与说明文本统一消费 token；新增字体层级契约测试。
- 本轮补强（动效时长）：`WidgetStyles.xaml` 新增 `Motion_Duration_Fast` 动效时长 token，并将焦点下划线动画时长从硬编码改为 token 引用；新增动效时长契约测试。
- 本轮补强（交互透明度）：`WidgetStyles.xaml` 新增 hover/pressed/disabled/overlay opacity token，并将按钮族与 Overlay 壳层中的关键透明度值改为 token 引用；新增透明度契约测试。
- 本轮补强（边框粗细）：`WidgetStyles.xaml` 新增 `BorderThickness_Regular/Emphasis` token，并将输入控件、按钮族与壳层核心样式中的 `BorderThickness` 关键值改为 token 引用；新增边框粗细契约测试。
- 本轮补强（壳层尺寸）：`WidgetStyles.xaml` 新增 `Dialog/Management/Work` 标题栏高度 token 与 close-button 间距 token，并将对应 shell 样式改为 token 引用；新增壳层尺寸契约测试。
- `ui-main-scenes-main-window`、`ui-main-scenes-rollcall-and-timer`、`ui-main-scenes-toolbar-and-overlay` 已完成并通过任务验证。
- `ui-management-image-and-list`、`ui-management-settings-dialog-family`、`ui-dialog-tail-pass` 已完成并通过任务验证。
- `theme-freeze`、`main-scene-freeze`、`fullscreen-float-freeze` 门禁已恢复并继续自动执行。
- 当前选择器状态：`done`（All tasks are completed）。
