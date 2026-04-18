# 2026-04-18 PPT Cursor Drag Focus Guard

- rule_id: `R1 R2 R3 R6 R8`
- risk_level: `low`
- current_boundary: `PowerPoint 全屏放映 + 光标模式 + 浮动工具窗口拖拽`
- current_landing: `src/ClassroomToolkit.App/Paint/PresentationFocusRestorePolicy.cs`
- target_destination: `拖拽期间禁止恢复 PPT 放映焦点，避免辅助窗口拖拽提交被打断`

## Basis

- 用户现象：PowerPoint 全屏放映时切到工具条光标模式，拖动工具条、点名窗口、启动器时，按住左键能跟随鼠标，松手后回到原位置；画笔模式正常；WPS 光标/画笔模式正常。
- 根因判断：PowerPoint 光标模式会运行 `RestorePresentationFocusIfNeeded` 焦点归还链路；现有置顶修复已避让 `WindowDragOperationState`，但焦点归还未避让，拖拽末端可能把前台抢回放映窗口，导致拖拽提交失败。
- 第二轮收敛：用户复测后确认只剩 `工具条` 回弹，`点名窗口/启动器` 已恢复正常；因此根因继续收敛到 `PaintToolbarWindow` 自身拖拽实现，而不是全局浮层焦点链路。

## Changes

- `src/ClassroomToolkit.App/Paint/PresentationFocusRestorePolicy.cs`
  - 新增 `dragOperationActive` 守卫，拖拽活跃时直接拒绝恢复放映焦点。
- `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs`
  - 将 `WindowDragOperationState.IsActive` 接入 `PresentationFocusRestorePolicy.CanRestore(...)`。
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
  - 工具条拖拽入口从 `OnToolbarDrag` 切换为 `OnToolbarDragStart`。
- `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - 移除 `DragMove()` 依赖。
  - 增加 `CaptureMouse + MouseMove + MouseLeftButtonUp` 自管拖拽路径。
  - 拖拽期间持有 `WindowDragOperationState`，并对虚拟屏幕范围做基础夹取。
- `tests/ClassroomToolkit.Tests/PresentationFocusRestorePolicyTests.cs`
  - 补充“拖拽活跃时不可恢复焦点”回归测试。
  - 补充与 `WindowDragOperationState` 集成的测试。
- `tests/ClassroomToolkit.Tests/PaintToolbarDragModeContractTests.cs`
  - 固化“工具条不再依赖 `DragMove()`，而使用鼠标捕获拖拽流”。

## Commands

- `codex --version`
  - `exit_code=0`
  - `key_output=codex-cli 0.121.0`
- `codex --help`
  - `exit_code=0`
- `codex status`
  - `exit_code=1`
  - `key_output=Error: stdin is not a terminal`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationFocusRestorePolicyTests"`
  - `exit_code=1`
  - `key_output=默认 bin/obj 被运行中的 sciman Classroom Toolkit 与 Visual Studio 锁定，无法复制输出`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationFocusRestorePolicyTests" --artifacts-path D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify`
  - `exit_code=0`
  - `key_output=7 passed`
- `dotnet build ClassroomToolkit.sln -c Debug --artifacts-path D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify`
  - `exit_code=0`
  - `key_output=0 warnings, 0 errors`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintToolbarDragModeContractTests|FullyQualifiedName~PresentationFocusRestorePolicyTests" --artifacts-path D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify`
  - `exit_code=0`
  - `key_output=8 passed`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --artifacts-path D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify`
  - `exit_code=1`
  - `key_output=BrushDpiGoldenRegressionTests 缺少基线文件 D:\OneDrive\CODE\ClassroomToolkit\artifacts\Baselines\brush-dpi-golden.json`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests" --artifacts-path D:\OneDrive\CODE\ClassroomToolkit\artifacts\verify`
  - `exit_code=0`
  - `key_output=28 passed`

## Verification

- build: `passed`
- targeted regression tests: `passed`
- contract/invariant: `passed`
- hotspot review: `passed`
  - 复核点 1：守卫只影响 `RestorePresentationFocusIfNeeded`，不改变拖拽、坐标写回、置顶修复实现。
  - 复核点 2：工具条拖拽已改为本地鼠标捕获实现，不再依赖 `DragMove()` 的系统提交行为。
  - 复核点 3：仅在 `WindowDragOperationState.IsActive` 时拦截放映焦点恢复，画笔模式、WPS 路径、非拖拽态 PowerPoint 光标模式保持原行为。
  - 复核点 4：测试覆盖了策略层、运行时状态接线与工具条拖拽契约。

## N/A / Blockers

- `platform_na`
  - reason: `codex status` 在当前非交互 shell 下失败：`stdin is not a terminal`
  - alternative_verification: `codex --version` 与 `codex --help`
  - evidence_link: `docs/change-evidence/20260418-ppt-cursor-drag-focus-guard.md`
  - expires_at: `2026-04-25`
- `gate_na`
  - reason: 无
  - alternative_verification: 无
  - evidence_link: `docs/change-evidence/20260418-ppt-cursor-drag-focus-guard.md`
  - expires_at: `N/A`

## Full-Test Blocker

- 非本次改动引入的现有失败：
  - `BrushDpiGoldenRegressionTests.DpiGoldenHashes_ShouldMatchBaseline`
  - 缺少基线文件：`D:\OneDrive\CODE\ClassroomToolkit\artifacts\Baselines\brush-dpi-golden.json`
  - 测试提示：设置 `CTOOLKIT_UPDATE_DPI_GOLDEN=1` 可再生基线。

## Rollback

- 回滚文件：
  - `src/ClassroomToolkit.App/Paint/PresentationFocusRestorePolicy.cs`
  - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Presentation.cs`
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml`
  - `src/ClassroomToolkit.App/Paint/PaintToolbarWindow.xaml.cs`
  - `tests/ClassroomToolkit.Tests/PresentationFocusRestorePolicyTests.cs`
  - `tests/ClassroomToolkit.Tests/PaintToolbarDragModeContractTests.cs`
- 回滚方式：
  - 还原上述三个文件到变更前版本。
  - 重新执行 `dotnet build`、目标测试和 contract/invariant。
