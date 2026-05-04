# 20260504 code optimization diagnostics responsiveness

## Scope

- Rules: R1, R2, R4, R6, R8; project C.2 fixed gate order.
- Risk: low. Changes are limited to diagnostics responsiveness, converter no-op robustness, redundant diagnostic cleanup, callback-path simplification, startup probe race hardening, source-contract helper deduplication, and contract tests.
- Boundary: no persisted data format changes; no `students.xlsx`, `student_photos/`, `settings.ini`, or settings JSON schema changes.

## Plan and task list

Completed slice 1:

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

Completed slice 2:

4. RollCall preload blocking-safety cleanup.
   - Replace direct completed-task `.Result` reads with a named helper that only reads `IsCompletedSuccessfully` preload tasks.
   - Extend the production blocking-wait contract to reject `.Result` in addition to `.Wait(` and same-line `.GetAwaiter().GetResult()`.
   - Add a RollCall-specific source contract that keeps preload code free of `.Result`, `.Wait(`, and same-line `.GetAwaiter().GetResult()`.

Completed slice 3:

5. Manual diagnostics duplicate-run guard.
   - Add an in-flight gate for manual diagnostics clicks so repeated taps do not launch concurrent full diagnostics probes.
   - Keep startup diagnostics unchanged; this guard applies only to user-triggered manual diagnostics.
   - Extend diagnostics entry contract tests to lock the gate and release behavior.

Completed slice 4:

6. ImageManager favorites/recents callback centralization.
   - Centralize `FavoritesChanged` and `RecentsChanged` notifications into named safe callback helpers.
   - Preserve existing callback order and `SafeActionExecutionExecutor` exception isolation.
   - Tighten the event callback contract so favorites/recents each have a single invoke entry.

Completed slice 5:

7. Startup compatibility process identity hardening.
   - Centralize volatile `Process.ProcessName` and `Process.Id` reads behind non-fatal guarded helpers.
   - Keep external PPT/WPS process race failures as diagnostic unknowns instead of letting startup compatibility checks throw.
   - Add helper tests and a source contract that keeps volatile process identity reads centralized.

Completed slice 6:

8. Source-contract occurrence helper deduplication.
   - Move duplicate `CountOccurrences` implementations into `ContractSourceAggregateLoader`.
   - Update ImageManager and startup compatibility source-contract tests to use the shared helper.

## Evidence

- Baseline: `git status --short --branch` returned `## main...origin/main`.
- Baseline build: `dotnet build ClassroomToolkit.sln -c Debug` passed with 0 warnings and 0 errors.
- Baseline test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed: 3482 passed, 0 failed.
- Static review:
  - Production code scan found no `Thread.Sleep`, empty `catch`, `Console.WriteLine`, `GC.Collect`, `TODO`, `FIXME`, or `HACK` after cleanup.
  - Slice 2 removed remaining production `.Result` matches from the RollCall preload path.
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
- Slice 2 focused verification:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~RollCallPreloadBlockingSafetyContractTests|FullyQualifiedName~RollCallViewModelPreloadConcurrencyTests|FullyQualifiedName~BlockingWaitUsageContractTests"` passed: 4 passed, 0 failed.
  - `rg -n --glob '*.cs' -g '!**/obj/**' -g '!**/bin/**' "\.Result\b|\.Wait\(|\.GetAwaiter\(\)\.GetResult\(\)" src` returned no production-code matches.
- Slice 3 focused verification:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~MainWindowDiagnosticsEntryContractTests|FullyQualifiedName~MainWindowStartupDiagnosticsDispatchContractTests"` passed: 5 passed, 0 failed.
  - `dotnet build ClassroomToolkit.sln -c Debug` passed with 0 warnings and 0 errors.
- Final hard gate after slice 3:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed with 0 warnings and 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed: 3485 passed, 0 failed.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` passed: 29 passed, 0 failed.
  - hotspot: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1` passed.
- Slice 4 focused verification:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerEventCallbackSafetyContractTests"` passed: 1 passed, 0 failed.
- Final hard gate after slice 4:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed with 0 warnings and 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed: 3485 passed, 0 failed.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` passed: 29 passed, 0 failed.
  - hotspot: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1` passed.
- Slice 5 focused verification:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StartupCompatibilityProbeTests"` passed: 13 passed, 0 failed.
- Final hard gate after slice 5:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed with 0 warnings and 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed: 3487 passed, 0 failed.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` passed: 29 passed, 0 failed.
  - hotspot: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1` passed.
- Slice 6 focused verification:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerEventCallbackSafetyContractTests|FullyQualifiedName~StartupCompatibilityProbeTests"` passed: 14 passed, 0 failed.
- Final hard gate after slice 6:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed with 0 warnings and 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed: 3487 passed, 0 failed.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` passed: 29 passed, 0 failed.
  - hotspot: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1` passed.

## Rollback

Restore the changed files:

```powershell
git restore -- src/ClassroomToolkit.App/MainWindow.xaml.cs src/ClassroomToolkit.App/MainWindow.Launcher.cs src/ClassroomToolkit.App/Diagnostics/DiagnosticsDialog.xaml.cs src/ClassroomToolkit.App/Photos/ImageManagerWindow.Converters.cs src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Data.cs src/ClassroomToolkit.Services/Compatibility/StartupCompatibilityProbe.cs tests/ClassroomToolkit.Tests/ContractSourceAggregateLoader.cs tests/ClassroomToolkit.Tests/MainWindowDiagnosticsEntryContractTests.cs tests/ClassroomToolkit.Tests/ImageManagerConvertersTests.cs tests/ClassroomToolkit.Tests/ImageManagerEventCallbackSafetyContractTests.cs tests/ClassroomToolkit.Tests/PresentationDiagnosticsProbeBlockingSafetyContractTests.cs tests/ClassroomToolkit.Tests/BlockingWaitUsageContractTests.cs tests/ClassroomToolkit.Tests/RollCallPreloadBlockingSafetyContractTests.cs tests/ClassroomToolkit.Tests/StartupCompatibilityProbeTests.cs docs/change-evidence/20260504-code-optimization-diagnostics-responsiveness.md
git restore --source=HEAD -- src/ClassroomToolkit.App/Diagnostics/BorderBrushDiagnostic.cs
```

## Follow-up backlog

- Large but budget-compliant files remain high-value future review targets: `PaintOverlayWindow.Ink.Rendering.cs`, `PaintToolbarWindow.xaml.cs`, `VariableWidthBrushRenderer.cs`, `ImageManagerWindow.Navigation.cs`, and `StartupCompatibilityProbe.cs`.
- Live touch/DPI/projector validation remains outside this automated code gate and should be handled before release claims about classroom-device acceptance.
