# 20260504 code optimization diagnostics responsiveness

## Scope

- Rules: R1, R2, R4, R6, R8; project C.2 fixed gate order.
- Risk: low. Changes are limited to diagnostics responsiveness, converter no-op robustness, redundant diagnostic cleanup, and contract tests.
- Boundary: no persisted data format changes; no `students.xlsx`, `student_photos/`, `settings.ini`, or settings JSON schema changes.

## Plan and task list

1. Baseline and audit.
   - Establish clean working tree and current gate baseline.
   - Static scan for UI blocking patterns, empty catches, aggressive GC, console output, TODO/FIXME/HACK, and dependency/analyzer status.
2. Low-risk fixes in this slice.
   - Move manual compatibility diagnostics collection off the UI click path; keep dialog creation on the dispatcher.
   - Remove redundant `BorderBrushDiagnostic` wrapper and duplicate diagnostics dialog BorderBrush pass.
   - Align `MultiplyConverter.ConvertBack` with other one-way converters by returning `Binding.DoNothing`.
   - Remove stale `GC.Collect` comment from photo bitmap loading.
   - Tighten diagnostics probe blocking-safety contract so it verifies bounded `WaitAsync(timeout)` despite line breaks.
3. Verification.
   - Run affected focused tests.
   - Run full build/test/contract/hotspot gate sequence.
   - Run analyzer and dependency vulnerability checks.

## Evidence

- Baseline: `git status --short --branch` returned `## main...origin/main`.
- Baseline build: `dotnet build ClassroomToolkit.sln -c Debug` passed with 0 warnings and 0 errors.
- Baseline test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed: 3482 passed, 0 failed.
- Static review:
  - Production code scan found no `Thread.Sleep`, empty `catch`, `Console.WriteLine`, `GC.Collect`, `TODO`, `FIXME`, or `HACK` after cleanup.
  - Remaining `Task.Result` usages are in `RollCallViewModel.Data.cs` behind completed-task checks and were not changed in this slice.
- Focused verification:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindowDiagnosticsEntryContractTests|FullyQualifiedName~SystemDiagnosticsCopyContractTests|FullyQualifiedName~MainWindowStartupDiagnosticsDispatchContractTests|FullyQualifiedName~BorderFixHelperDeferredDispatchFallbackContractTests"` passed: 7 passed, 0 failed.
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerConvertersTests|FullyQualifiedName~MainWindowDiagnosticsEntryContractTests"` passed: 8 passed, 0 failed.
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PresentationDiagnosticsProbeBlockingSafetyContractTests|FullyQualifiedName~ImageManagerConvertersTests|FullyQualifiedName~MainWindowDiagnosticsEntryContractTests"` passed: 9 passed, 0 failed.
- Additional quality checks:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1` passed.
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-dependency-vulnerabilities.ps1` passed: no vulnerable packages detected.
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-analyzer-backlog-baseline.ps1 -Configuration Debug` passed: total=0.
- Final hard gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed with 0 warnings and 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed: 3484 passed, 0 failed.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` passed: 29 passed, 0 failed.
  - hotspot: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1` passed.

## Rollback

Restore the changed files:

```powershell
git restore -- src/ClassroomToolkit.App/MainWindow.Launcher.cs src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml.cs src/ClassroomToolkit.App/Photos/ImageManagerWindow.Converters.cs src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs tests/ClassroomToolkit.Tests/MainWindowDiagnosticsEntryContractTests.cs tests/ClassroomToolkit.Tests/ImageManagerConvertersTests.cs tests/ClassroomToolkit.Tests/PresentationDiagnosticsProbeBlockingSafetyContractTests.cs docs/change-evidence/20260504-code-optimization-diagnostics-responsiveness.md
git restore --source=HEAD -- src/ClassroomToolkit.App/Diagnostics/BorderBrushDiagnostic.cs
```

## Follow-up backlog

- Large but budget-compliant files remain high-value future review targets: `PaintOverlayWindow.Ink.Rendering.cs`, `PaintToolbarWindow.xaml.cs`, `VariableWidthBrushRenderer.cs`, `ImageManagerWindow.Navigation.cs`, and `StartupCompatibilityProbe.cs`.
- The two `RollCallViewModel.Data.cs` `Task.Result` reads are currently guarded by completed-task checks; a future refactor can centralize that pattern if the project wants a stricter text-level no-`.Result` policy.
- Live touch/DPI/projector validation remains outside this automated code gate and should be handled before release claims about classroom-device acceptance.
