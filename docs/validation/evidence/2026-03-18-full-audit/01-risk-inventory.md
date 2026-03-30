# 01 Risk Inventory

- Date: 2026-03-18
- Baseline commit: `7c5df30`
- Scope: Round-1 high-risk code audit

| File | Risk Type | Existing Test Signals | Gap | Batch |
| --- | --- | --- | --- | --- |
| src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs | Cross-page state, async timing, dispatch fallback | `CrossPageDisplayLifecycleContractTests` + multiple CrossPage policy tests | No direct behavioral tests for long-sequence interleaving paths | R1-App |
| src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Input.cs | Input interpolation, tool routing, high-frequency events | Indirect via pointer/stylus policy tests | Missing direct contract around interpolation edge cases and cancellation race | R1-App |
| src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.cs | Ink runtime state and persistence hooks | Ink regression + performance tests | Missing targeted test for mixed-mode transitions under rapid toggles | R1-App |
| src/ClassroomToolkit.App/Photos/ImageManagerWindow.xaml.cs | async void events, Task.Run IO, UI threading | `ImageManager*` and transition tests | Need explicit guard tests for async event teardown and dispose timing | R1-App |
| src/ClassroomToolkit.App/Windowing/WindowInteropRetryExecutor.cs | retry/backoff, Thread.Sleep branch | `WindowInteropRetryExecutorTests` | Add negative-path latency and cancellation admission checks | R1-Windowing |
| src/ClassroomToolkit.Interop/Presentation/KeyboardHook.cs | Hook lifecycle and callback dispatch safety | `InteropHookLifecycleContractTests`, `InteropHookEventDispatchContractTests` | Add runtime stress tests around rapid stop/start sequences | R1-Interop |
| src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook.cs | WPS navigation hook, event dispatch | `InteropHookLifecycleContractTests`, `InteropHookEventDispatchContractTests` | Need integration-like scenario for unavailable slideshow target | R1-Interop |
| src/ClassroomToolkit.Interop/Utilities/ComObjectManager.cs | COM object lifetime and release idempotency | None direct | Add dedicated unit tests (`ComObjectManagerTests`) | R1-Interop |
| src/ClassroomToolkit.Infra/Storage/RollCallSqliteStoreAdapter.cs | storage fallback, exception downgrade, consistency | `RollCallSqliteStoreAdapterTests` | Add corruption and partial-write recovery case | R1-Infra |
| src/ClassroomToolkit.Infra/Storage/StudentWorkbookSqliteStoreAdapter.cs | arbitration between excel/sqlite state | `StudentWorkbookSqliteStoreAdapterTests` | Add conflict precedence and malformed state payload cases | R1-Infra |

## Notes

- Current workspace was clean before running Chunk-1 gates.
- Next action should start from Chunk-2 hotspot deep review (`App/Windowing -> Interop -> Infra`).
