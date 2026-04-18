# 20260418 Window Drag State Test Isolation

## Purpose
Stabilize the flaky `WindowTopmostExecutor` regression observed during full-suite execution.

## Basis
- `tests/ClassroomToolkit.Tests/App/WindowTopmostExecutorTests.cs`
- `tests/ClassroomToolkit.Tests/PresentationFocusRestorePolicyTests.cs`
- `src/ClassroomToolkit.App/Windowing/WindowDragOperationState.cs`

## Root Cause
- `WindowDragOperationState` is process-wide static state.
- `WindowTopmostExecutorTests` and `PresentationFocusRestorePolicyTests` both manipulate that state with `WindowDragOperationState.Begin()`.
- Under the full xUnit suite, those classes can run in parallel because they are in different default test collections.
- When another test holds drag state active, `WindowTopmostExecutor.TryApplyHandleNoActivate(...)` exits early before touching the fake adapter, which explains the observed `adapter.CallCount = 0`.

## Change
- Added a dedicated non-parallel xUnit collection:
  - `tests/ClassroomToolkit.Tests/SharedWindowDragStateCollection.cs`
- Moved these shared-state tests into that collection:
  - `tests/ClassroomToolkit.Tests/App/WindowTopmostExecutorTests.cs`
  - `tests/ClassroomToolkit.Tests/PresentationFocusRestorePolicyTests.cs`

## Why This Fix
- The failure was test interference, not production behavior regression.
- The minimal safe fix is to isolate the tests that mutate shared process-wide drag state.
- This avoids changing runtime windowing semantics for the shipping app.

## Verification
1. `dotnet build ClassroomToolkit.sln -c Debug`
   - exit `0`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore`
   - exit `0`
   - `通过: 3258`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - exit `0`
   - `通过: 28`
4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-restore --no-build --filter "FullyQualifiedName~WindowTopmostExecutorTests.TryApplyHandleNoActivate_ShouldNotRetry_OnInvalidHandleError|FullyQualifiedName~PresentationFocusRestorePolicyTests.CanRestore_ShouldFollowWindowDragOperationState"`
   - exit `0`
   - `通过: 2`

## Rollback
- Remove `SharedWindowDragStateCollection`
- Remove `[Collection(SharedWindowDragStateCollection.Name)]` from the two test classes
