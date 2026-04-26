# 2026-04-26 Systematic Review Optimization

## Scope

- Boundary: ClassroomToolkit repository only.
- Current landing: low-risk hardening in ink storage and validation diagnostics.
- Target home: preserve existing UI behavior, public contracts, data formats, and compatibility while improving path safety, IO failure tolerance, diagnostics, and test coverage.
- Risk level: low.

## Changes

1. `src/ClassroomToolkit.App/Ink/InkStorageService.cs`
   - Reject `.` and `..` document names by falling back to `unknown`, preventing dot-segment document names from resolving outside the intended date/document folder.
   - Make `LoadPage` return `null` for non-fatal path, IO, or access failures instead of allowing storage-edge exceptions to bubble into UI-adjacent flows.
   - Keep `ListPages` loading other pages when an individual sidecar read fails with a non-fatal exception.
   - Use `Directory.EnumerateDirectories(rootPath, "*", TopLevelIgnoreInaccessibleOptions)` during old-record cleanup to match the rest of the ink listing behavior.
2. `scripts/validation/collect-pilot-metrics.ps1`
   - Fix the GC counter path typo from `"\ .NET CLR Memory(...)"` to `"\.NET CLR Memory(...)"`.
3. Tests
   - Added dot-segment and invalid-root regressions in `InkStorageServiceTests`.
   - Added `PilotMetricsScriptContractTests` to lock the corrected GC counter path.
4. `src/ClassroomToolkit.App/Ink/InkPersistenceService.cs`
   - Preserve source filenames containing `.ink.json` by stripping only the final sidecar suffix instead of using broad string replacement.
   - Pre-validate composed sidecar paths with `Path.GetFullPath` so invalid source paths are rejected before file writes.
   - Make public read/write/delete APIs no-op or return empty results for invalid source paths rather than surfacing storage-edge exceptions.
   - Broaden sidecar read failure handling to non-fatal exceptions and keep diagnostics.
5. `scripts/validation/collect-pilot-metrics.ps1`
   - Treat `LogRoot`, log files, and generated report paths as literal paths for read/write operations.
6. Additional tests
   - Added `.ink.json` filename preservation and invalid source path regressions in `InkPersistenceServiceTests`.
   - Extended `PilotMetricsScriptContractTests` to lock literal path handling.

## Platform Diagnostics

- `codex --version`
  - exit: 0
  - key output: `codex-cli 0.125.0`
- `codex --help`
  - exit: 0
  - key output: command list printed.
- `codex status`
  - result: `platform_na`
  - reason: non-interactive execution returned `Error: stdin is not a terminal`.
  - alternative_verification: active project rule path was supplied in the current task context as `D:\CODE\ClassroomToolkit\AGENTS.md`; repository gates below were run directly.
  - evidence_link: this file.
  - expires_at: next interactive Codex session or next rule-loading investigation.
- `dotnet --info`
  - result: partial `platform_na`
  - reason: printed SDK/runtime information, then failed in workload info with `TypeInitializationException` / `NullReferenceException` from `InstallerBase`.
  - alternative_verification: `scripts/env/Initialize-WindowsProcessEnvironment.ps1` was dot-sourced for repo commands; `dotnet build` and `dotnet test` completed successfully.
  - evidence_link: this file.
  - expires_at: next host environment repair.

## Baseline Evidence

- `dotnet build ClassroomToolkit.sln -c Debug`
  - pass: 0 warnings, 0 errors, elapsed `00:00:04.72`.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - pass: 3457 passed, 0 failed, 0 skipped.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - pass: 28 passed, 0 failed, 0 skipped.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - pass: `[hotspot] PASS - all .cs files within line budget (max=1200)`.
- `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive --source https://api.nuget.org/v3/index.json`
  - pass: no vulnerable packages detected for all projects.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validation/collect-settings-load-performance-samples.ps1 -Configuration Debug -ColdIterations 3 -HotIterations 20 -OutputRoot artifacts\validation\current-review-baseline`
  - pass.
  - small sample: 4249 bytes, cold avg 10.5599 ms, cold p95 30.6795 ms, hot avg 0.2803 ms, hot p95 0.3021 ms.
  - medium sample: 524441 bytes, cold avg 12.6654 ms, cold p95 35.1395 ms, hot avg 1.2734 ms, hot p95 1.6393 ms.

## Post-change Verification

- Targeted:
  - `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InkStorageServiceTests|FullyQualifiedName~PilotMetricsScriptContractTests"`
  - pass: 10 passed, 0 failed, 0 skipped.
- Pilot metrics smoke:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validation\collect-pilot-metrics.ps1 -LogRoot artifacts\validation\current-review-pilot -WindowMinutes 30`
  - pass: generated `artifacts\validation\current-review-pilot\validation\pilot-metrics-20260426_220748.md`.
- Second-slice targeted:
  - `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InkPersistenceServiceTests|FullyQualifiedName~InkStorageDiagnosticsContractTests|FullyQualifiedName~PilotMetricsScriptContractTests"`
  - pass: 27 passed, 0 failed, 0 skipped.
- Literal path pilot metrics smoke:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validation\collect-pilot-metrics.ps1 -LogRoot artifacts\validation\literal-[pilot] -WindowMinutes 1440`
  - pass: generated `artifacts\validation\literal-[pilot]\validation\pilot-metrics-20260426_222118.md`.
- Build:
  - `dotnet build ClassroomToolkit.sln -c Debug`
  - pass: 0 warnings, 0 errors, elapsed `00:00:02.08`.
- Full tests:
  - `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug`
  - pass: 3465 passed, 0 failed, 0 skipped.
- Contract/invariant:
  - `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - pass: 28 passed, 0 failed, 0 skipped.
- Hotspot:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1`
  - pass: `[hotspot] PASS - all .cs files within line budget (max=1200)`.
- Unified quality gate:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
  - pass: `[quality] ALL PASS`.
  - included dependency-governance active-waiver check, dependency-vulnerability scan, logging-alert-threshold, and analyzer-backlog-baseline.
  - analyzer backlog report: total `635`.

## Hotspot Review

- Reviewed changed files manually:
  - `InkStorageService.cs`: changes are localized to input normalization, read-side non-fatal exception handling, and cleanup enumeration. No public model or persisted JSON shape changed.
  - `InkPersistenceService.cs`: changes are localized to sidecar path derivation, invalid path admission, and read-side non-fatal exception handling. Existing sidecar JSON shape and `.ctk-ink` layout are unchanged.
  - `collect-pilot-metrics.ps1`: one-character counter path typo fix; no output schema change.
  - tests: added regression/contract coverage only.
- No line-budget hotspot breach.

## Rollback

- Revert this slice with:
  - `git checkout -- src/ClassroomToolkit.App/Ink/InkStorageService.cs scripts/validation/collect-pilot-metrics.ps1 tests/ClassroomToolkit.Tests/InkStorageServiceTests.cs`
  - `git checkout -- src/ClassroomToolkit.App/Ink/InkPersistenceService.cs tests/ClassroomToolkit.Tests/InkPersistenceServiceTests.cs`
  - `git clean -f -- tests/ClassroomToolkit.Tests/PilotMetricsScriptContractTests.cs docs/change-evidence/20260426-systematic-review-optimization.md`
- Then rerun:
  - `dotnet build ClassroomToolkit.sln -c Debug`
  - `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug`
  - contract/invariant filter
  - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1`
