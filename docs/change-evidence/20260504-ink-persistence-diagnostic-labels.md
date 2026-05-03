# 2026-05-04 InkPersistence diagnostic labels

## Scope
- Rules: R2, R3, R5, R6, R8
- Risk: low
- Boundary: ink persistence diagnostic labels only.
- Current landing: `src/ClassroomToolkit.App/Ink/InkPersistenceService.cs`
- Target home: repeated diagnostics should identify the failing operation without changing persistence behavior.

## Basis
- P2 backlog item: local dead code and duplicate diagnostic string cleanup.
- Baseline diagnostics test before change:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~InkStorageDiagnosticsContractTests|FullyQualifiedName~FileLoggerProviderTests|FullyQualifiedName~SafeTaskRunnerTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests"`
- Result: passed, 40 tests.

## Change
- Renamed the `InkPersistenceService` atomic temp cleanup failure diagnostic from `delete file failed` to `temp cleanup failed`.
- Kept the real sidecar delete failure diagnostic as `delete file failed`.
- Extended diagnostics contract coverage so both labels remain distinct.

## Verification
- Targeted ink persistence diagnostics test:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~InkStorageDiagnosticsContractTests|FullyQualifiedName~InkPersistenceServiceTests"`
- Result: passed, 25 tests.
- Full gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed, 3481 tests.
  - contract/invariant: passed, 29 tests.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` passed.
  - analyzer baseline: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug` passed, total=0.
  - diff check: `git diff --check` reported only LF/CRLF worktree warnings, no whitespace errors.

## Hotspot review
- No JSON sidecar schema, path, writeback, delete behavior, cache behavior, or public API change.
- The change is limited to Debug diagnostic wording and a source contract assertion.

## Rollback
- Revert:
  - `src/ClassroomToolkit.App/Ink/InkPersistenceService.cs`
  - `tests/ClassroomToolkit.Tests/InkStorageDiagnosticsContractTests.cs`
  - `docs/change-evidence/20260504-ink-persistence-diagnostic-labels.md`
