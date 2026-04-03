# 2026-04-03 UI visual endstate polish

- rule_id: R1/R2/R4/R6/R8
- risk_level: medium
- active_rule_path: E:/CODE/ClassroomToolkit/AGENTS.md
- current_landing: `src/ClassroomToolkit.App/Assets/Styles/*.xaml` + major window XAML
- target_destination: shared theme tokens and shell styles first, window-level XAML second
- migration_batch: UI visual endstate polish

## Basis
- 用户要求将主窗口、点名/倒计时、工具条、PDF/图片管理、关于、设置与轻量弹窗统一到“最佳实践终态”，同时保持紧凑、美观、低性能开销。
- 现状存在共享主题已建立但窗口之间密度、标题栏、按钮、提示文案和局部样式岛不统一的问题。

## Changes
- 收口共享视觉基础：`Colors.xaml`、`WidgetStyles.xaml`
  - 统一深色石板底色、边框与强调色。
  - 下调 glow/shadow 强度，减少渲染压力。
  - 统一图标按钮、动作按钮、工作壳层与色板尺寸。
- 重做高频窗口外观：`MainWindow.xaml`、`RollCallWindow.xaml`、`PaintToolbarWindow.xaml`
  - 主窗口改为更清晰的标题层级和紧凑 Hero 入口。
  - 点名/倒计时窗口统一顶部动作区、名字卡片、底部操作条密度。
  - 工具条改为更紧凑的胶囊结构，统一图形菜单与颜色泡泡。
- 统一对话框/管理窗：`AboutDialog.xaml`、`TimerSetDialog.xaml`、`AutoExitDialog.xaml`、`ClassSelectDialog.xaml`、`RemoteKeyDialog.xaml`、`InkSettingsDialog.xaml`、`BoardColorDialog.xaml`、`QuickColorPaletteWindow.xaml`、`Photos/ImageManagerWindow.xaml`
  - 精简标题、按钮和说明文案。
  - 统一壳层、圆角、边框和按钮密度。
- 修复异常契约：`Diagnostics/StartupCompatibilityWarningDialog.xaml.cs`
  - 为两个 `catch (Exception)` 补充 `when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))` 过滤，满足异常处理契约测试。

## Commands
- `codex status`
- `codex --version`
- `codex --help`
- `Get-Command dotnet`
- `Get-Command powershell`
- `Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
- `dotnet build ClassroomToolkit.sln -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`

## Verification Evidence
- build: passed, 0 warning / 0 error
- full test: passed, 3169 passed / 0 failed
- contract/invariant subset: passed, 25 passed / 0 failed
- hotspot: passed, `status=PASS`

## platform_na
- type: `platform_na`
- command: `codex status`
- reason: non-interactive terminal returned `stdin is not a terminal`
- alternative_verification:
  - `codex --version` => `codex-cli 0.118.0`
  - `codex --help` returned normal CLI command list
  - active rule path recorded manually as `E:/CODE/ClassroomToolkit/AGENTS.md`
- evidence_link: `docs/change-evidence/20260403-ui-visual-endstate-polish.md`
- expires_at: `2026-04-10`

## Rollback
- revert theme tokens: `src/ClassroomToolkit.App/Assets/Styles/Colors.xaml`, `src/ClassroomToolkit.App/Assets/Styles/WidgetStyles.xaml`
- revert work windows: `src/ClassroomToolkit.App/MainWindow.xaml`, `src/ClassroomToolkit.App/RollCallWindow.xaml`, `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
- revert dialog/manager windows: changed XAML files listed above
- revert exception contract fix: `src/ClassroomToolkit.App/Diagnostics/StartupCompatibilityWarningDialog.xaml.cs`
