# 2026-05-04 Hook Interop lifecycle closeout

## Scope
- Rules: R2, R3, R6, R8, E4
- Risk: low
- Boundary: Hook/Interop lifecycle evidence and backlog status only.
- Current landing:
  - `src/ClassroomToolkit.Services/Input/GlobalHookService.cs`
  - `src/ClassroomToolkit.Interop/Presentation/KeyboardHook*.cs`
  - `src/ClassroomToolkit.Interop/Presentation/WpsSlideshowNavigationHook*.cs`
- Target home: hook lifecycle behavior remains idempotent, unsubscribed on stop/dispose, and minimally diagnosable.

## Basis
- P1 backlog item: Hook/Interop lifecycle boundary review.
- Existing code and contract evidence:
  - `GlobalHookService` drains active hooks before stop and clears active hook state under lock.
  - Registration failure paths unsubscribe callbacks and call cleanup for already-started hooks.
  - `TryStopHook` isolates recoverable dispose failures and logs exception type plus message.
  - `KeyboardHook` and `WpsSlideshowNavigationHook` contract tests cover accept/intercept gates, subscriber clearing, unhook failure diagnostics, and start failure diagnostics.

## Change
- Marked P1 Task 5 acceptance criteria and verification complete in `docs/tech-debt-backlog.md`.
- No production code change in this closeout slice.

## Verification
- Targeted Hook/Interop lifecycle test:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~GlobalHookServiceTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests"`
- Result: passed, 27 tests.
- Full gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed, 3481 tests.
  - contract/invariant: passed, 29 tests.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` passed.
  - analyzer baseline: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug` passed, total=0.
  - diff check: `git diff --check` reported only LF/CRLF worktree warnings, no whitespace errors.

## Hotspot review
- `GlobalHookService`: `Dispose`, `UnregisterAll`, registration abort, registration failure, and inactive hook rollback all stop started hooks.
- `KeyboardHook`: stop clears subscribers and target binding, disables event acceptance, and records unhook errors.
- `WpsSlideshowNavigationHook`: stop disables intercept before dispatch generation bump, clears subscribers after dispose, and records keyboard/mouse unhook errors.
- No public API, UI, settings, data, or Interop hook behavior change in this slice.

## Rollback
- Revert:
  - `docs/tech-debt-backlog.md`
  - `docs/change-evidence/20260504-hook-interop-lifecycle-closeout.md`
