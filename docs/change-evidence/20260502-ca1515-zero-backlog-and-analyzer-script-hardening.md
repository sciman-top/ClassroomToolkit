## 2026-05-02 CA1515 zero backlog and analyzer script hardening

- rule_id: `R6 R8 E4`
- risk: `low`
- scope:
  - `src/ClassroomToolkit.App/GlobalSuppressions.cs`
  - `tests/ClassroomToolkit.Tests/PublicContractVisibilitySuppressionContractTests.cs`
  - `tests/ClassroomToolkit.Tests/GovernanceTruthSourceContractTests.cs`
  - `scripts/quality/check-analyzer-backlog-baseline.ps1`

### Goal

Finish the remaining low-risk `CA1515` cleanup without changing runtime behavior, and make the analyzer backlog gate robust when:

1. the backlog reaches `0`
2. WPF analyzer builds hit transient `wpftmp` or generated-file host failures

### Basis

- The analyzer backlog report before this slice still contained only `CA1515`.
- Remaining diagnostics mapped to intentional public contracts:
  - WPF app/window/dialog/control/converter entrypoints
  - XAML-bound models and view models
  - public DI or composition-facing contracts
  - persisted paint/session/settings compatibility enums and DTOs
- The analyzer gate script had a null-shape bug when `diagnostics_total` became `0`.
- This repository has known transient WPF analyzer build-state failures around `*_wpftmp.csproj`, generated `.g.cs` files, and `InitializeComponent` during analyzer-only builds. Those are host/build-state issues and should be retried, not misreported as product regressions.

### Changes

1. Expanded `CA1515` suppressions in `GlobalSuppressions.cs` for intentional public contracts instead of forcing unsafe visibility narrowing.
2. Extended `PublicContractVisibilitySuppressionContractTests` so the suppression list is contract-checked.
3. Hardened `check-analyzer-backlog-baseline.ps1`:
   - normalize `uniqueDiagnostics` with array wrapping so `0` diagnostics is a valid shape
   - compute `diagnostics_total` from the wrapped array count
   - add transient analyzer build failure detection for WPF `wpftmp` / generated-file races
   - retry once after `dotnet build-server shutdown`
4. Extended `GovernanceTruthSourceContractTests` so the analyzer script hardening is locked as repo truth source behavior.

### Commands

```powershell
dotnet build ClassroomToolkit.sln -c Debug
dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug
dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\run-local-quality-gates.ps1 -Profile standard -Configuration Debug
```

### Key evidence

- `dotnet build ClassroomToolkit.sln -c Debug`
  - result: `PASS`
  - summary: `0 warning / 0 error`
- `dotnet test tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj -c Debug`
  - result: `PASS`
  - summary: `3474 passed`
- contract/invariant filter
  - result: `PASS`
  - summary: `28 passed`
- `check-hotspot-line-budgets.ps1`
  - result: `PASS`
  - summary: `all .cs files within line budget (max=1200)`
- `run-local-quality-gates.ps1 -Profile standard -Configuration Debug`
  - result: `ALL PASS`
  - included:
    - `governance-truth-source PASS`
    - `dependency-governance PASS`
    - `dependency-vulnerability PASS`
    - `analyzer-backlog-baseline PASS total=0`
- `artifacts/quality/analyzer-backlog-report.json`
  - `diagnostics_total: 0`
  - `project_counts: []`
  - `rule_counts: []`

### Compatibility and behavior

- No production feature behavior changed.
- No public runtime contract was narrowed in places where WPF/XAML, DI composition, persisted data, or tests still intentionally rely on public visibility.
- No performance budget threshold was relaxed in this slice.

### Rollback

```powershell
git restore --source=HEAD -- scripts/quality/check-analyzer-backlog-baseline.ps1
git restore --source=HEAD -- src/ClassroomToolkit.App/GlobalSuppressions.cs
git restore --source=HEAD -- tests/ClassroomToolkit.Tests/GovernanceTruthSourceContractTests.cs
git restore --source=HEAD -- tests/ClassroomToolkit.Tests/PublicContractVisibilitySuppressionContractTests.cs
git restore --source=HEAD -- docs/change-evidence/20260502-ca1515-zero-backlog-and-analyzer-script-hardening.md
```
