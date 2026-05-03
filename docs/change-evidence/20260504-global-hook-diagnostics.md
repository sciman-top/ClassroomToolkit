# 2026-05-04 GlobalHookService diagnostics

## Scope
- Rules: R2, R3, R6, R8, E4
- Risk: low
- Boundary: `GlobalHookService` recoverable diagnostics and lifecycle contract tests only.
- Current landing: `src/ClassroomToolkit.Services/Input/GlobalHookService.cs`
- Target home: Hook/Interop lifecycle diagnostics remain best-effort and do not change public behavior.

## Basis
- Existing Hook/Interop lifecycle baseline passed before the change:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~GlobalHookServiceTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests"`
- Result: passed, 26 tests.

## Change
- Added exception type to recoverable `Debug.WriteLine` diagnostics for hook start, binding registration, hook stop, binding callback, and `HookUnavailable` callback failures.
- Extended lifecycle contract coverage to preserve stop-hook diagnostic exception type output.

## Verification
- Targeted Hook/Interop test:
  `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~GlobalHookServiceTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests"`
- Result: passed, 27 tests.
- Full gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug` passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug` passed, 3480 tests.
  - contract/invariant: passed, 29 tests.
  - hotspot: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1` passed.
  - analyzer baseline: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug` passed, total=0.
  - diff check: `git diff --check` reported only LF/CRLF worktree warnings, no whitespace errors.

## Hotspot review
- No Hook registration order change.
- No Stop/Dispose lifecycle order change.
- No public API, persisted data, settings, or external file format change.
- UI behavior remains unchanged; diagnostics are emitted only on recoverable failure paths.

## Rollback
- Revert:
  - `src/ClassroomToolkit.Services/Input/GlobalHookService.cs`
  - `tests/ClassroomToolkit.Tests/GlobalHookServiceLifecycleContractTests.cs`
  - `docs/change-evidence/20260504-global-hook-diagnostics.md`
