# 2026-04-22 Diagnostics Bundle Export Hardening

## Scope
- Boundary: `src/ClassroomToolkit.App/Diagnostics` + corresponding tests.
- Current landing: diagnostics bundle generation and log selection reliability.
- Target landing: preserve export success under filename collisions and partial file-access failures without changing external UX or data format.

## Risk
- Level: Low
- Reason: additive hardening only; no public API contract break; existing entry `Export(DiagnosticsResult)` preserved.

## Rule Mapping
- R1: declared landing + target before edits.
- R2: small-step: code change -> focused tests -> full gates.
- R3: root-cause fix for collision and partial IO failures (not UI-layer retry patch).
- R6: executed hard gates in fixed order.
- R8: evidence includes basis, commands, outputs, rollback.

## Platform / N-A
- `platform_na`
  - reason: `codex status` requires interactive terminal and failed with `stdin is not a terminal`.
  - alternative_verification: used `codex --version` and `codex --help` as platform diagnostics baseline.
  - evidence_link: terminal output in this task session + this document.
  - expires_at: 2026-05-22.

## Commands And Key Outputs
1. `codex --version`
- exit_code: 0
- key_output: `codex-cli 0.122.0`

2. `codex --help`
- exit_code: 0
- key_output: CLI help printed normally.

3. `codex status`
- exit_code: 1
- key_output: `Error: stdin is not a terminal`

4. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~DiagnosticsBundleExportServiceTests"`
- exit_code: 0
- key_output: passed 4 tests.

5. `dotnet build ClassroomToolkit.sln -c Debug`
- exit_code: 0
- key_output: 0 warnings, 0 errors.

6. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
- exit_code: 0
- key_output: passed 3408 tests.

7. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
- exit_code: 0
- key_output: passed 28 contract/invariant tests.

8. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
- exit_code: 0
- key_output: hotspot PASS (`max=1200`).

9. `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive`
- exit_code: 0
- key_output: no vulnerable packages found.

10. `dotnet list ClassroomToolkit.sln package --outdated --include-transitive`
- exit_code: 0
- key_output: runtime projects up-to-date; test transitive updates available (Microsoft.Testing.* / ApplicationInsights).

## Rollback
- Revert this slice only:
  - `git restore --source=HEAD~1 -- src/ClassroomToolkit.App/Diagnostics/DiagnosticsBundleExportService.cs tests/ClassroomToolkit.Tests/DiagnosticsBundleExportServiceTests.cs`
- Expected effect: diagnostics export behavior returns to previous semantics.
