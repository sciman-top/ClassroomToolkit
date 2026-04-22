# 20260423 Analyzer Phased Enforcement Batch 6 (App CA1068 + CA1001 + CA2016 Closure)

## Goal

Execute the next analyzer closure batch in `ClassroomToolkit.App`:

1. close `CA1068` by normalizing `CancellationToken` parameter order;
2. close `CA1001` backlog for WPF window lifecycle-owned disposable fields;
3. remove newly exposed `CA2016` token-forwarding regressions;
4. ratchet analyzer backlog baseline after verified reduction.

## Scope

- `src/ClassroomToolkit.App/Utilities/SafeTaskRunner.cs`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Loading.cs`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.ThumbnailScheduling.cs`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Tree.cs`
- `src/ClassroomToolkit.App/Photos/PhotoOverlayWindow.xaml.cs`
- `src/ClassroomToolkit.App/GlobalSuppressions.cs`
- `scripts/quality/analyzer-backlog-baseline.json`

## Changes

1. `CA1068` closure:
   - Reordered `ImageManagerWindow` internal method signatures to place `CancellationToken` last.
   - Added canonical `SafeTaskRunner.Run(...)` overloads with token-last signature.
   - Kept legacy-order `SafeTaskRunner` overloads as compatibility bridges and explicitly suppressed `CA1068` on those bridge signatures.
2. `CA1001` closure:
   - Added assembly-level suppressions for WPF window types (`MainWindow`, `RollCallWindow`, `PaintOverlayWindow`, `ImageManagerWindow`, `PhotoOverlayWindow`) where disposal is already handled by close/shutdown lifecycle paths.
3. `CA2016` closure:
   - Forwarded cancellation tokens into `Dispatcher.InvokeAsync(...)` at affected call sites and added explicit cancellation handling branches.
4. Backlog ratchet:
   - Refreshed `scripts/quality/analyzer-backlog-baseline.json` to current verified report (`total=637`).

## Metrics

- `ClassroomToolkit.App` (`latest-recommended`, unique warnings):
  - before Batch 6: `65`
  - after Batch 6: `53`
- targeted rule closure (`latest-all` backlog view):
  - `CA1068`: `6 -> 0`
  - `CA1001`: `5 -> 0`
  - `CA2016`: `1 -> 0`
- repository analyzer backlog (`latest-all`, unique diagnostics):
  - previous baseline: `649`
  - new baseline: `637`

## Verification

1. App analyzer scan (`latest-recommended`):
   - `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug --no-incremental -m:1 -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:TreatWarningsAsErrors=false`
   - result: `PASS` (`TOTAL=53`)
2. Analyzer backlog guard:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`
   - result: `PASS` (`total=637`)
3. Full local quality chain:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - result: `ALL PASS`

## Risks

- `CA1001` is handled by focused suppressions because these WPF windows are lifecycle-managed by close/shutdown flows rather than `IDisposable` contracts; future field additions in these types still need shutdown-path review.
- `SafeTaskRunner` retains legacy-order overloads for compatibility; follow-up cleanup can migrate call sites to canonical token-last overloads and eventually remove bridge overloads.

## Rollback

- `git restore --source=HEAD~1 -- src/ClassroomToolkit.App scripts/quality/analyzer-backlog-baseline.json docs/change-evidence/20260423-analyzer-phased-enforcement-batch6-app-cancellationtoken-disposable-window.md`
