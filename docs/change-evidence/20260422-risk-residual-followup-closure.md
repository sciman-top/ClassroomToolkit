# 2026-04-22 Risk Residual Follow-up Closure

## Scope
- Boundary:
  - `src/ClassroomToolkit.App/Diagnostics/DiagnosticsBundleExportService.cs`
  - `tests/ClassroomToolkit.Tests/DiagnosticsBundleExportServiceTests.cs`
  - `scripts/validation/run-stable-tests.ps1`
  - `scripts/validation/validate-stable-test-config.ps1`
- Current landing: risk/residual items from previous systematic review.
- Target landing: close low/medium residual risks with safe, incremental, backward-compatible changes.

## Changes
1. Diagnostics bundle integrity hardening
- Added entry-name allowlist validation (`settings/*`, `logs/startup-compatibility-latest.json`, `logs/error_*.log`, `diagnostics/diagnostics-summary.txt`).
- Added optional source file size guard (`MaxOptionalSourceFileBytes = 8 MB`) to skip oversized files while keeping export successful.
- Added resilient file-length read fallback and non-fatal skip behavior.

2. Regression tests expanded
- Added oversized optional file regression test to verify export degradation path is safe.
- Added allowlist policy tests for diagnostics bundle entry names.

3. Stable-tests pipeline closure
- Reintroduced missing scripts:
  - `scripts/validation/run-stable-tests.ps1`
  - `scripts/validation/validate-stable-test-config.ps1`
- Enabled profile semantics:
  - `quick`: stable critical subset (101 tests on current baseline)
  - `standard`: full test pass
  - `full`: full test pass
- Added summary output (`artifacts/TestResults/stable-tests-summary.json`).
- Added summary write anti-race fallback (retry + unique fallback path) to avoid false gate failures under file-lock races.

## Dependency governance follow-up
- `dotnet list ... --outdated --include-transitive` still reports:
  - `Microsoft.Testing.* 1.9.1 -> 2.2.1`
  - `Microsoft.ApplicationInsights 2.23.0 -> 3.1.0`
- Root cause: current stable `xunit.v3` line (`3.2.2`) pins `xunit.v3.core.mtp-v1` which in turn pins `Microsoft.Testing.* 1.9.1` in lockfile; no stable `xunit.v3` upgrade is currently available on configured feeds.
- Action taken: do not force unsafe transitive override; keep compatibility and close with validated gate evidence.

## Commands and key outputs
1. `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~DiagnosticsBundleExportServiceTests"`
- exit_code: 0
- key_output: passed `12` tests.

2. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/validation/validate-stable-test-config.ps1`
- exit_code: 0
- key_output: quick/standard/full dry-run validation pass.

3. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick`
- exit_code: 0
- key_output: build pass; stable-tests(quick)=101 pass; contract=28 pass; hotspot pass.

4. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard`
- exit_code: 0
- key_output: build pass; stable-tests(standard)=3416 pass; contract=28 pass; hotspot pass.

5. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile full`
- exit_code: 0
- key_output: build pass; stable-tests(full)=3416 pass; contract=28 pass; hotspot pass.

6. `dotnet list tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj package --outdated --include-transitive`
- exit_code: 0
- key_output: transitive `Microsoft.Testing.*` and `Microsoft.ApplicationInsights` updates still available.

7. `dotnet list tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj package --outdated --include-transitive --include-prerelease`
- exit_code: 0
- key_output: newer lines are currently pre-release for top-level test stack.

8. Hot-path baseline probes (wall-clock)
- `RollCallViewModelPreloadConcurrencyTests`: ~12614 ms command wall time, test payload duration ~1 s.
- `ImageManagerThumbnailCacheWarmupContractTests`: ~11758 ms command wall time, test payload duration ~38 ms.
- `WindowInteropRetryExecutorTests|InteropBackgroundDispatchExecutorTests`: ~11337 ms command wall time, test payload duration ~63 ms.

## Risk status
- Diagnostics export residual risk: CLOSED (integrity guard + regression tests).
- Stable-tests profile-chain residual risk: CLOSED (missing script gap filled + validated).
- Test-platform transitive upgrade residual risk: PARTIALLY CLOSED (risk understood + validated; awaiting stable upstream line for safe migration).

## Rollback
- `git restore --source=HEAD~1 -- src/ClassroomToolkit.App/Diagnostics/DiagnosticsBundleExportService.cs tests/ClassroomToolkit.Tests/DiagnosticsBundleExportServiceTests.cs scripts/validation/run-stable-tests.ps1 scripts/validation/validate-stable-test-config.ps1`
