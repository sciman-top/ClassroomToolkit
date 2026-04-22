# 2026-04-22 Transitive Dependency Closure

## Summary

- scope: `src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj`
- scope: `tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
- scope: refreshed `packages.lock.json` files under `src/ClassroomToolkit.Infra/`, `src/ClassroomToolkit.App/`, and `tests/ClassroomToolkit.Tests/`
- target destination: close the remaining low-risk transitive dependency drift without changing business semantics, external contracts, or persisted data formats
- risk level: low

## Basis

1. After the direct dependency alignment slice, `dotnet list ClassroomToolkit.sln package --outdated --include-transitive` still reported:
   - `DocumentFormat.OpenXml 3.1.1 -> 3.5.1`
   - `DocumentFormat.OpenXml.Framework 3.1.1 -> 3.5.1`
   - `System.IO.Packaging 8.0.1 -> 10.0.7`
   - `Newtonsoft.Json 13.0.3 -> 13.0.4`
   - `Microsoft.Bcl.AsyncInterfaces 6.0.0 -> 10.0.7`
2. These packages are not classroom-facing behavior changes. They are compatibility-style runtime libraries with a lower upgrade risk than the remaining major-version drifts.
3. Other residuals were intentionally not auto-applied because they cross major version boundaries or are tightly coupled to upstream tooling:
   - `SQLitePCLRaw.* 2.1.11 -> 3.0.2`
   - `SixLabors.Fonts 1.0.0 -> 2.1.3`
   - `Microsoft.Testing.Platform* 1.9.1 -> 2.2.1`
   - `Microsoft.ApplicationInsights 2.23.0 -> 3.1.0`

## Rule Mapping

- `R1`: current landing point was transitive dependency residue; target destination was to close only the low-risk subset and keep high-risk package families isolated.
- `R2`: applied the upgrade as one small package slice, then re-ran the fixed gate chain.
- `R4`: only low-risk package overrides were executed automatically; higher-risk major-version families were left for a dedicated compatibility slice.
- `R6`: executed `build -> test -> contract/invariant -> hotspot` in fixed order.
- `R8`: this file records `basis -> command -> evidence -> rollback`.

## Platform Diagnostics

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
    - evidence_link: `docs/change-evidence/20260422-transitive-dependency-closure.md`
    - expires_at: `2026-04-22`

## Changes

### Infra

- added explicit `DocumentFormat.OpenXml 3.5.1`
- added explicit `System.IO.Packaging 10.0.7`

### Tests

- added explicit `DocumentFormat.OpenXml 3.5.1`
- added explicit `System.IO.Packaging 10.0.7`
- added explicit `Microsoft.Bcl.AsyncInterfaces 10.0.7`
- added explicit `Newtonsoft.Json 13.0.4`

## Commands And Evidence

### Discovery

- cmd: `dotnet list ClassroomToolkit.sln package --include-transitive`
  - exit_code: `0`
  - key_output: confirmed the residual packages were transitive, not direct top-level requests
- cmd: `dotnet list ClassroomToolkit.sln package --outdated --include-transitive`
  - exit_code before change: `0`
  - key_output: reported `OpenXml`, `System.IO.Packaging`, `Newtonsoft.Json`, and `Microsoft.Bcl.AsyncInterfaces` as remaining lower-risk drifts

### Hard gates

- cmd: `dotnet build ClassroomToolkit.sln -c Debug -m:1`
  - exit_code: `0`
  - key_output: `0 warnings`, `0 errors`
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1`
  - exit_code: `0`
  - key_output: `3406` passed
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - exit_code: `0`
  - key_output: `28` contract/invariant tests passed
- cmd: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - exit_code: `0`
  - key_output: `[hotspot] PASS - all .cs files within line budget (max=1200)`

### Post-upgrade dependency verification

- cmd: `dotnet list ClassroomToolkit.sln package --deprecated`
  - exit_code: `0`
  - key_output: no deprecated packages across all projects
- cmd: `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive`
  - exit_code: `0`
  - key_output: no vulnerable packages across all projects
- cmd: `dotnet list ClassroomToolkit.sln package --outdated --include-transitive`
  - exit_code after change: `0`
  - key_output:
    - removed from outdated set: `DocumentFormat.OpenXml`, `DocumentFormat.OpenXml.Framework`, `System.IO.Packaging`, `Newtonsoft.Json`, `Microsoft.Bcl.AsyncInterfaces`
    - remaining residuals are now limited to major-version families: `SQLitePCLRaw.*`, `SixLabors.Fonts`, `Microsoft.Testing.Platform*`, `Microsoft.ApplicationInsights`

## Hotspot Review

- file: `src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj`
  - conclusion: only package-resolution metadata changed; no business logic, storage paths, or workbook semantics were modified.
- file: `tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
  - conclusion: test-runtime compatibility libraries were pinned forward without changing test intent or production code behavior.

## Residuals

- `SQLitePCLRaw.* 2.1.11 -> 3.0.2`
  - reason: major-version jump under `Microsoft.Data.Sqlite`; should be validated together with actual SQLite runtime behavior instead of forced as an override.
- `SixLabors.Fonts 1.0.0 -> 2.1.3`
  - reason: major-version jump under `ClosedXML`; should be validated with workbook import/export compatibility and rendering behavior.
- `Microsoft.Testing.Platform* 1.9.1 -> 2.2.1`
  - reason: test-host platform is owned by upstream test packages; overriding it independently is a tooling compatibility risk.
- `Microsoft.ApplicationInsights 2.23.0 -> 3.1.0`
  - reason: major-version jump in the test dependency chain with no current vulnerability/deprecation evidence.

## Rollback

- rollback command:
  - `git restore --source=HEAD -- src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj src/ClassroomToolkit.Infra/packages.lock.json src/ClassroomToolkit.App/packages.lock.json tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj tests/ClassroomToolkit.Tests/packages.lock.json docs/change-evidence/20260422-transitive-dependency-closure.md`
