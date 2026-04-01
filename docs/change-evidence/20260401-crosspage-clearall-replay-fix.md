# 2026-04-01 Cross-Page ClearAll Replay Fix

- Rule IDs: `R1` `R2` `R3` `R6` `R8`
- Risk: Medium
- Boundary: `src/ClassroomToolkit.App/Paint/*`
- Current landing: `PaintOverlayWindow` cross-page ink persistence / recovery path
- Target destination: keep page-state recovery and auto-save coordination in `Paint` layer

## Goal

Fix the regression where `PDF/图片全屏 + 跨页显示` could replay already-cleared ink from the original page after:

- brush mode: clear-all on one page, then write on the neighbor page
- cursor mode: clear-all on one page, then drag/zoom to trigger redraw

## Root Cause

`ClearAll()` cleared runtime strokes and synchronously persisted an empty page, but it did **not** cancel already-scheduled auto-save work.

That left a stale window where an older queued auto-save snapshot could still write pre-clear strokes back to sidecar storage. Later cross-page neighbor rendering / sidecar fallback legally reloaded that stale page snapshot, which looked like “cleared ink reappeared”.

## Changes

1. Added `InkAutoSaveSnapshotAdmissionPolicy`.
   - Rejects auto-save persistence when the queued snapshot hash no longer matches the current runtime page hash.
   - Prevents stale pre-clear snapshots from overwriting newer page state.

2. Hardened `QueueSidecarAutoSave(...)` in `PaintOverlayWindow.Export.cs`.
   - Checks runtime page hash before each persist attempt.
   - Defers stale snapshots instead of writing them.

3. Hardened `ClearPhotoInkStateAfterClearAll()` in `PaintOverlayWindow.xaml.cs`.
   - Stops the auto-save timer.
   - Advances `_inkSidecarAutoSaveGate` generation before persisting the empty snapshot.
   - Ensures queued pre-clear auto-save work becomes stale immediately.

4. Added tests.
   - `InkAutoSaveSnapshotAdmissionPolicyTests`
   - `PaintOverlayClearAllCrossPageRecoveryContractTests`

## Verification

### Precheck

- `Get-Command dotnet` -> `C:\Program Files\dotnet\dotnet.exe`
- `Get-Command powershell` -> `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`
- `Test-Path tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj` -> `True`

### Focused Red/Green

- Red:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~InkAutoSaveSnapshotAdmissionPolicyTests|FullyQualifiedName~PaintOverlayClearAllCrossPageRecoveryContractTests"`
  - Result: failed before implementation because `InkAutoSaveSnapshotAdmissionPolicy` did not exist.
- Green:
  - Same command after implementation
  - Result: passed `4/4`

### Hard Gates

1. Build
   - `dotnet build ClassroomToolkit.sln -c Debug`
   - Result: pass, `0 warning`, `0 error`

2. Test
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - Result: pass, `3032/3032`

3. Contract / Invariant
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - Result: pass, `24/24`

4. Hotspot
   - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - Result: `status=PASS`

## Platform N/A

- Type: `platform_na`
- Item: `codex status`
- Reason: non-interactive terminal returned `stdin is not a terminal`
- Alternative verification:
  - `codex --version` -> `codex-cli 0.118.0`
  - `codex --help` succeeded
  - active repo rule path was provided in task context
- Evidence link: this file
- Expires at: `2026-04-08`

## Rollback

1. Revert this fix:
   - `src/ClassroomToolkit.App/Paint/InkAutoSaveSnapshotAdmissionPolicy.cs`
   - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Export.cs`
   - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.xaml.cs`
   - `tests/ClassroomToolkit.Tests/InkAutoSaveSnapshotAdmissionPolicyTests.cs`
   - `tests/ClassroomToolkit.Tests/PaintOverlayClearAllCrossPageRecoveryContractTests.cs`

2. Preserved user worktree snapshot:
   - `stash@{0}: pre-crosspage-clearall-cleanup-20260401`

3. If later需要恢复清理前的用户改动，再基于该 stash 做审阅/回放，不要直接丢弃。
