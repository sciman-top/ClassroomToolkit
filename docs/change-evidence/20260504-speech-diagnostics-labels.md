# 2026-05-04 Speech diagnostics labels

## Scope
- Rules: R2, R3, R6, R8, E4
- Risk: low
- Boundary: speech service diagnostic string cleanup only.
- Current landing:
  - `src/ClassroomToolkit.Services/Speech/SpeechService.cs`
  - `tests/ClassroomToolkit.Tests/SpeechServiceLifecycleContractTests.cs`
- Target home: speech failures remain non-fatal where recoverable and Debug diagnostics include enough exception context for classroom troubleshooting.

## Basis
- P2 Task 8: local duplicate diagnostic string cleanup.
- Existing `SpeechService` failure paths repeated `[SpeechService] ... failed: {ex.Message}` and omitted exception type, making recoverable speech and callback failures harder to classify.
- This slice does not change public API, event behavior, threading, persisted data, settings, or speech fallback semantics.

## Change
- Added a private `FormatDiagnostic` helper in `SpeechService`.
- Routed speak failure, unavailable callback failure, cancel failure, and dispose failure diagnostics through the helper.
- Added a source contract test that locks the diagnostic helper and four call sites.

## Verification
- Targeted speech tests:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~SpeechService"`
- Result: passed, 12 tests.
- Full gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed, 3482 tests.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"` passed, 29 tests.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` passed.
  - analyzer baseline: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug` passed, total=0.
  - diff check: `git diff --check` reported only LF/CRLF worktree warnings, no whitespace errors.
- Note: one parallel contract/invariant attempt collided with concurrent analyzer/build writes under `obj/Debug`; the same contract command passed when rerun standalone in the required fixed gate order.

## Hotspot review
- Recoverable `System.Speech.Synthesis` failures are still swallowed and notify `SpeechUnavailable` at most once per unavailable run.
- Fatal callback exceptions still rethrow through the existing `IsNonFatal` filter.
- Diagnostics now include `exception type + message` without adding user-visible dialogs or dependencies.

## Rollback
- Revert:
  - `src/ClassroomToolkit.Services/Speech/SpeechService.cs`
  - `tests/ClassroomToolkit.Tests/SpeechServiceLifecycleContractTests.cs`
  - `docs/change-evidence/20260504-speech-diagnostics-labels.md`
