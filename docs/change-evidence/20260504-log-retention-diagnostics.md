# 2026-05-04 log retention diagnostics

## Scope

- Rule IDs: R2/R3/R6/R8, E4
- Risk level: low
- Boundary: log retention best-effort diagnostics only.
- Goal: make retention cleanup failures diagnosable without changing retention behavior, user-visible UX, dependencies, or shutdown behavior.

## Basis

- `LogRetentionPolicy.TryApply` intentionally treats retention cleanup as best effort so logging startup cannot fail because old log cleanup failed.
- The existing catch blocks preserved robustness but were silent, making cleanup failures hard to diagnose.

## Change

- Added `Debug.WriteLine` diagnostics for retention scan failures.
- Added `Debug.WriteLine` diagnostics for individual log file cleanup failures.
- Added contract coverage to keep `source + exception type + message` diagnostics in place.

## Verification

- Targeted logging tests:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~FileLoggerProviderTests|FullyQualifiedName~FileLoggerProviderShutdownSafetyContractTests|FullyQualifiedName~SafeTaskRunnerTests"`: passed, 29 passed.
- Final hard gates:
  - build: `dotnet build ClassroomToolkit.sln -c Debug`: passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: passed, 3479 passed.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`: passed, 28 passed.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`: passed.
  - static analyzer: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`: passed, `total=0`.
  - whitespace: `git diff --check`: passed; Git reported only existing LF/CRLF normalization warnings for touched `.cs` files.

## Hotspot Review

- No retention policy threshold changed.
- No user-visible dialog or production dependency added.
- Cleanup remains best effort; diagnostics only go to debug output.

## Rollback

- Revert `src/ClassroomToolkit.Infra/Logging/LogRetentionPolicy.cs`.
- Revert the added contract in `tests/ClassroomToolkit.Tests/FileLoggerProviderTests.cs`.
