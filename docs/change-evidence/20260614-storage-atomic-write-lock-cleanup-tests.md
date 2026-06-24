# 2026-06-14 Storage Atomic Write Lock Cleanup Tests

## Scope
- Rule IDs: R1, R2, R5, R6, R8
- Risk: low
- Boundary: `tests/ClassroomToolkit.Tests/*Settings*`, `tests/ClassroomToolkit.Tests/Ink*`, `tests/ClassroomToolkit.Tests/StudentWorkbookStoreTests.cs`
- Target: verify that the existing atomic-write paths clean up temp files when the destination file is locked, without changing persistence semantics.

## Current Landing
Before this slice:
- `AtomicFileReplaceUtility` already had direct temp-cleanup coverage.
- `IniSettingsStore` and `InkStorageService` already had locked-target temp cleanup tests.
- `JsonSettingsDocumentStoreAdapter`, `InkPersistenceService`, `InkWriteAheadLogService`, and `StudentWorkbookStore` relied on the same underlying atomic-write path, but lacked direct locked-target temp cleanup proof.

## Change
- Added locked-target temp cleanup tests for:
  - `JsonSettingsDocumentStoreAdapter`
  - `InkPersistenceService`
  - `InkWriteAheadLogService`
  - `StudentWorkbookStore`
- Kept implementation unchanged after confirming the current architecture:
  - `settings / workbook` use `AtomicFileReplaceUtility`
  - `ink / wal` use `InkAtomicFileWriter`, which delegates to `AtomicFileReplaceUtility`

## Verification
- `dotnet build ClassroomToolkit.sln -c Debug -p:UseSharedCompilation=false`
  - PASS: 0 warnings, 0 errors.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false --filter "FullyQualifiedName~AtomicFileReplaceUtilityTests|FullyQualifiedName~JsonSettingsDocumentStoreAdapterTests|FullyQualifiedName~IniSettingsStoreSaveTests|FullyQualifiedName~InkPersistenceServiceTests|FullyQualifiedName~InkStorageServiceTests|FullyQualifiedName~InkWriteAheadLogServiceTests|FullyQualifiedName~StudentWorkbookStoreTests"`
  - PASS: 62 related tests passed.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false`
  - PASS: full test suite completed successfully.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - PASS: contract/invariant filter completed successfully.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - PASS: all `.cs` files within line budget.

## Hotspot Review
- No product code or persistence helper semantics changed in this slice.
- The new coverage closes the most practical consistency gap for `Task 4`: lock-file temp cleanup behavior now has direct tests across settings, ink sidecar, wal, and workbook save paths.
- This keeps the next storage slice free to focus on real semantic drift instead of basic cleanup parity.

## Rollback
- Revert:
  - `tests/ClassroomToolkit.Tests/JsonSettingsDocumentStoreAdapterTests.cs`
  - `tests/ClassroomToolkit.Tests/InkPersistenceServiceTests.cs`
  - `tests/ClassroomToolkit.Tests/InkWriteAheadLogServiceTests.cs`
  - `tests/ClassroomToolkit.Tests/StudentWorkbookStoreTests.cs`
- Remove this evidence file
