# 2026-05-04 atomic write consolidation

## Scope

- Rule IDs: R2/R3/R6/R8, E5/E6
- Risk level: low
- Boundary: shared atomic file replacement helper and workbook persistence only.
- Goal: consolidate duplicate `temp + replace/copy + cleanup` logic while preserving workbook file format, temp extension requirements, exception behavior, and rollback shape.

## Basis

- `settings`, `ini`, `ink sidecar`, and `wal` persistence already used `AtomicFileReplaceUtility.WriteAtomically`.
- `StudentWorkbookStore.Save` kept a local temp-file implementation because ClosedXML needs an Excel-compatible temp extension.
- The safe consolidation is to add a temp-extension-aware overload instead of changing workbook serialization or temp output format.

## Change

- Added `AtomicFileReplaceUtility.WriteAtomically(targetPath, tempFileExtension, writeTempFile, onTempCleanupFailure)`.
- The existing `WriteAtomically(targetPath, writeTempFile, onTempCleanupFailure)` remains compatible and delegates to the new overload with `.tmp`.
- Changed `StudentWorkbookStore.Save` to use the shared helper with the workbook extension as temp suffix.
- Added regression coverage that requested temp extensions produce `.tmp.xlsx` temporary files and clean them up.

## Verification

- Targeted storage tests:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~AtomicFileReplaceUtilityTests|FullyQualifiedName~StudentWorkbookStoreTests|FullyQualifiedName~JsonSettingsDocumentStoreAdapterTests|FullyQualifiedName~InkPersistenceServiceTests|FullyQualifiedName~InkStorageServiceTests"`: passed, 55 passed.
- Final hard gates:
  - build: `dotnet build ClassroomToolkit.sln -c Debug`: passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: passed, 3478 passed.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`: passed, 28 passed.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`: passed.
  - static analyzer: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`: passed, `total=0`.
  - whitespace: `git diff --check`: passed; Git reported only existing LF/CRLF normalization warnings for touched `.cs` files.

## Hotspot Review

- No public API removal.
- Workbook final path behavior is preserved; only the temporary file construction and cleanup owner moved to the shared utility.
- The helper still writes temp files beside the target file, then uses `File.Replace` or overwrite-copy fallback for existing targets.

## Rollback

- Revert `src/ClassroomToolkit.Domain/Utilities/AtomicFileReplaceUtility.cs`.
- Revert `src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`.
- Revert the related tests in `tests/ClassroomToolkit.Tests/AtomicFileReplaceUtilityTests.cs` and `tests/ClassroomToolkit.Tests/StudentWorkbookStoreTests.cs`.
