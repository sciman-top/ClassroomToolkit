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
- Status: Open
- Evidence:
  - Retry loop uses `Thread.Sleep(retrySleepMs)` without cancellation token.
- Recommendation:
  - Introduce cancellation-aware retry overload or pluggable wait strategy.

### F-3 Medium: COM lifecycle manager lacks dedicated behavior tests

- File: `src/ClassroomToolkit.Interop/Utilities/ComObjectManager.cs`
- Status: Open
- Evidence:
  - No direct `ComObjectManager` test file in current test suite.
- Recommendation:
  - Add tests for dedup, release/dispose idempotency, disposed-track behavior.

## Next Actions

1. Implement F-3 tests (`ComObjectManagerTests`).
2. Refactor F-2 retry sleep strategy with cancellation-aware overload.
