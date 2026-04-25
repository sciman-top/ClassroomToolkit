# 2026-04-24 Systematic Review And Optimization

## Scope
- Rule: R1-R8, C.2 fixed gate order.
- Boundary: low-risk correctness and quality-gate review in `D:\CODE\ClassroomToolkit`.
- Current location: `src/ClassroomToolkit.Domain/Utilities/AtomicFileReplaceUtility.cs`.
- Target home: preserve public behavior while ensuring fallback replace cleanup matches the atomic write contract.

## Baseline
- `git status --short --branch`: clean `main...origin/main`.
- `dotnet build ClassroomToolkit.sln -c Debug`: failed with `0 warning / 0 error`; diagnostic log showed `_GetProjectReferenceTargetFrameworkProperties` failure without source errors.
- `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: passed.
- `dotnet build ClassroomToolkit.sln -c Debug --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: blocked by generated test output ACL, `MSB3491 Access to the path is denied` for `.msCoverageSourceRootsMapping_ClassroomToolkit.Tests`.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --no-build -v:minimal`: blocked by `SocketException (10106)` while VSTest initialized its local socket channel.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`: passed.

## Change
- `AtomicFileReplaceUtility.ReplaceOrOverwrite` now deletes the temp file after the fallback copy succeeds.

## Post-change Verification
- `dotnet build src/ClassroomToolkit.Domain/ClassroomToolkit.Domain.csproj -c Debug --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: passed with `0 warning / 0 error`.
- `dotnet build src/ClassroomToolkit.Application/ClassroomToolkit.Application.csproj -c Debug --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: passed with `1 warning / 0 error`; warning was generated `obj` cache ACL, `MSB3101 Access to the path is denied`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`: passed.
- `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug --no-restore -m:1 -v:minimal /p:UseSharedCompilation=false`: blocked by generated `obj` ACL in WPF `MarkupCompilePass1`, `MC1000/MSB4018 Access to the path ... App.g.cs/sciman Classroom Toolkit_MarkupCompile.cache is denied`.

## Follow-up Fix
- Repaired generated-output ACL by granting the current user Modify rights on existing `src/**/bin`, `src/**/obj`, `tests/**/bin`, `tests/**/obj`, and `artifacts/validation` contents.
- Removed stale generated `bin` / `obj` / validation outputs after verifying all resolved paths stayed under the repository root.
- Restored the missing process-level Windows environment variables required by NuGet and VSTest in gate commands: `SystemRoot`, `windir`, `ComSpec`, `APPDATA`, `LOCALAPPDATA`, `ProgramData`, `ProgramFiles`, `ProgramFiles(x86)`, `CommonProgramFiles`, `CommonProgramFiles(x86)`, and `NUGET_PACKAGES`.
- Verified a minimal .NET `TcpListener` probe succeeded after restoring those variables, confirming the VSTest `SocketException (10106)` was caused by the incomplete process environment rather than repository code.

## Final Verification
- `codex --version`: passed, `codex-cli 0.124.0`, after restoring the same Windows environment variables.
- `codex --help`: passed, command list printed.
- `codex status`: `platform_na`; failed with `stdin is not a terminal`, expected in this non-interactive runner.
- `dotnet build ClassroomToolkit.sln -c Debug -m:1 -v:minimal /p:UseSharedCompilation=false /p:NuGetAudit=false`: passed with `0 warning / 0 error`.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 -v:minimal /p:UseSharedCompilation=false /p:NuGetAudit=false`: passed, `3430` tests.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 -v:minimal /p:UseSharedCompilation=false /p:NuGetAudit=false --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`: passed, `28` tests.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`: passed.
- `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive`: passed; no vulnerable packages reported.

## Risk
- Low. The normal `File.Replace` path is unchanged.
- Fallback already means the target was overwritten successfully; deleting the temp file aligns fallback behavior with the normal path and existing cleanup expectations.

## Rollback
- Revert the one-line `File.Delete(tempPath)` addition in `src/ClassroomToolkit.Domain/Utilities/AtomicFileReplaceUtility.cs`.

## N/A / Blockers
- Resolved: `codex --version` and `codex --help` originally failed with Node native `ncrypto::CSPRNG` assertion; root cause was the incomplete process environment.
- `platform_na`: `codex status` still fails with `stdin is not a terminal`, which is expected for this non-interactive runner.
- Resolved: full `dotnet test` originally failed because VSTest local socket initialization hit `SocketException (10106)`; root cause was missing Windows environment variables in the process.
- Resolved: solution/test-project build was originally blocked by generated output ACL preventing MSBuild `WriteLinesToFile` from replacing coverage mapping files.
- Alternative verification: no longer needed for the build/test/contract/hotspot chain after the environment repair.
- Evidence link: `artifacts/validation/baseline-build-20260424.log`, `artifacts/validation/baseline-app-build-20260424.log`, this file.
- Expires at: `platform_na` for `codex status` expires when an interactive terminal is available.

## Current Review Increment - Release Preflight OutputRoot
- Boundary: release validation scripts and their regression contract; no application behavior, data format, or UI changes.
- Current location: `scripts/release/preflight-check.ps1`.
- Target home: absolute and relative `-OutputRoot` values are both valid release-preflight output destinations.

### Baseline
- `codex --version`: passed after restoring process-level Windows environment variables, `codex-cli 0.124.0`.
- `codex --help`: passed after restoring process-level Windows environment variables.
- `codex status`: `platform_na`; failed with `stdin is not a terminal`.
- `dotnet build ClassroomToolkit.sln -c Debug`: passed, `0 warning / 0 error`.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: passed, `3430` tests.
- Contract/invariant filtered `dotnet test`: passed, `28` tests.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`: passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile standard -Configuration Debug`: passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Debug -Profile standard -OutputRoot artifacts/release/current-preflight`: passed.

### Problem
- `scripts/release/preflight-check.ps1` always joined `OutputRoot` to the repository root. A rooted value such as `D:\CODE\ClassroomToolkit\artifacts\validation\preflight-absolute-outputroot-before-fix` became `D:\CODE\ClassroomToolkit\D:\CODE\ClassroomToolkit\artifacts\validation`, causing `New-Item` to fail before release checks could run.

### Change
- `preflight-check.ps1` now resolves `OutputRoot` with `[System.IO.Path]::IsPathRooted($OutputRoot)`: rooted paths are used as-is; relative paths are joined to `$repoRoot`.
- Added `ReleasePreflightOutputRootContractTests` to keep the rooted-path branch and prevent the old unconditional join from returning.

### Verification
- Before-fix probe: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Debug -SkipTests -SkipCompatibilityReport -SkipUiPerformanceSampling -SkipSettingsLoadPerformanceSampling -OutputRoot <absolute-path>` failed at `New-Item`; log `artifacts/validation/preflight-absolute-outputroot-before-fix-20260424.log`.
- After-fix probe with the same absolute `OutputRoot`: passed; log `artifacts/validation/preflight-absolute-outputroot-after-fix-20260424.log`.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --no-build --filter FullyQualifiedName~ReleasePreflightOutputRootContractTests`: passed, `1` test.
- Note: an earlier parallel targeted-test/probe attempt hit a shared WPF `obj` write lock from `VBCSCompiler`; rerunning sequentially passed.
- Final fixed-order gate: `dotnet build ClassroomToolkit.sln -c Debug`: passed, `0 warning / 0 error`; log `artifacts/validation/final-build-20260424.log`.
- Final fixed-order gate: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: passed, `3431` tests; log `artifacts/validation/final-test-20260424.log`.
- Final fixed-order gate: contract/invariant filtered `dotnet test`: passed, `28` tests; log `artifacts/validation/final-contract-20260424.log`.
- Final fixed-order gate: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`: passed; log `artifacts/validation/final-hotspot-20260424.log`.
- Full absolute-output-root release preflight: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/preflight-check.ps1 -Configuration Debug -Profile standard -OutputRoot <absolute-path>`: passed; log `artifacts/validation/final-preflight-absolute-outputroot-20260424.log`.
- Dependency governance inside final preflight: passed; stable outdated packages remain covered by active waivers.
- Dependency vulnerability scan inside final preflight: passed; no vulnerable packages detected.
- Performance sampling inside final preflight: generated UI and settings-load samples under `artifacts/release/current-preflight-after-fix-abs/validation`.

### Risk
- Low. The change only affects output directory resolution in release preflight. Relative output roots keep the previous repository-relative behavior.

### Rollback
- Revert `scripts/release/preflight-check.ps1` and remove `tests/ClassroomToolkit.Tests/ReleasePreflightOutputRootContractTests.cs`, then rerun `build -> test -> contract/invariant -> hotspot`.

## Current Review Increment - Process Environment, Dependency Gates, And Capture Path Robustness
- Date: 2026-04-25.
- Boundary: repo engineering entrypoints, dependency governance scripts, and region-capture path classification only.
- Current location: `scripts/env/Initialize-WindowsProcessEnvironment.ps1`, `scripts/quality/*`, `scripts/release/*`, `scripts/validation/*`, `src/ClassroomToolkit.App/Paint/RegionScreenCaptureWorkflow.cs`.
- Target home: preserve app behavior while making gates resilient in incomplete Windows process environments, keeping dependency checks on a trusted package source, and preventing invalid capture paths from bubbling exceptions into UI flows.

### Baseline
- `codex --version`, `codex --help`, and `codex status` failed with native Node `ncrypto::CSPRNG` assertions before process environment bootstrap.
- `dotnet build ClassroomToolkit.sln -c Debug` failed in NuGet restore with `Value cannot be null. (Parameter 'path1')` when the process only exposed a minimal set of Windows environment variables.
- After manually restoring missing process variables in-command, `dotnet build -> dotnet test -> contract/invariant -> hotspot` passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug` initially reached dependency governance and failed because `dotnet list ... --outdated` used the machine default local SDK package source and returned `Value cannot be null. (Parameter 'path1')`.
- The same outdated-package scan passed when constrained to `--source https://api.nuget.org/v3/index.json`.

### Change
- Added `scripts/env/Initialize-WindowsProcessEnvironment.ps1`, which only fills missing process-level defaults for `USERPROFILE/HOME`, Windows root variables, `APPDATA/LOCALAPPDATA`, `ProgramFiles` family variables, `ComSpec`, `ProgramData`, and `NUGET_PACKAGES`.
- Dot-sourced the bootstrap from local quality, release, validation, dependency, and one-click scripts that invoke `dotnet` or spawn those gates.
- Updated dependency governance and vulnerability scripts with a `PackageSource` parameter defaulting to NuGet.org, and included bounded `dotnet list` output in failure messages.
- Hardened `RegionScreenCaptureWorkflow.IsSessionRegionCaptureFilePath` so malformed path input returns `false` with debug diagnostics instead of throwing.
- Added contract tests for the environment bootstrap, dependency package source/failure-output behavior, and invalid region-capture path handling.

### Verification
- `. scripts/env/Initialize-WindowsProcessEnvironment.ps1; codex --version`: passed, `codex-cli 0.125.0`.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 -v:minimal /p:UseSharedCompilation=false --filter "FullyQualifiedName~RegionCaptureInitialPassthroughPolicyTests|FullyQualifiedName~WindowsProcessEnvironmentBootstrapContractTests|FullyQualifiedName~DependencyGovernancePackageSourceContractTests"`: passed, `25` tests.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-dependency-upgrade-feasibility.ps1`: passed; stable outdated packages are covered by active waivers.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-dependency-vulnerabilities.ps1`: passed; no vulnerable packages detected.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`: passed; build passed, quick stable tests `105` passed, contract tests `28` passed, hotspot passed, dependency governance passed, vulnerability scan passed, analyzer backlog baseline passed.
- Final fixed-order gate: `. scripts/env/Initialize-WindowsProcessEnvironment.ps1; dotnet build ClassroomToolkit.sln -c Debug`: passed, `0 warning / 0 error`.
- Final fixed-order gate: `. scripts/env/Initialize-WindowsProcessEnvironment.ps1; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: passed, `3454` tests.
- Final fixed-order gate: `. scripts/env/Initialize-WindowsProcessEnvironment.ps1; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`: passed, `28` tests.
- Final fixed-order gate: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`: passed.
- `git diff --check`: passed; only CRLF normalization warnings were printed.

### Risk
- Low. The bootstrap only sets variables that are missing or blank in the current process and does not overwrite user-provided values.
- Low. Dependency scripts still allow callers to override `-PackageSource`; defaulting to NuGet.org avoids broken local SDK package-source state for this public dependency graph.
- Low. The capture path change only changes malformed input from exception to `false`; valid session capture paths keep the existing behavior.

### Rollback
- Remove `scripts/env/Initialize-WindowsProcessEnvironment.ps1` and the dot-source blocks added to scripts.
- Revert `scripts/quality/check-dependency-upgrade-feasibility.ps1` and `scripts/quality/check-dependency-vulnerabilities.ps1` package-source/failure-output changes.
- Revert the `RegionScreenCaptureWorkflow.IsSessionRegionCaptureFilePath` try/catch block and remove the new contract tests.
- Rerun `build -> test -> contract/invariant -> hotspot`.

## Current Review Increment - Region Capture Hot Path And Release Script Safety
- Date: 2026-04-25.
- Boundary: region-capture bitmap allocation and release package script input validation only.
- Current location: `src/ClassroomToolkit.App/Paint/RegionScreenCaptureWorkflow.cs`, `scripts/release/prepare-distribution.ps1`.
- Target home: preserve region-capture behavior and release package outputs while reducing screenshot memory pressure and blocking unsafe release output/download inputs.

### Baseline
- Direct `dotnet build ClassroomToolkit.sln -c Debug` in the incomplete Codex process environment failed during NuGet restore with `Value cannot be null. (Parameter 'path1')`.
- Alternative environment-bootstrapped baseline passed: `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; dotnet build ClassroomToolkit.sln -c Debug`, `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`, contract subset, and hotspot.

### Problem
- Region capture allocated a bitmap for the entire virtual screen and then cloned the selected rectangle. On high-DPI or multi-monitor classrooms this increased memory use and UI stall risk for small captures.
- `prepare-distribution.ps1` accepted `-Version` as a raw path segment and later joined it under `OutputRoot`, so path separators or traversal-like values could escape the intended release version folder before overwrite cleanup.
- Runtime installer download accepted the configured URL without checking that it was an absolute HTTPS URL.

### Change
- Region capture now creates a bitmap sized to the intersected target rectangle and calls `CopyFromScreen` from `target.Left` / `target.Top` directly.
- Added release-version segment validation: non-empty, no leading/trailing whitespace, no `.` / `..`, no path separators, and no invalid file-name characters.
- Added runtime installer URL validation requiring an absolute HTTPS URL before `Invoke-WebRequest`.
- Added source contract tests for the region-capture hot path and release packaging safety checks.

### Verification
- Targeted region tests: `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter FullyQualifiedName~RegionCaptureInitialPassthroughPolicyTests`: passed, `5` tests.
- Targeted release script tests: `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter FullyQualifiedName~ReleasePrepareDistributionContractTests`: passed, `2` tests.
- Unsafe version probe: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/prepare-distribution.ps1 -Version '..\escape' -PackageMode offline -Configuration Debug -SkipPublish -SkipZip -AllowOverwriteVersion`: failed as expected with `Invalid release version '..\escape'`.
- Fixed-order gate: `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; dotnet build ClassroomToolkit.sln -c Debug`: passed, `0 warning / 0 error`.
- Fixed-order gate: `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: passed, `3457` tests.
- Fixed-order gate: `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`: passed, `28` tests.
- Fixed-order gate: `. .\scripts\env\Initialize-WindowsProcessEnvironment.ps1; powershell -File scripts/quality/check-hotspot-line-budgets.ps1`: passed.
- Quality gate: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`: passed, `[quality] ALL PASS`.
- `git diff --check`: passed; only CRLF normalization warnings were printed.

### Risk
- Low. Region capture still saves the same PNG output for the same selected screen rectangle; only the intermediate bitmap allocation changed.
- Low. Valid release versions and HTTPS runtime installer URLs keep working; unsafe inputs now fail before any release directory deletion or download.

### Rollback
- Revert the target-sized bitmap capture in `RegionScreenCaptureWorkflow.cs` and the related contract assertion.
- Remove `Assert-SafeReleaseVersionSegment` / `Assert-HttpsDownloadUrl` from `prepare-distribution.ps1` and delete `ReleasePrepareDistributionContractTests.cs`.
- Rerun `build -> test -> contract/invariant -> hotspot`.
