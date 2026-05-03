# 2026-05-04 Logging diagnostics closeout

## Scope
- Rules: R2, R3, R6, R8, E4
- Risk: low
- Boundary: logging diagnostics evidence and backlog status only.
- Current landing:
  - `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
  - `src/ClassroomToolkit.App/Utilities/SafeTaskRunner.cs`
  - `src/ClassroomToolkit.Infra/Logging/LogRetentionPolicy.cs`
- Target home: background logging failures stay best-effort and diagnosable without user-visible disruption.

## Basis
- P1 backlog item: logging and diagnostics chain minimum observability.
- Existing code and contract evidence:
  - `FileLoggerProvider` writes Debug diagnostics for queue worker, flush, append, shutdown wait, wait handle, drop summary append, and resource dispose failures.
  - `SafeTaskRunner` logs normalized source plus exception type and message for task and `onError` callback failures.
  - `LogRetentionPolicy` logs retention scan and file cleanup failures with source fields, exception type, and message.
  - No `MessageBox` or user-visible dialog is used by these background logging paths.

## Change
- Marked P1 Task 4 acceptance criteria and verification complete in `docs/tech-debt-backlog.md`.
- No production code change in this closeout slice.

## Verification
- Targeted logging diagnostics test:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~FileLoggerProviderTests|FullyQualifiedName~FileLoggerProviderShutdownSafetyContractTests|FullyQualifiedName~SafeTaskRunnerTests"`
- Result: passed, 29 tests.
- Full gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed, 3481 tests.
  - contract/invariant: passed, 29 tests.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` passed.
  - analyzer baseline: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug` passed, total=0.
  - diff check: `git diff --check` reported only LF/CRLF worktree warnings, no whitespace errors.

## Hotspot review
- Background logging and retention failures remain best-effort.
- Diagnostic paths include source tags and exception type/message where exceptions are available.
- No user-visible popup, new dependency, persisted data, settings, or runtime behavior change in this slice.

## Rollback
- Revert:
  - `docs/tech-debt-backlog.md`
  - `docs/change-evidence/20260504-logging-diagnostics-closeout.md`
