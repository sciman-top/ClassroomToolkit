# 2026-05-04 StudentPhotoResolver warmup cancellation

## Scope
- Rules: R2, R3, R6, R8, E4
- Risk: low
- Boundary: `StudentPhotoResolver` background warmup cancellation only.
- Current landing: `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
- Target home: photo cache warmup should stop promptly after cancellation and avoid stale or unnecessary cache growth.

## Basis
- P0 backlog item: unify photo cache and warmup close path.
- Baseline targeted test before change:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~StudentPhotoResolverTests|FullyQualifiedName~RollCallViewModelPhotoPathRefreshTests"`
- Result: passed, 25 tests.

## Change
- Split regular photo resolution index building from warmup index building.
- Passed the warmup cancellation token into the warmup index path.
- Added cancellation checks before taking the index lock, after entering the lock, during file enumeration, and before cache writeback.
- Added a race regression test proving a canceled warmup index build does not repopulate `_cache` after it resumes.

## Verification
- Targeted photo test:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~StudentPhotoResolverTests|FullyQualifiedName~RollCallViewModelPhotoPathRefreshTests"`
- Result: passed, 26 tests.
- Full gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed, 3481 tests.
  - contract/invariant: passed, 29 tests.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` passed.
  - analyzer baseline: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug` passed, total=0.
  - diff check: `git diff --check` reported only LF/CRLF worktree warnings, no whitespace errors.

## Hotspot review
- No persisted data, settings, workbook, or photo directory format change.
- Direct `ResolvePhotoPath` behavior remains unchanged.
- Warmup now stops cache writeback when canceled, reducing unnecessary IO after repeated warmup requests or close-path cancellation.

## Rollback
- Revert:
  - `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
  - `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`
  - `docs/change-evidence/20260504-student-photo-warmup-cancellation.md`
