# 2026-05-04 Storage atomic write consistency closeout

## Scope
- Rules: R2, R3, R6, R8, E4, E6
- Risk: low
- Boundary: storage atomic write consistency evidence and backlog status only.
- Current landing: `settings / ink / workbook / wal` atomic write call sites.
- Target home: storage writeback paths share one atomic write helper and keep persisted formats compatible.

## Basis
- P0 backlog item: storage-layer atomic write fallback consistency.
- Current implementation evidence:
  - `JsonSettingsDocumentStoreAdapter.Save` uses `AtomicFileReplaceUtility.WriteAtomically`.
  - `IniSettingsStore.Save` uses `AtomicFileReplaceUtility.WriteAtomically`.
  - `InkPersistenceService`, `InkStorageService`, `InkWriteAheadLogService`, and `InkExportManifestUtilities` use `AtomicFileReplaceUtility.WriteAtomically`.
  - `StudentWorkbookStore.Save` uses `AtomicFileReplaceUtility.WriteAtomically` with workbook extension preservation.
  - `AtomicFileReplaceUtility.ReplaceOrOverwrite` owns the `File.Replace -> File.Copy(overwrite) -> File.Delete(temp)` fallback.

## Change
- Marked P0 Task 3 complete in `docs/tech-debt-backlog.md`.
- Recorded the final consistency evidence after the prior helper consolidation and ink storage diagnostics patch.
- No production code change in this closeout slice.

## Verification
- Storage baseline:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~JsonSettingsDocumentStoreAdapterTests|FullyQualifiedName~IniSettingsStoreSaveTests|FullyQualifiedName~InkPersistenceServiceTests|FullyQualifiedName~InkStorageServiceTests|FullyQualifiedName~InkWriteAheadLogServiceTests|FullyQualifiedName~StudentWorkbookStoreTests|FullyQualifiedName~AtomicFileReplaceUtilityTests"`
- Result: passed, 58 tests.
- Full gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed, 3481 tests.
  - contract/invariant: passed, 29 tests.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` passed.
  - analyzer baseline: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug` passed, total=0.
  - diff check: `git diff --check` reported only LF/CRLF worktree warnings, no whitespace errors.

## Hotspot review
- `rg` review confirmed no remaining ad hoc `File.Replace` call outside `AtomicFileReplaceUtility`.
- `SettingsDocumentMigrationService` uses `File.Copy(... overwrite: false)` for backup creation, not writeback replacement.
- `InkStorageService.CopyPhoto` uses `File.Copy(... overwrite: false)` for user photo import, not atomic persistence writeback.
- No persisted JSON, INI, workbook, WAL, sidecar, or ink export format change.

## Rollback
- Revert:
  - `docs/tech-debt-backlog.md`
  - `docs/change-evidence/20260504-storage-atomic-write-consistency-closeout.md`
