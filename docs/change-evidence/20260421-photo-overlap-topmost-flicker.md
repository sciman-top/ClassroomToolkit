# 2026-04-21 Photo overlap topmost flicker fix

- rule_id: R1/R2/R6/R8
- risk_level: medium
- issue: Student photo display flickers repeatedly when overlapping toolbar/launcher/roll-call windows.

## Basis
- Observed code path: `FloatingTopmostWatchdogPolicy.ShouldForceRetouch` returned true whenever any floating utility window was visible, even during photo mode.
- Effect: watchdog (`700ms`) repeatedly triggered `RequestApplyZOrderPolicy(forceEnforceZOrder: true)` and forced topmost retouch calls.

## Changes
1. Added `photoModeActive` gate in `FloatingTopmostWatchdogPolicy.ShouldForceRetouch`.
2. Wired `photoModeActive` from main lifecycle watchdog tick and z-order enforcement path.
3. Added unit tests for policy behavior in photo mode.
4. Removed `PhotoFullscreen` from `FloatingTopmostApplyPolicy` launcher interactive-retouch surfaces to stop repeated force-enforce retouch during photo-mode overlap.
5. Added regression test: launcher visible + photo surface unchanged should not enforce z-order.

## Commands
1. `dotnet build ClassroomToolkit.sln -c Debug`
2. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
3. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
4. `codex --version`
5. `codex --help`
6. `codex status`

## Key output
- build: success, 0 errors
- full test: passed 3385
- contract/invariant subset: passed 28
- codex version: `codex-cli 0.122.0`
- codex help: command help rendered successfully
- codex status: failed with `stdin is not a terminal` (non-interactive session)
- targeted regression tests (`FloatingTopmostApplyPolicyTests|FloatingTopmostWatchdogPolicyTests`): passed 11
- isolated-output build (`BaseOutputPath=artifacts/tmp`): success, 0 errors
- isolated-output full test: failed 1 (`BrushDpiGoldenRegressionTests` baseline path coupling with relocated output)
- isolated-output contract/invariant subset: passed 28

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

## N/A
- type: `platform_na`
  - reason: `codex status` requires an interactive terminal and cannot run in the current non-interactive command execution context.
  - alternative_verification: executed `codex --version` and `codex --help` successfully, and completed project hard gates (`build/test/contract`).
  - evidence_link: `docs/change-evidence/20260421-photo-overlap-topmost-flicker.md`
  - expires_at: `2026-05-21`
- type: `gate_na`
  - reason: standard output path gate commands are blocked by file locks (`sciman Classroom Toolkit` process and Visual Studio hold `src/ClassroomToolkit.App/bin/Debug/net10.0-windows/sciman Classroom Toolkit*.{exe,dll}`), causing `MSB3021/MSB3027`.
  - alternative_verification: validated policy regression tests and contract subset with isolated output path (`-p:BaseOutputPath=D:\CODE\ClassroomToolkit\artifacts\tmp\`); isolated build succeeded.
  - evidence_link: `docs/change-evidence/20260421-photo-overlap-topmost-flicker.md`
  - expires_at: `2026-04-28`

## Rollback
- Revert commit or restore these files:
  - `src/ClassroomToolkit.App/Windowing/FloatingTopmostWatchdogPolicy.cs`
  - `src/ClassroomToolkit.App/MainWindow.Lifecycle.cs`
  - `src/ClassroomToolkit.App/MainWindow.ZOrder.cs`
  - `tests/ClassroomToolkit.Tests/App/FloatingTopmostWatchdogPolicyTests.cs`
  - `src/ClassroomToolkit.App/Windowing/FloatingTopmostApplyPolicy.cs`
  - `tests/ClassroomToolkit.Tests/App/FloatingTopmostApplyPolicyTests.cs`
