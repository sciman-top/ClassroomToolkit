# 2026-04-22 Systematic Code Review Low-Risk Hardening

## Summary

- scope: `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
- scope: `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
- scope: `tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs`
- scope: `tests/ClassroomToolkit.Tests/FileLoggerProviderTests.cs`
- target destination: keep existing business behavior and external data formats unchanged while fixing low-risk correctness/performance issues in cache refresh and retention time-source consistency.
- risk level: low

## Rule Mapping

- `R1`: current landing point was photo cache refresh + log retention clock source; target destination was low-risk hardening without contract changes.
- `R2`: implemented as two small slices, each followed by targeted tests.
- `R3`: avoided stopgap patches; both changes address root causes instead of masking symptoms.
- `R6`: executed `build -> test -> contract/invariant -> hotspot` in fixed order.
- `R8`: this file records `basis -> command -> evidence -> rollback`.

## Basis

1. `StudentPhotoResolver.ResolvePhotoPath` dropped the whole directory cache when a warm-cache miss discovered a newly added preferred-extension photo. That forced later requests for the same class back into direct probes or full index rebuilds.
2. `FileLoggerProvider.TryApplyRetention` used `DateTime.Now` unless `retentionNow` was injected, while log timestamps already respected `nowProvider`. That split runtime behavior from the provider clock abstraction and made retention behavior inconsistent under injected clocks.
3. Dependency baseline:
   - vulnerable packages: none reported by `dotnet list ... --vulnerable --include-transitive`
   - deprecated packages: test project still uses legacy `xunit 2.9.3`, replacement suggested by NuGet is `xunit.v3`

## Commands And Evidence

### Platform diagnostics

- cmd: `codex --version`
  - exit_code: `0`
  - key_output: `codex-cli 0.122.0`
- cmd: `codex --help`
  - exit_code: `0`
  - key_output: CLI help displayed successfully.
- cmd: `codex status`
  - exit_code: `1`
  - platform_na:
    - reason: non-interactive environment returned `stdin is not a terminal`
    - alternative_verification: used explicit `codex --version`, `codex --help`, repository `AGENTS.md`, and local workspace inspection instead
    - evidence_link: `docs/change-evidence/20260422-systematic-code-review-low-risk-hardening.md`
    - expires_at: `2026-04-22`

### Baseline review

- cmd: `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: `0`
  - key_output: `0 warnings`, `0 errors`
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: `0`
  - key_output: `3403` baseline tests passed before changes; `3405` tests passed after changes
- cmd: `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive`
  - exit_code: `0`
  - key_output: no vulnerable packages across all projects
- cmd: `dotnet list ClassroomToolkit.sln package --deprecated`
  - exit_code: `0`
  - key_output: only deprecated package reported was legacy `xunit 2.9.3` in `ClassroomToolkit.Tests`

### Slice 1: student photo cache refresh

- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~StudentPhotoResolverTests"`
  - exit_code: `0`
  - key_output: `20` tests passed
- evidence:
  - warm-cache direct hit now merges the new student photo path back into the cached directory index instead of removing the whole directory cache
  - regression test added to verify cached directory index retains both old and newly added student photos

### Slice 2: logger retention clock consistency

- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~FileLoggerProviderTests"`
  - exit_code: `0`
  - key_output: `17` tests passed
- evidence:
  - retention reference time now resolves from `retentionNow` or provider `nowProvider`
  - regression test added to verify retention still prunes expired logs when only `nowProvider` is supplied

## Hard Gates

- cmd: `dotnet build ClassroomToolkit.sln -c Debug`
  - exit_code: `0`
  - key_output: success
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - exit_code: `0`
  - key_output: `3405` passed
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: `0`
  - key_output: `28` contract/invariant tests passed
- cmd: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code: `0`
  - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

## Hotspot Review

- file: `src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs`
  - conclusion: change is local to warm-cache miss recovery; no external path format, sanitization rule, or dispose semantics changed.
- file: `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`
  - conclusion: retention now follows the same time-source abstraction as log timestamping; shutdown, queue, and file layout logic remain unchanged.

## Rollback

- rollback command:
  - `git restore --source=HEAD -- src/ClassroomToolkit.App/Photos/StudentPhotoResolver.cs src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs tests/ClassroomToolkit.Tests/StudentPhotoResolverTests.cs tests/ClassroomToolkit.Tests/FileLoggerProviderTests.cs docs/change-evidence/20260422-systematic-code-review-low-risk-hardening.md`

## Deferred / Not Changed

- dependency upgrades were reviewed but not auto-applied because they cross compatibility boundaries:
  - multiple `Microsoft.Extensions.*` packages are behind latest `10.0.7`
  - `System.Speech` is behind `10.0.7`
  - test stack includes legacy `xunit 2.9.3`
- no storage format, workbook schema, `students.xlsx`, `student_photos/`, or `settings.ini` behavior was changed.
