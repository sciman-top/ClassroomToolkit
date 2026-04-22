# 2026-04-22 SQLite And ClosedXML Compatibility Upgrades

## Summary

- scope: `src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj`
- scope: `tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
- scope: refreshed `packages.lock.json` files under `src/ClassroomToolkit.Infra/`, `src/ClassroomToolkit.App/`, and `tests/ClassroomToolkit.Tests/`
- target destination: close the remaining SQLite runtime and ClosedXML font dependency drift without changing business semantics, external contracts, or persisted data formats
- risk level: medium

## Basis

1. Remaining runtime-oriented outdated packages were narrowed to:
   - `SixLabors.Fonts 1.0.0 -> 2.1.3`
   - `SQLitePCLRaw.bundle_e_sqlite3 2.1.11 -> 3.0.2`
   - `SQLitePCLRaw.core 2.1.11 -> 3.0.2`
   - `SQLitePCLRaw.provider.e_sqlite3 2.1.11 -> 3.0.2`
   - later, after that upgrade, `SourceGear.sqlite3 3.50.4.2 -> 3.50.4.5`
2. These packages sit on the actual workbook import/export and SQLite persistence path, so the upgrade was treated as a dedicated compatibility slice rather than a generic dependency cleanup.
3. The repository already contains focused regression coverage for both areas:
   - workbook/ClosedXML path: `StudentWorkbookStoreTests`
   - SQLite path: `StudentWorkbookSqliteStoreAdapterTests`, `RollCallSqliteStoreAdapterTests`, `InkHistorySqliteStoreAdapterTests`, `StartupCompatibilityProbeTests`
4. Remaining outdated packages after this slice belong to the test-host / telemetry toolchain (`Microsoft.Testing.Platform*`, `Microsoft.ApplicationInsights`) rather than the classroom runtime path.

## Rule Mapping

- `R1`: current landing point was the remaining runtime dependency residue; target destination was to close the SQLite and workbook-related families with evidence-backed compatibility checks.
- `R2`: upgraded in one narrow package slice, validated with focused regressions, then re-ran the full fixed gate chain.
- `R3`: root-cause focus was package drift on real runtime paths, not cosmetic “outdated list” cleanup.
- `R4`: treated as medium risk because the packages are major-version upgrades under runtime code paths; mitigated with dedicated SQLite/workbook regression runs before full gates.
- `R6`: executed `build -> test -> contract/invariant -> hotspot` after the final package state.
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
    - evidence_link: `docs/change-evidence/20260422-sqlite-closedxml-compatibility-upgrades.md`
    - expires_at: `2026-04-22`

## Changes

### Infra

- added explicit `SixLabors.Fonts 2.1.3`
- added explicit `SQLitePCLRaw.bundle_e_sqlite3 3.0.2`
- added explicit `SourceGear.sqlite3 3.50.4.5`

### Tests

- added explicit `SixLabors.Fonts 2.1.3`
- added explicit `SQLitePCLRaw.bundle_e_sqlite3 3.0.2`
- added explicit `SourceGear.sqlite3 3.50.4.5`

## Commands And Evidence

### Discovery

- cmd: `dotnet list src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj package --outdated --include-transitive`
  - exit_code before change: `0`
  - key_output: reported `SixLabors.Fonts` and `SQLitePCLRaw.*` as the remaining runtime-family drifts in infra
- cmd: `dotnet list tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj package --outdated --include-transitive`
  - exit_code before change: `0`
  - key_output: reported the same runtime-family drifts in the test project

### Focused compatibility validation

- cmd: `dotnet build ClassroomToolkit.sln -c Debug -m:1`
  - exit_code: `0`
  - key_output: build succeeded after adding `SixLabors.Fonts 2.1.3`, `SQLitePCLRaw.bundle_e_sqlite3 3.0.2`, and `SourceGear.sqlite3 3.50.4.5`
- cmd: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -m:1 --filter "FullyQualifiedName~StudentWorkbookStoreTests|FullyQualifiedName~StudentWorkbookSqliteStoreAdapterTests|FullyQualifiedName~RollCallSqliteStoreAdapterTests|FullyQualifiedName~InkHistorySqliteStoreAdapterTests|FullyQualifiedName~StartupCompatibilityProbeTests"`
  - exit_code: `0`
  - key_output: `67` targeted SQLite/workbook compatibility tests passed

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

- cmd: `dotnet list ClassroomToolkit.sln package --outdated --include-transitive`
  - exit_code after change: `0`
  - key_output:
    - removed from outdated set: `SixLabors.Fonts`, `SQLitePCLRaw.*`, `SourceGear.sqlite3`
    - remaining residuals are now limited to test-platform / telemetry packages in `ClassroomToolkit.Tests`
- cmd: `dotnet list ClassroomToolkit.sln package --deprecated`
  - exit_code: `0`
  - key_output: no deprecated packages across all projects
- cmd: `dotnet list ClassroomToolkit.sln package --vulnerable --include-transitive`
  - exit_code: `0`
  - key_output: no vulnerable packages across all projects

## Hotspot Review

- file: `src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj`
  - conclusion: only package-resolution metadata changed; no SQLite schema, workbook format, or file layout logic changed.
- file: `tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj`
  - conclusion: test project now matches the same runtime dependency family versions used by the infra path, so the focused regressions exercise the upgraded graph instead of the old one.

## Residuals

- `Microsoft.ApplicationInsights 2.23.0 -> 3.1.0`
  - reason: telemetry chain owned by the test-host ecosystem; not part of the classroom runtime path and not deprecated/vulnerable.
- `Microsoft.Testing.Platform* 1.9.1 -> 2.2.1`
  - reason: test-host/tooling stack major upgrade; should be handled as a dedicated test-platform migration, not mixed into runtime dependency work.

## Rollback

- rollback command:
  - `git restore --source=HEAD -- src/ClassroomToolkit.Infra/ClassroomToolkit.Infra.csproj src/ClassroomToolkit.Infra/packages.lock.json src/ClassroomToolkit.App/packages.lock.json tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj tests/ClassroomToolkit.Tests/packages.lock.json docs/change-evidence/20260422-sqlite-closedxml-compatibility-upgrades.md`
