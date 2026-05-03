# 2026-05-04 RollCall preload concurrency closeout

## Scope
- Rules: R2, R3, R6, R8, E4
- Risk: low
- Boundary: RollCall preload concurrency evidence and backlog status only.
- Current landing: `src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Data.cs`
- Target home: preload task state remains race-safe and diagnostics stay explicit.

## Basis
- P0 backlog item: review roll-call preload concurrency state machine.
- Existing code evidence:
  - `CompletePreloadTask` clears `_preloadTask` only when `ReferenceEquals(_preloadTask, preloadTask)`.
  - `LoadDataAsync` returns before UI application when `_disposed`, cancellation is requested, or dispatcher shutdown has started/finished.
  - Preload consume, canceled task, faulted task, and file write time failures are formatted through `RollCallDataLoadDiagnosticsPolicy`.

## Change
- Marked P0 Task 2 acceptance criteria and verification complete in `docs/tech-debt-backlog.md`.
- No production code change in this closeout slice.

## Verification
- Targeted preload concurrency test:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~RollCallViewModelPreloadConcurrencyTests"`
- Result: passed, 2 tests.
- Full gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed, 3481 tests.
  - contract/invariant: passed, 29 tests.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` passed.
  - analyzer baseline: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug` passed, total=0.
  - diff check: `git diff --check` reported only LF/CRLF worktree warnings, no whitespace errors.

## Hotspot review
- `WarmupData`: matching path and write time guard prevents duplicate preload work.
- `CompletePreloadTask`: stale task completion cannot clear a newer `_preloadTask`.
- `LoadDataAsync`: dispose/cancellation and dispatcher shutdown checks block UI application after close.
- Failure paths keep diagnostic source, exception type, and message through `RollCallDataLoadDiagnosticsPolicy`.

## Rollback
- Revert:
  - `docs/tech-debt-backlog.md`
  - `docs/change-evidence/20260504-rollcall-preload-concurrency-closeout.md`
