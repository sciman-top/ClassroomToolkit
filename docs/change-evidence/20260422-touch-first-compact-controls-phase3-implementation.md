# 20260422 Touch First Compact Controls Phase 3 Implementation

- date: 2026-04-22
- scope: task8 low-risk slice + task7 image-manager low-risk slice + task9 regression coverage
- risk_level: medium
- active_rule_path: D:\CODE\ClassroomToolkit\AGENTS.md
- current_boundary: dialog and management window touch-density cleanup without changing toolbar button count or free-drag rules
- target_destination: unify compact visual sizing and touchable action bands for TimerSetDialog, RollCallWindow, and ImageManagerWindow

## Basis
- user constraints: toolbar/button count unchanged unless justified; free drag stays; compact visual size allowed; transparent or effective hit targets should remain touch friendly; same-area buttons should stay consistent.
- repository facts: TimerSetDialog minute steppers used compact secondary buttons with effective 32x30 targets; RollCall bottom action bar mixed 30/32/44/64 sizing; ImageManager top action bands mixed icon/text/toggle button heights and widths.

## Changes
1. TimerSetDialog added local compact-touch styles:
   - `Style_TimerCompactStepperButton`
   - `Style_TimerValueTextBox`
   - `Style_TimerPresetButton`
   - minute steppers moved to icon-button template with touch-safe minimums via shared style.
   - minute/second text boxes and presets now use unified touch-sized controls.
2. RollCallWindow added local bottom-bar sizing styles:
   - `Style_RollCallBottomBarTextButton`
   - `Style_RollCallBottomBarAccentButton`
   - removed raw `MinWidth=44` group chip sizing and unified bottom action buttons to one compact band.
3. ImageManagerWindow added local toolbar sizing styles:
   - `Style_ImageManagerToolbarButton`
   - `Style_ImageManagerToolbarDangerButton`
   - `Style_ImageManagerToolbarToggleButton`
   - `Style_ImageManagerToolbarIconButton`
   - title-bar actions, favorites/recent action strip, navigation buttons, view toggles, and selection-mode buttons now share the same touch-height band.
4. Regression coverage extended:
   - `DialogTouchFlowContractTests` now asserts timer/roll-call unified touch styles.
   - `ImageManagerTouchFlowContractTests` now asserts toolbar unified touch styles.

## Commands
1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~DialogTouchFlowContractTests"`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerTouchFlowContractTests|FullyQualifiedName~ManagementWindowsXamlContractTests"`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
5. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
6. `git diff -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs src/ClassroomToolkit.App/TimerSetDialog.xaml src/ClassroomToolkit.App/RollCallWindow.xaml tests/ClassroomToolkit.Tests/App/DialogTouchFlowContractTests.cs`

## Key Output
- build: passed
- targeted dialog contracts: passed (4/4)
- targeted management contracts: passed (6/6)
- full test: passed (3397/3397)
- contract/invariant: passed (28/28)
- hotspot review: passed for the touched XAML/test files; no button-count increase introduced; free-drag behavior unchanged.

## Rollback
1. revert `src/ClassroomToolkit.App/TimerSetDialog.xaml`
2. revert `src/ClassroomToolkit.App/RollCallWindow.xaml`
3. revert `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml`
4. revert `tests/ClassroomToolkit.Tests/App/DialogTouchFlowContractTests.cs`
5. revert `tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs`

## Remaining Gap
- task7 deep performance item remains open: thumbnail grid still uses `WrapPanel + visible-range scheduling`; true virtualization or layout replacement has not been landed in this phase.
