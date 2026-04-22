# 20260423 Analyzer Phased Enforcement Batch 4 (App Backlog Reduction)

## Goal

Continue phased static-analysis closure for `ClassroomToolkit.App` by shrinking `latest-recommended` backlog in low-risk areas and ratcheting the repository analyzer backlog baseline.

## Scope

- `src/ClassroomToolkit.App/Settings/AppSettings.cs`
- `src/ClassroomToolkit.App/Ink/InkModels.cs`
- `src/ClassroomToolkit.App/Ink/InkStrokeRenderer.cs`
- `src/ClassroomToolkit.App/Ink/InkExportService.cs`
- `src/ClassroomToolkit.App/Ink/InkExportService.CompositeIndexing.cs`
- `src/ClassroomToolkit.App/Ink/InkExportService.Exporting.cs`
- `src/ClassroomToolkit.App/Ink/InkExportService.Rendering.cs`
- `src/ClassroomToolkit.App/MainWindow.Launcher.cs`
- `src/ClassroomToolkit.App/MainWindow.Lifecycle.cs`
- `src/ClassroomToolkit.App/Paint/*` (targeted CA1805/CA1822 helper cleanup)
- `src/ClassroomToolkit.App/Photos/ImageManagerViewModel.cs`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.IO.cs`
- `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs`
- `src/ClassroomToolkit.App/RollCallWindow.Windowing.cs`
- `src/ClassroomToolkit.App/RollCallWindow.xaml.cs`
- `src/ClassroomToolkit.App/ViewModels/RollCallViewModel*.cs`
- `scripts/quality/analyzer-backlog-baseline.json`

## Changes

1. App `CA1805` low-risk closure (default-value initializers):
   - removed explicit default `false/0/0.0` initializers where runtime semantics remain unchanged.
2. App `CA1822` closure:
   - converted private helper methods to `static` where API compatibility is unaffected;
   - retained instance APIs where compatibility is expected (public/internal entry points, test-instantiated types) and added targeted `SuppressMessage` entries.
3. Compatibility-safe adjustments:
   - kept `WpsHookOrchestrator` as instance class (test compatibility), used targeted `CA1822` suppressions instead of static-class migration.
4. Analyzer backlog ratchet:
   - refreshed `scripts/quality/analyzer-backlog-baseline.json` from latest scan report.

## Metrics

- `ClassroomToolkit.App` (`latest-recommended`, unique warnings):
  - before this batch: `163`
  - after this batch: `79`
- repository analyzer backlog (`latest-all`, unique diagnostics):
  - previous baseline: `751`
  - new baseline: `663`

## Verification

1. App analyzer scan:
   - `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug --no-incremental -m:1 -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:TreatWarningsAsErrors=false`
   - result: `PASS` (App unique warning total `79`)
2. Analyzer backlog guard:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`
   - result: `PASS` (`total=663`)
3. Full local quality chain:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - result: `ALL PASS`

## Risks

- `ClassroomToolkit.App` remains outside enforced analyzer batch; `latest-recommended` still has `79` remaining warnings (mainly `CA1859`, `CA1305`, `CA1068`, `CA1001`).
- Targeted `CA1822` suppressions preserve compatibility now, but should be revisited if/when App moves to full enforcement.

## Rollback

Revert this batch changes (App analyzer reduction + baseline ratchet):

- `git restore --source=HEAD~1 -- src/ClassroomToolkit.App scripts/quality/analyzer-backlog-baseline.json docs/change-evidence/20260423-analyzer-phased-enforcement-batch4-app-backlog-reduction.md`
