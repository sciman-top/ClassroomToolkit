# 20260422 Analyzer Phased Enforcement Batch 1

## Goal

Start phased static-analysis enforcement without breaking the current build chain:

1. enforce `EnableNETAnalyzers + AnalysisLevel=latest-recommended` on low-risk layers first;
2. add `latest-all` backlog baseline guard for the remaining layers to prevent regression.

## Scope

- `Directory.Build.props`
- `scripts/quality/check-analyzer-backlog-baseline.ps1` (new)
- `scripts/quality/analyzer-backlog-baseline.json` (new, ratcheted to latest scan)
- `scripts/quality/run-local-quality-gates.ps1`
- `scripts/quality/check-governance-truth-source.ps1`
- `docs/governance/truth-source.md`
- `docs/runbooks/governance-endstate-maintenance.md`
- `tests/ClassroomToolkit.Tests/GovernanceTruthSourceContractTests.cs`
- Analyzer-fix touch points:
  - `src/ClassroomToolkit.Domain/Models/ClassRoster.cs`
  - `src/ClassroomToolkit.Domain/Services/RollCallEngine.cs`
  - `src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs`
  - `src/ClassroomToolkit.Infra/Settings/SettingsDocumentMigrationService.cs`
  - `src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs`
  - `src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs`

## Changes

1. Switched to phased analyzer strategy in `Directory.Build.props`:
   - global default: `EnableNETAnalyzers=false`, `TreatWarningsAsErrors=true`;
   - Batch 1 enforced projects: `ClassroomToolkit.Domain`, `ClassroomToolkit.Application`, `ClassroomToolkit.Infra` with:
     - `EnableNETAnalyzers=true`
     - `AnalysisLevel=latest-recommended`
2. Added backlog guard script:
   - `scripts/quality/check-analyzer-backlog-baseline.ps1`
   - scans `src/*` with `latest-all` + `--no-incremental` + non-failing warning mode;
   - writes `artifacts/quality/analyzer-backlog-report.json`;
   - blocks regressions when any CA rule/project count exceeds baseline.
3. Added and ratcheted baseline file:
   - `scripts/quality/analyzer-backlog-baseline.json`
   - current baseline total: `856` (down from previous `898`).
4. Wired new guard into quality chain:
   - appended `analyzer-backlog-baseline` step in `run-local-quality-gates.ps1`.
5. Updated governance truth-source contract/doc/runbook to include analyzer backlog guard as active gate.
6. Fixed a subset of CA issues in Batch 1 enforced layers (Domain/Infra), including:
   - `CA1865`, `CA2249`, `CA1512`, `CA1816`, `CA1859` and compatibility-preserving `CA1822` suppressions where API stability is required.

## Verification

1. Build gate:
   - `dotnet build ClassroomToolkit.sln -c Debug -m:1`
   - result: `PASS`
2. Full tests:
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - result: `PASS` (`3425 passed`)
3. Contract/invariant tests:
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - result: `PASS` (`28 passed`)
4. Analyzer backlog guard:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`
   - result: `PASS` (`total=856`)
5. Full local quality chain:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - result: `ALL PASS`

## Risks

- Analyzer backlog still exists in non-Batch-1 projects (`App/Interop/Services`) and is currently controlled by baseline ratchet rather than hard zero.
- Intermittent `MSB3026` copy-lock warnings can appear during local builds when files are transiently locked by running processes.

## Rollback

Revert this changeset:

- `git restore --source=HEAD~1 -- Directory.Build.props scripts/quality/check-analyzer-backlog-baseline.ps1 scripts/quality/analyzer-backlog-baseline.json scripts/quality/run-local-quality-gates.ps1 scripts/quality/check-governance-truth-source.ps1 docs/governance/truth-source.md docs/runbooks/governance-endstate-maintenance.md tests/ClassroomToolkit.Tests/GovernanceTruthSourceContractTests.cs src/ClassroomToolkit.Domain/Models/ClassRoster.cs src/ClassroomToolkit.Domain/Services/RollCallEngine.cs src/ClassroomToolkit.Infra/Settings/IniSettingsStore.cs src/ClassroomToolkit.Infra/Settings/SettingsDocumentMigrationService.cs src/ClassroomToolkit.Infra/Storage/StudentWorkbookStore.cs src/ClassroomToolkit.Infra/Logging/FileLoggerProvider.cs docs/change-evidence/20260422-analyzer-phased-enforcement-batch1.md`
