# 2026-05-04 code optimization audit

## Scope

- Rule IDs: R1/R2/R3/R6/R8, E4/E5/E6
- Risk level: low
- Boundary: `StudentPhotoResolver` cache invalidation behavior and related regression tests only.
- Goal: improve robustness for roll-call student photo updates after a previous cache miss, without changing public APIs, data formats, settings semantics, or photo directory layout.

## Basis

- Baseline `dotnet build ClassroomToolkit.sln -c Debug`: passed, 0 warnings, 0 errors.
- Baseline `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: passed, 3476 passed.
- Quality audit:
  - `scripts/quality/check-hotspot-line-budgets.ps1`: passed.
  - `scripts/quality/check-dependency-vulnerabilities.ps1`: passed, no vulnerable packages detected.
  - `scripts/quality/check-dependency-upgrade-feasibility.ps1`: passed, stable outdated packages are covered by active waivers.
  - `scripts/quality/check-logging-alert-threshold.ps1`: passed with local `logs` directory absent.
  - `scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`: passed, `total=0`.

## Change

- Added regression coverage for a previously missing student photo that is added after the resolver has marked a miss.
- Changed `InvalidateStudentCache` so invalidating a student always resets the class cache miss-probe throttle, even when that student was not already in the cached index.

## Verification

- Red test before fix:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StudentPhotoResolverTests"`
  - Failed at `InvalidateStudentCache_ShouldForceNextProbe_WhenStudentWasPreviouslyMissing`, proving the stale miss-probe behavior.
- Targeted tests after fix:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StudentPhotoResolverTests"`: passed, 23 passed.
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~StudentPhotoResolverTests|FullyQualifiedName~RollCallViewModelPhotoPathRefreshTests|FullyQualifiedName~RollCallViewModelPreloadConcurrencyTests|FullyQualifiedName~JsonSettingsDocumentStoreAdapterTests|FullyQualifiedName~InkPersistenceServiceTests|FullyQualifiedName~InkStorageServiceTests|FullyQualifiedName~StudentWorkbookStoreTests"`: passed, 78 passed.
- Final hard gates:
  - build: `dotnet build ClassroomToolkit.sln -c Debug`: passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: passed, 3477 passed.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`: passed, 28 passed.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`: passed.
  - static analyzer: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`: passed, `total=0`.
  - whitespace: `git diff --check`: passed; Git reported only existing LF/CRLF normalization warnings for touched `.cs` files.

## Hotspot Review

- `StudentPhotoResolver` remains API-compatible.
- The fix does not change path sanitization, class folder resolution, supported image extensions, cache TTL, or photo file format.
- Storage atomic-write review found no evidence-backed low-risk code change for this slice; workbook temp-file handling remains intentionally local because ClosedXML temp output keeps the workbook extension.

## Rollback

- Revert `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`.
- Revert the added test in `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`.
