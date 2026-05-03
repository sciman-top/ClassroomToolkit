# 2026-05-04 Roll call speech wrapper cleanup

## Scope
- Rules: R2, R5, R6, R8, E4
- Risk: low
- Boundary: roll call speech unavailable event wiring only.
- Current landing:
  - `src/ClassroomToolkit.App/RollCallWindow.xaml.cs`
  - `src/ClassroomToolkit.App/RollCallWindow.Windowing.cs`
  - `src/ClassroomToolkit.App/RollCallWindow.State.cs`
  - `tests/ClassroomToolkit.Tests/App/RollCallWindowLifecycleSubscriptionContractTests.cs`
  - `docs/tech-debt-backlog.md`
- Target home: remove a local one-line wrapper while preserving symmetric event subscription cleanup.

## Basis
- P2 Task 8: local dead code and low-value wrapper cleanup.
- `OnSpeechUnavailable()` only forwarded to `NotifySpeechError()` and had no independent logic.
- `SpeechUnavailable` is an `Action`, so direct subscription to `NotifySpeechError` preserves the same call shape.

## Change
- Replaced `_speechService.SpeechUnavailable += OnSpeechUnavailable` with direct `NotifySpeechError` subscription.
- Replaced matching unsubscribe with direct `NotifySpeechError` unsubscribe.
- Removed the now-unused `OnSpeechUnavailable()` wrapper.
- Updated lifecycle source contract to require direct subscription and to reject the removed wrapper name.
- Marked P2 Task 8 complete in `docs/tech-debt-backlog.md`.

## Verification
- Targeted contract tests:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~RollCallWindowLifecycleSubscriptionContractTests|FullyQualifiedName~RollCallSpeechDispatchFallbackContractTests"`
- Result: passed, 5 tests.
- Residual reference check:
  `rg -n "OnSpeechUnavailable" src -g "*.cs"` returned no production source matches.
- Full gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed, 3482 tests.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` passed, 29 tests.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` passed.
  - analyzer baseline: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug` passed, total=0.
  - diff check: `git diff --check` reported only LF/CRLF worktree warnings, no whitespace errors.
- Note: one parallel contract/invariant attempt collided with concurrent analyzer/build writes under `obj/Debug`; the same contract command passed when rerun standalone in the required fixed gate order.

## Hotspot review
- Event subscription and unsubscription remain symmetric.
- Speech unavailable notification still flows through `NotifySpeechError()`.
- No public API, persisted data, settings format, UI text, dependency, or threading model change.
- Net source diff reduces this slice by 3 lines.

## Rollback
- Revert:
  - `src/ClassroomToolkit.App/RollCallWindow.xaml.cs`
  - `src/ClassroomToolkit.App/RollCallWindow.Windowing.cs`
  - `src/ClassroomToolkit.App/RollCallWindow.State.cs`
  - `tests/ClassroomToolkit.Tests/App/RollCallWindowLifecycleSubscriptionContractTests.cs`
  - `docs/tech-debt-backlog.md`
  - `docs/change-evidence/20260504-rollcall-speech-wrapper-cleanup.md`
