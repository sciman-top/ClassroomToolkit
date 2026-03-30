# 03 Hotspot Findings

- Date: 2026-03-18
- Baseline commit: `6f4f66c`
- Scope: Round-1 static hotspot review (`Windowing + Interop + ImageManager`)

## Findings (ordered by severity)

### F-1 High: `async void` folder expansion path lacked shutdown-safe guard

- File: `src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs:946`
- Status: Fixed in working tree
- Evidence:
  - Original handler was `private static async void OnFolderExpanded(...)`.
  - UI mutation and dispatcher await path had no outer shutdown-safe catch.
- Fix summary:
  - Changed to `void` handler delegating to `Task` worker (`_ = OnFolderExpandedAsync(sender);`).
  - Added catches for `OperationCanceledException` and `ObjectDisposedException`.
  - Added contract test: `ImageManagerWindowFolderExpandLifecycleContractTests`.

### F-2 Medium: Retry executor uses blocking sleep without cancellation admission

- File: `src/ClassroomToolkit.App/Windowing/WindowInteropRetryExecutor.cs:69`
- Status: Fixed in working tree (cancellation-aware overload added)
- Evidence:
  - Original retry loop used `Thread.Sleep(retrySleepMs)` without cancellation token.
- Recommendation:
  - Implemented cancellation-aware overloads for `Execute/ExecuteWithValue`.
  - Added cancellation-path tests in `WindowInteropRetryExecutorTests`.

### F-3 Medium: COM lifecycle manager lacks dedicated behavior tests

- File: `src/ClassroomToolkit.Interop/Utilities/ComObjectManager.cs`
- Status: Fixed in working tree (contract + runtime integration tests)
- Evidence:
  - No direct `ComObjectManager` test file in current test suite.
- Recommendation:
  - Added `ComObjectManagerContractTests` for dedup/release/dispose/disposed-track source contracts.
  - Added `ComObjectManagerIntegrationTests` for runtime COM object release/idempotency checks.

## Next Actions

1. Continue next-round review on remaining Interop and storage hotspots.
