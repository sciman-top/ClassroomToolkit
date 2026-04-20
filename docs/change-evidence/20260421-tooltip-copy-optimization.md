# 2026-04-21 Tooltip Copy Optimization

## Scope
- 边界：仅优化画笔工具栏中快捷颜色、图形按钮、快捷颜色弹窗色块的 Tooltip 文案。
- 当前落点：
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - `src/ClassroomToolkit.App/Paint/QuickColorPaletteWindow.xaml.cs`
  - `tests/ClassroomToolkit.Tests/PaintToolbarTouchSettingsContractTests.cs`
- 目标归宿：Tooltip 显示当前状态与操作方式，不改变点击、长按、弹窗和绘制行为。

## Rule Mapping
- R1：已声明边界、当前落点、目标归宿。
- R2：先定位文案，再小步修改 XAML/CS/契约测试，并逐项验证。
- R3：根因是 Tooltip 缺少当前颜色/当前形状上下文；直接修复文案来源，无止血补丁。
- R4：风险等级低；仅 UI 文案与契约测试，未改业务数据格式和外部依赖。
- R5：未引入新抽象层，仅添加局部显示名 helper。
- R6：按 build -> test -> contract/invariant -> hotspot 执行。
- R7：未改变 `students.xlsx`、`student_photos/`、`settings.ini` 格式语义。
- R8：本文件记录依据 -> 命令 -> 证据 -> 回滚。

## Commands
- `codex --version`
  - exit_code=0
  - key_output=`codex-cli 0.121.0`
- `codex --help`
  - exit_code=0
  - key_output=`Codex CLI`
- `codex status`
  - platform_na
  - reason=`stdin is not a terminal`
  - alternative_verification=`codex --version` + `codex --help` + 当前项目 AGENTS.md 已由会话注入
  - evidence_link=`docs/change-evidence/20260421-tooltip-copy-optimization.md`
  - expires_at=`2026-05-21`
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code=0
  - key_output=`已成功生成。0 个警告 0 个错误`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code=0
  - key_output=`已通过! - 失败: 0，通过: 3383，已跳过: 0，总计: 3383`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code=0
  - key_output=`已通过! - 失败: 0，通过: 28，已跳过: 0，总计: 28`

## Hotspot Review
- 快捷颜色按钮：初始 Tooltip 从泛化“点按切换”改为“当前颜色 + 点按使用 + 再点/长按换色”；运行时 `SetQuickColorSlot` 会同步当前颜色名或十六进制值。
- 图形按钮：初始和运行时 Tooltip 均显示当前形状；形状切换后由 `UpdateShapeButtonIcon` 同步图标与文案。
- 快捷颜色弹窗：色块 Tooltip 从单字颜色名改为“选择黑色/红色/...”。
- 契约测试：`PaintToolbarTouchSettingsContractTests` 已同步覆盖新文案与动态 helper。

## Rollback
- 回滚 `PaintToolbarWindow.xaml` 中快捷颜色与图形 Tooltip 文案。
- 回滚 `PaintToolbarWindow.xaml.cs` 中 `GetQuickColorDisplayName`、`GetShapeDisplayName` 及对应 ToolTip 赋值。
- 回滚 `QuickColorPaletteWindow.xaml.cs` 中颜色显示名与 `选择{option.Name}` Tooltip。
- 回滚 `PaintToolbarTouchSettingsContractTests.cs` 中本次文案断言。
- 回滚后重跑 build -> test -> contract/invariant -> hotspot。
