# 2026-05-04 InkStorage temp cleanup diagnostics

## Scope
- Rules: R2, R3, R6, R8, E4
- Risk: low
- Boundary: ink storage atomic write temp cleanup diagnostics only.
- Current landing: `src/ClassroomToolkit.App/Ink/InkStorageService.cs`
- Target home: storage-layer atomic write fallback and cleanup paths expose consistent minimum diagnostics.

## Basis
- P0 backlog item: storage-layer atomic write fallback consistency.
- Baseline storage test before change:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~JsonSettingsDocumentStoreAdapterTests|FullyQualifiedName~InkPersistenceServiceTests|FullyQualifiedName~InkStorageServiceTests|FullyQualifiedName~StudentWorkbookStoreTests"`
- Result: passed, 51 tests.

## Change
- Added `[InkStorage] temp cleanup failed path=... ex=... msg=...` Debug diagnostics for `AtomicFileReplaceUtility.WriteAtomically` temp cleanup failures in `InkStorageService`.
- Extended existing ink storage diagnostics contract coverage.

## Verification
- Targeted ink storage test:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~InkStorageServiceTests|FullyQualifiedName~InkStorageDiagnosticsContractTests|FullyQualifiedName~InkPersistenceServiceTests|FullyQualifiedName~InkWriteAheadLogServiceTests"`
- Result: passed, 36 tests.
- Full gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed, 3481 tests.
  - contract/invariant: passed, 29 tests.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` passed.
  - analyzer baseline: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug` passed, total=0.
  - diff check: `git diff --check` reported only LF/CRLF worktree warnings, no whitespace errors.

## Hotspot review
- No persisted JSON schema, sidecar path, WAL path, workbook format, or settings format change.
- Atomic write and fallback behavior remain unchanged.
- Only previously silent best-effort temp cleanup failures now emit Debug diagnostics consistent with related storage components.

## Rollback
- Revert:
  - `src/ClassroomToolkit.App/Ink/InkStorageService.cs`
  - `tests/ClassroomToolkit.Tests/InkStorageDiagnosticsContractTests.cs`
  - `docs/change-evidence/20260504-ink-storage-temp-cleanup-diagnostics.md`
