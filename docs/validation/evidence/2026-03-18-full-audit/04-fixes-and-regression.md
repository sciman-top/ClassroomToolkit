# 04 Fixes And Regression

- Date: 2026-03-18
- Scope: Post-finding fix verification (Round-1)

## Implemented Fixes

1. `ImageManagerWindow` folder expansion lifecycle hardening
- File: `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs`
- Change:
  - `OnFolderExpanded` changed from `async void` body to fire-and-forget delegator.
  - New `OnFolderExpandedAsync` wraps full path with shutdown-safe exception handling.

2. Added regression contract tests
- File: `tests/ClassroomToolkit.Tests/ImageManagerWindowFolderExpandLifecycleContractTests.cs`
- Coverage:
  - verifies event handler delegates to async worker
  - verifies shutdown-safe catches are present

3. Added `ComObjectManager` lifecycle contract tests
- File: `tests/ClassroomToolkit.Tests/ComObjectManagerContractTests.cs`
- Coverage:
  - track guard for non-COM and disposed manager path
  - release path with non-fatal exception guard
  - dispose reverse-order release and state clearing contract

4. Added cancellation-aware retry path in window interop executor
- File: `src/ClassroomToolkit.App/Windowing/WindowInteropRetryExecutor.cs`
- Change:
  - `Execute` and `ExecuteWithValue` now accept optional `CancellationToken`.
  - Retry wait path now checks cancellation via wait handle before next attempt.
  - Existing call sites remain backward compatible.

5. Added runtime COM integration tests
- File: `tests/ClassroomToolkit.Tests/ComObjectManagerIntegrationTests.cs`
- Coverage:
  - tracked COM object release idempotency
  - disposed manager `Track` behavior on COM object
  - fallback creation across multiple COM providers

## Verification Commands

1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ImageManagerWindowFolderExpandLifecycleContractTests"`
- Result: PASS (2/2)

2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~ImageManager"`
- Result: PASS (86/86)

3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ComObjectManagerContractTests"`
- Result: PASS (3/3)

4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~WindowInteropRetryExecutorTests"`
- Result: PASS (10/10)

5. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ComObjectManager"`
- Result: PASS (5/5)

## Residual Risks

- No blocker-level residual risk from current Round-1 findings.

## Manual Gate Status

- Decision: skipped by user instruction (`跳过人工`).
- Implication: classroom scene/manual DPI/multi-window acceptance is not verified in this round.
- Deferred checklist: `docs/validation/manual-final-regression-checklist.md`
