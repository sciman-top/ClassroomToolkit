# 2026-04-27 UI visual refresh evidence

## Scope
- Rule IDs: R1, R2, R6, R8, E4
- Risk: medium
- Boundary: WPF XAML appearance, shared theme/style tokens, UI copy contracts
- Landing: `src/ClassroomToolkit.App`
- Target: compact, cohesive, touch-aware visual terminal state without new runtime dependencies or heavy animation

## Changes
- Refined shared color, gradient, radius, spacing, shell, button, tab, expander, list, and title-icon tokens.
- Updated main launcher, roll call/timer, paint toolbar/settings, image/PDF manager, student list, diagnostics, startup warning, about, class select, remote key, ink, and small palette dialogs.
- Synchronized source-string UI contracts with the new concise copy and style-token surface.
- Repaired the host NuGet restore blocker by setting explicit NuGet cache/scratch process defaults and disabling solution restore graph parallelism for this repository.

## Commands
- `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: 0
  - key_output: `已成功生成。0 个警告 0 个错误`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: 0
  - key_output: `已通过! - 失败: 0，通过: 3466，已跳过: 0，总计: 3466`
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: 0
  - key_output: `已通过! - 失败: 0，通过: 28，已跳过: 0，总计: 28`
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code: 0
  - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`
- stability note: the first post-fix full-test run hit two `BrushPerformanceGuardTests` ratio failures; immediate rerun of the same command passed 3466/3466.
- `git diff --check`
  - exit_code: 0
  - key_output: no whitespace errors; line-ending warnings only

## Hotspot review
- Performance: no new runtime dependency; no new per-frame effects; reused frozen brushes/effects and existing XAML templates.
- Touch: preserved existing touch target token floor for icon controls and toolbar compact hit-area strategy.
- Interop/windowing: no changes to Win32/COM/WPS/UIAutomation code paths.
- Compatibility: no changes to `students.xlsx`, `student_photos/`, `settings.ini`, persistence formats, or external behavior.
- Restore repair: `CommonApplicationData` is empty in this host process, so NuGet machine-wide settings can throw `path1`; explicit NuGet paths plus serial solution restore avoid the SDK restore crash.
- Remaining acceptance: live touch-device visual inspection is still recommended before release.

## Rollback
- Revert the modified XAML/style/test-contract files from this change.
- Remove `Directory.Solution.props` and revert `scripts/env/Initialize-WindowsProcessEnvironment.ps1` NuGet defaults if the host .NET/NuGet environment is permanently repaired upstream.
- No migration or data rollback required.
