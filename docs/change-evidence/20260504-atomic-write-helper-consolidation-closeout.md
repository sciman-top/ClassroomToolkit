# 2026-05-04 Atomic write helper consolidation closeout

## Scope
- Rules: R2, R3, R5, R6, R8, E6
- Risk: low
- Boundary: atomic write helper consolidation evidence and backlog status only.
- Current landing:
  - `src/ClassroomToolkit.Domain/Utilities/AtomicFileReplaceUtility.cs`
  - `src/ClassroomToolkit.Infra/Settings/*.cs`
  - `src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`
  - `src/ClassroomToolkit.App/Ink/*.cs`
- Target home: repeated `temp + replace/copy + cleanup` persistence writeback logic remains centralized.

## Basis
- P2 backlog item: repeated atomic write helper consolidation.
- Dependency P0 Task 3 is complete and verified.
- Current implementation evidence:
  - `AtomicFileReplaceUtility.WriteAtomically` owns directory creation, temp naming, replace/move, and best-effort temp cleanup.
  - `AtomicFileReplaceUtility.ReplaceOrOverwrite` owns `File.Replace` fallback to `File.Copy(overwrite: true)` plus temp deletion.
  - Settings, ink sidecar, ink storage, ink WAL, ink export manifest, and workbook save paths all call `WriteAtomically`.
  - Workbook temp file extension preservation is covered by `AtomicFileReplaceUtilityTests`.

## Change
- Marked P2 Task 7 acceptance criteria and verification complete in `docs/tech-debt-backlog.md`.
- No production code change in this closeout slice.

## Verification
- Targeted storage and helper test:
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
- `rg` review confirmed no remaining ad hoc `File.Replace` outside `AtomicFileReplaceUtility`.
- `File.Copy(... overwrite: false)` occurrences are backup/import flows, not atomic persistence replacement.
- No additional abstraction was introduced in this slice.
- No persisted JSON, INI, workbook, WAL, sidecar, or ink export format change.

## Rollback
- Revert:
  - `docs/tech-debt-backlog.md`
  - `docs/change-evidence/20260504-atomic-write-helper-consolidation-closeout.md`
