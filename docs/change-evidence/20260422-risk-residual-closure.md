# 2026-04-22 Risk And Residual Closure

## Summary

- scope: `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Loading.cs`
- scope: `tests/ClassroomToolkit.Tests/ImageManagerThumbnailCacheWarmupContractTests.cs`
- scope: residual verification for `tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
- target destination: close remaining low-risk residuals from the previous review rounds without changing business semantics, UI behavior, or external file formats
- risk level: low

## Basis

1. After upgrading to `FluentAssertions 8`, the test project still contained legacy numeric assertion API usage in `InkReplayBaselineIntegrityTests`, preventing the migration from compiling cleanly.
2. `ImageManagerWindow.QueueThumbnailLoad` decoded thumbnails on background workers, but discarded stale-generation results before writing them into the existing LRU thumbnail cache. During rapid folder switches this meant already-completed decode work could not be reused and would be re-decoded on the next visit.
3. The thumbnail cache is already bounded (`ThumbnailCacheCapacity = 512`), keyed by path + decode width + modified time, and only a very small number of stale workers can complete after cancellation because thumbnail worker concurrency is capped to `1..2`. That makes warming the cache with decoded-but-stale results low risk while preserving current UI semantics.

## Rule Mapping

- `R1`: current landing point was dependency/performance residual closure; target destination was to finish the migration and reduce repeated thumbnail decode waste without changing rendering behavior.
- `R2`: first fixed the blocking test migration error, then applied one small thumbnail cache fix, then re-ran hard gates.
- `R3`: the thumbnail change addresses the root cause (decoded results dropped before cache warmup) instead of adding more dispatch guards.
- `R6`: executed `build -> test -> contract/invariant -> hotspot` in fixed order after the code changes.
- `R8`: this file records `basis -> command -> evidence -> rollback`.

## Platform Diagnostics

- cmd: `codex --version`
  - exit_code: `0`
  - key_output: `codex-cli 0.122.0`
- cmd: `codex --help`
  - exit_code: `0`
  - key_output: CLI help displayed successfully.
- cmd: `codex status`
  - exit_code: `1`
  - platform_na:
    - reason: non-interactive environment returned `stdin is not a terminal`
    - alternative_verification: used explicit `codex --version`, `codex --help`, repository `AGENTS.md`, and local workspace inspection instead
    - evidence_link: `docs/change-evidence/20260422-risk-residual-closure.md`
    - expires_at: `2026-04-22`

## Changes

### Slice 1: FluentAssertions migration closure

- replaced the remaining `BeLessOrEqualTo` calls with `BeLessThanOrEqualTo` in `InkReplayBaselineIntegrityTests`
- result: `FluentAssertions 8.9.0` migration now compiles and runs cleanly

### Slice 2: thumbnail cache warmup on stale decode completion

- moved thumbnail cache insertion ahead of the stale-generation discard branch
- kept the existing guard that prevents stale or canceled results from updating UI state
- added a contract test to lock in the intended ordering: warm cache first, then suppress stale UI apply

## Commands And Evidence

### Reproduction and fix validation

- cmd: `dotnet build tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
  - exit_code before fix: `1`
  - key_output: three `CS1061` errors for `BeLessOrEqualTo` in `InkReplayBaselineIntegrityTests.cs`
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
  - exit_code after fix: `0`
  - key_output: `3406` tests passed

### Hard gates

- cmd: `dotnet build ClassroomToolkit.sln -c Debug -m:1`
  - exit_code: `0`
  - key_output: `0 warnings`, `0 errors`
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
  - exit_code: `0`
  - key_output: `3406` passed
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: `0`
  - key_output: `28` contract/invariant tests passed
- cmd: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code: `0`
  - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

### Dependency residual checks

- cmd: `dotnet list ClassroomToolkit.sln package --deprecated`
  - exit_code: `0`
  - key_output: no deprecated packages across all projects
- cmd: `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive`
  - exit_code: `0`
  - key_output: no vulnerable packages across all projects
- cmd: `dotnet list ClassroomToolkit.sln package --outdated --include-transitive`
  - exit_code: `0`
  - key_output: only transitive updates remain (`DocumentFormat.OpenXml`, `SixLabors.Fonts`, `SQLitePCLRaw.*`, `System.IO.Packaging`, `Microsoft.Testing.Platform*`, `Microsoft.ApplicationInsights`, etc.)

## Hotspot Review

- file: `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Loading.cs`
  - conclusion: stale thumbnails still do not update UI, but already-decoded results now seed the bounded cache and can be reused by later requests for the same path/decode width/modified time tuple.
- file: `tests/ClassroomToolkit.Tests/ImageManagerThumbnailCacheWarmupContractTests.cs`
  - conclusion: contract test pins the ordering so later refactors do not regress back to “drop decoded result before cache warmup”.

## Residuals

- transitive package updates remain available, but current evidence shows no deprecated or vulnerable packages. These are compatibility/upgrade opportunities, not release blockers.
- no further automatic change was made to the photo overlay full-size decode path because current code already runs on background workers (`SafeTaskRunner.Run -> Task.Run`) and the remaining waste inside `BitmapImage` decode is not directly cancelable through WPF APIs.

## Rollback

- rollback command:
  - `git restore --source=HEAD -- src/ClassroomToolkit.App/Photos/ImageManagerWindow.Loading.cs tests/ClassroomToolkit.Tests/InkReplayBaselineIntegrityTests.cs tests/ClassroomToolkit.Tests/ImageManagerThumbnailCacheWarmupContractTests.cs docs/change-evidence/20260422-risk-residual-closure.md docs/change-evidence/20260422-dependency-governance-upgrades.md`
