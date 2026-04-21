# 2026-04-21 Photo overlap topmost flicker fix

- rule_id: R1/R2/R6/R8
- risk_level: medium
- issue: Student photo display flickers repeatedly when overlapping toolbar/launcher/roll-call windows.

## Basis
- Observed code path: `FloatingTopmostWatchdogPolicy.ShouldForceRetouch` returned true whenever any floating utility window was visible, even during photo mode.
- Effect: watchdog (`700ms`) repeatedly triggered `RequestApplyZOrderPolicy(forceEnforceZOrder: true)` and forced topmost retouch calls.
- Follow-up diagnosis: even after suppressing the watchdog path, `FloatingTopmostPlanPolicy` still requested `OverlayShouldActivate=true` on `PhotoFullscreen` while toolbar / launcher / roll-call windows were visible.
- Effect: the photo overlay kept reclaiming foreground activation, while floating utilities were later retouched back above it, causing repeated overlap-region front/back contention.

## Changes
1. Added `photoModeActive` gate in `FloatingTopmostWatchdogPolicy.ShouldForceRetouch`.
2. Wired `photoModeActive` from main lifecycle watchdog tick and z-order enforcement path.
3. Added unit tests for policy behavior in photo mode.
4. Removed `PhotoFullscreen` from `FloatingTopmostApplyPolicy` launcher interactive-retouch surfaces to stop repeated force-enforce retouch during photo-mode overlap.
5. Added regression test: launcher visible + photo surface unchanged should not enforce z-order.
6. Added photo-surface activation gating in `FloatingTopmostPlanPolicy`: when toolbar / roll-call / launcher is visible, `PhotoFullscreen` no longer requests overlay activation.
7. Kept whiteboard activation behavior unchanged so board mode still raises overlay as the teaching surface.
8. Updated coordinator and plan-policy tests to lock the new photo-mode activation contract.
9. Added execution-plan level photo ordering replay: when photo mode is active and z-order enforcement runs, overlay is replayed first and floating utilities are reapplied above it in the same execution pass.
10. Added execution-plan / executor regression coverage for the explicit "photo below floating utilities" ordering contract.

## Commands
1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. `codex --version`
5. `codex --help`
6. `codex status`
7. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~FloatingTopmostPlanPolicyTests|FullyQualifiedName~FloatingWindowCoordinatorTests|FullyQualifiedName~FloatingWindowActivationPolicyTests|FullyQualifiedName~FloatingTopmostApplyPolicyTests"`
8. `powershell -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
9. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~FloatingWindowExecutionExecutorTests|FullyQualifiedName~FloatingWindowExecutionPlanPolicyTests|FullyQualifiedName~FloatingWindowCoordinatorTests|FullyQualifiedName~FloatingTopmostPlanPolicyTests|FullyQualifiedName~FloatingTopmostApplyPolicyTests"`

## Key output
- build: success, 0 errors
- full test: passed 3386
- contract/invariant subset: passed 28
- codex version: `codex-cli 0.122.0`
- codex help: command help rendered successfully
- codex status: failed with `stdin is not a terminal` (non-interactive session)
- targeted regression tests (`FloatingTopmostPlanPolicyTests|FloatingWindowCoordinatorTests|FloatingWindowActivationPolicyTests|FloatingTopmostApplyPolicyTests`): passed 25
- targeted ordering regression tests (`FloatingWindowExecutionExecutorTests|FloatingWindowExecutionPlanPolicyTests|FloatingWindowCoordinatorTests|FloatingTopmostPlanPolicyTests|FloatingTopmostApplyPolicyTests`): passed 29
- hotspot line budget: pass

## Minimal diagnostics matrix
- cmd: `codex --version`
  - exit_code: `0`
  - key_output: `codex-cli 0.122.0`
  - timestamp: `2026-04-21 21:15:03 +08:00`
- cmd: `codex --help`
  - exit_code: `0`
  - key_output: `Codex CLI usage/help text printed`
  - timestamp: `2026-04-21 21:15:04 +08:00`
- cmd: `codex status`
  - exit_code: `1`
  - key_output: `Error: stdin is not a terminal`
  - timestamp: `2026-04-21 21:15:05 +08:00`

## Hotspot review
- Reviewed `MainWindow.Lifecycle.cs`, `MainWindow.ZOrder.cs`, and `FloatingTopmostWatchdogPolicy.cs` for topmost trigger regression.
- Conclusion: watchdog retouch is now suppressed during photo mode while explicit/manual force-retouch path is retained.
- Reviewed `FloatingTopmostApplyPolicy.cs` launcher interactive retouch branch.
- Conclusion: photo surface no longer uses unconditional launcher retouch; presentation/whiteboard retouch behavior remains unchanged.
- Reviewed `FloatingTopmostPlanPolicy.cs` and `FloatingWindowCoordinatorTests.cs` for overlay-vs-floating-window foreground ownership.
- Conclusion: photo mode no longer re-activates overlay while visible floating utilities are intentionally expected to stay above the photo; whiteboard activation remains unchanged.
- Reviewed `FloatingWindowExecutionPlanPolicy.cs` and `FloatingWindowExecutionExecutor.cs` for runtime z-order replay sequencing.
- Conclusion: photo-mode z-order execution now replays overlay before utility-window topmost application, making the final intended order explicit instead of relying on implicit activation side effects.

## N/A
- type: `platform_na`
  - reason: `codex status` requires an interactive terminal and cannot run in the current non-interactive command execution context.
  - alternative_verification: executed `codex --version` and `codex --help` successfully, and completed project hard gates (`build/test/contract`).
  - evidence_link: `docs/change-evidence/20260421-photo-overlap-topmost-flicker.md`
  - expires_at: `2026-05-21`

## Rollback
- Revert commit or restore these files:
  - `src/ClassroomToolkit.App/Windowing/FloatingTopmostWatchdogPolicy.cs`
  - `src/ClassroomToolkit.App/MainWindow.Lifecycle.cs`
  - `src/ClassroomToolkit.App/MainWindow.ZOrder.cs`
  - `tests/ClassroomToolkit.Tests/App/FloatingTopmostWatchdogPolicyTests.cs`
  - `src/ClassroomToolkit.App/Windowing/FloatingTopmostApplyPolicy.cs`
  - `tests/ClassroomToolkit.Tests/App/FloatingTopmostApplyPolicyTests.cs`
  - `src/ClassroomToolkit.App/Windowing/FloatingTopmostPlanPolicy.cs`
  - `src/ClassroomToolkit.App/Windowing/FloatingWindowExecutionPlanPolicy.cs`
  - `src/ClassroomToolkit.App/Windowing/FloatingWindowExecutionExecutor.cs`
  - `tests/ClassroomToolkit.Tests/App/FloatingTopmostPlanPolicyTests.cs`
  - `tests/ClassroomToolkit.Tests/App/FloatingWindowCoordinatorTests.cs`
  - `tests/ClassroomToolkit.Tests/App/FloatingWindowExecutionPlanPolicyTests.cs`
  - `tests/ClassroomToolkit.Tests/App/FloatingWindowExecutionExecutorTests.cs`
