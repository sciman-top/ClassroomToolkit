# 20260423 Analyzer Phased Enforcement Batch 5 (App Globalization + String Guard)

## Goal

Execute the next low-risk analyzer closure batch in `ClassroomToolkit.App`:

1. close `CA1305` / `CA1304` (explicit culture/format provider);
2. close `CA1865` (single-character `StartsWith`/`EndsWith` usage);
3. ratchet analyzer backlog baseline after verified reduction.

## Scope

- `src/ClassroomToolkit.App/App.xaml.cs`
- `src/ClassroomToolkit.App/AutoExitDialog.xaml.cs`
- `src/ClassroomToolkit.App/Ink/InkSettingsDialog.xaml.cs`
- `src/ClassroomToolkit.App/Photos/ImageItem.cs`
- `src/ClassroomToolkit.App/Photos/PhotoNavigationDiagnosticsTimestampPolicy.cs`
- `src/ClassroomToolkit.App/TimerSetDialog.xaml.cs`
- `src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Timer.cs`
- `src/ClassroomToolkit.App/Diagnostics/SystemDiagnostics.cs`
- `src/ClassroomToolkit.App/RollCallSettingsDialog.xaml.cs`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.IO.cs`
- `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Tree.cs`
- `src/ClassroomToolkit.App/ViewModels/RollCallViewModel.Navigation.cs`
- `scripts/quality/analyzer-backlog-baseline.json`

## Changes

1. `CA1305` closure:
   - Added explicit `CultureInfo.InvariantCulture` for fixed-format timestamp/number string conversions used in logs and deterministic UI fields.
2. `CA1304` closure:
   - Replaced parameterless `SpeechSynthesizer.GetInstalledVoices()` with explicit `GetInstalledVoices(CultureInfo.CurrentUICulture)` in diagnostics/settings voice enumeration paths.
3. `CA1865` closure:
   - Replaced single-character string calls with char overloads (`StartsWith('.')`, `EndsWith('组')`).
4. Backlog ratchet:
   - Refreshed `scripts/quality/analyzer-backlog-baseline.json` to current verified report.

## Metrics

- `ClassroomToolkit.App` (`latest-recommended`, unique warnings):
  - before Batch 5: `79`
  - after Batch 5: `65`
- targeted rule closure:
  - `CA1305`: `8 -> 0`
  - `CA1304`: `2 -> 0`
  - `CA1865`: `4 -> 0`
- repository analyzer backlog (`latest-all`, unique diagnostics):
  - previous baseline: `663`
  - new baseline: `649`

## Verification

1. App analyzer scan:
   - `dotnet build src/ClassroomToolkit.App/ClassroomToolkit.App.csproj -c Debug --no-incremental -m:1 -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:TreatWarningsAsErrors=false`
   - result: `PASS` (`TOTAL=65`, and `CA1305/CA1304/CA1865 = 0`)
2. Analyzer backlog guard:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-analyzer-backlog-baseline.ps1 -Configuration Debug`
   - result: `PASS` (`total=649`)
3. Full local quality chain:
   - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/quality/run-local-quality-gates.ps1 -Profile quick -Configuration Debug`
   - result: `ALL PASS`

## Risks

- App analyzer backlog still remains (`65`) and is now dominated by `CA1859` / `CA1068` / `CA1001` / `CA1848`.
- During local build chain, transient file-lock retry warning (`MSB3026`) may still appear but resolved within retry window and does not break gates.

## Rollback

- `git restore --source=HEAD~1 -- src/ClassroomToolkit.App scripts/quality/analyzer-backlog-baseline.json docs/change-evidence/20260423-analyzer-phased-enforcement-batch5-app-globalization-string-guard.md`
