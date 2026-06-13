# 20260602 window layer and countdown UI

## Scope
- Boundary: classroom floating window z-order and countdown UI labels/styles only.
- Current landing: `src/ClassroomToolkit.App` WPF windowing and XAML surfaces.
- Target landing: stable classroom layer order where toolbar/roll-call/launcher stay above roll-call student photos, and student photos stay above ordinary focus windows.

## Rules And Risk
- Rules: R1, R2, R6, R8, E4.
- Risk: medium for z-order behavior, low for countdown label/style.
- Compatibility: no changes to `students.xlsx`, `student_photos/`, `settings.ini`, or persisted formats.

## Changes
- `PhotoOverlayWindow` starts with `Topmost="False"`, so the photo does not briefly cover toolbar/roll-call/launcher during `Show()`.
- Runtime photo placement uses `ApplyNoActivateBehind` and a resolved critical-window anchor, inserting the photo below toolbar/roll-call/launcher without activation.
- `MainWindow.ZOrder` resolves the photo anchor in priority order: toolbar, roll-call, then launcher; toolbar is used first because it is the lowest critical floating control in the final retouch order.
- `WindowTopmostExecutor.PrepareNoActivateBehind` now creates/prepares the hidden photo window handle before `Show()`, so first display and display-after-pause use the same anchored z-order as continuous photos.
- Follow-up after live report: `RollCallWindow` no longer closes and recreates a hidden `PhotoOverlayWindow`, so a pause after auto-close does not force the next photo back through a brand-new window path.
- Follow-up after live report: first display/re-display keeps the photo window transparent and non-hit-testable until a render-priority deferred retouch has again placed the photo behind the anchor and re-touched toolbar/roll-call/launcher.
- Countdown bottom-bar action label changed from `时长` to `设置`.
- `TimerSetDialog` minute/second value boxes now inherit the shared dark `TextBox` template.

## Root Cause
- First display and display-after-pause were still running `Show()` before the photo window could be inserted behind the critical floating controls.
- Continuous photos were already visible, so they skipped the `Show()` first-frame path and only ran the post-show z-order retouch; that is why the second continuous photo looked correct.
- The fix moves the first z-order operation earlier: hidden photo windows get an HWND via `WindowInteropHelper.EnsureHandle()` and are positioned behind the anchor before `Show()`.
- Follow-up root cause from live report: the hidden-overlay recreation path made every paused photo a fresh first-show window. In addition, revealing `Opacity=1.0` in the same UI call stack was still too early for WPF's show/z-order finalization on some runs; the visible reveal now waits until a render-priority retouch completes.

## Commands
- `codex --version`
  - key output: `codex-cli 0.135.0`
- `codex --help`
  - key output: command list includes `exec`, `review`, `doctor`, `mcp`, `plugin`, `debug`, `features`.
- `codex status`
  - platform_na: `stdin is not a terminal`
  - alternative_verification: `codex --version` and `codex --help`
  - evidence_link: `docs/change-evidence/20260602-window-layer-countdown.md`
  - expires_at: `2026-06-09`
- `dotnet build ClassroomToolkit.sln -c Debug`
  - key output: passed, 0 warnings, 0 errors.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build`
  - key output: superseded by full current run below.
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug`
  - key output: passed, 3532 tests.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - key output: superseded by current filtered run below.
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - key output: passed, 29 tests.
- `powershell -File scripts\quality\check-hotspot-line-budgets.ps1`
  - key output: `[hotspot] PASS - all .cs files within line budget (max=1200)`.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~WindowTopmostExecutorTests|FullyQualifiedName~PhotoOverlayTopmostNoActivateContractTests|FullyQualifiedName~RollCallAuxOverlayTopmostPolicyTests"`
  - key output: superseded by focused current run below.
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PhotoOverlayTopmostNoActivateContractTests|FullyQualifiedName~WindowTopmostExecutorTests|FullyQualifiedName~RollCallAuxOverlayTopmostPolicyTests|FullyQualifiedName~RollCallWindowPhotoOverlayReuseContractTests|FullyQualifiedName~PhotoOverlayShowOrderContractTests|FullyQualifiedName~PhotoOverlayCloseHideGuardContractTests"`
  - key output: passed, 25 tests.
- `git diff --check`
  - key output: no whitespace errors; LF/CRLF warnings only.

## Hotspot Review
- Z-order: student photo overlay no longer starts as topmost. When an anchor is available, `SetWindowPos(photo, anchorHandle, SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOMOVE | SWP_NOSIZE)` positions it under the critical floating controls from the first runtime z-order apply.
- First-frame safety: hidden photo windows are now prepared behind the anchor before `Show()`, closing the reported gap where the first photo after a pause briefly covered critical controls.
- Follow-up first-frame safety: the photo window remains `Opacity=0.0` and `IsHitTestVisible=false` until `DeferRevealAfterInitialZOrderRetouch(...)` runs at render priority, re-applies `ApplyNoActivateBehind`, triggers immediate critical-window retouch, and only then restores opacity/hit testing.
- Overlay reuse: `RecreateHiddenPhotoOverlayIfNeeded` was removed from `RollCallWindow.Photo`, so auto-close pauses no longer create a new photo window with a fresh Z-order history.
- Focus safety: photo overlay uses `ShowActivated = false` and `ApplyNoActivateBehind`, so the fix should not steal classroom focus.
- Countdown UI: value boxes reuse shared `TextBox` styling instead of default white platform chrome.
- Live desktop screenshot: not executed to avoid starting or disturbing a native desktop session without explicit user confirmation.
  - gate_na reason: WPF app launch could interrupt the current desktop session.
  - alternative_verification: WPF XAML inspection, z-order contract tests, full test run, hotspot script.
  - evidence_link: `docs/change-evidence/20260602-window-layer-countdown.md`
  - expires_at: `2026-06-09`

## Rollback
- Revert:
  - `src/ClassroomToolkit.App/MainWindow.ZOrder.cs`
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml`
  - `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
  - `src/ClassroomToolkit.App/RollCallWindow.xaml`
  - `src/ClassroomToolkit.App/RollCallWindow.Photo.cs`
  - `src/ClassroomToolkit.App/TimerSetDialog.xaml`
  - `src/ClassroomToolkit.App/Windowing/RollCallAuxOverlayTopmostPolicy.cs`
  - `src/ClassroomToolkit.App/Windowing/WindowTopmostExecutor.cs`
  - `src/ClassroomToolkit.App/Windowing/IWindowTopmostInteropAdapter.cs`
  - `src/ClassroomToolkit.App/Windowing/NativeWindowTopmostInteropAdapter.cs`
  - related tests changed in this slice.
