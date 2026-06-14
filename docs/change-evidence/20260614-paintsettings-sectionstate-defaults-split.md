# 2026-06-14 PaintSettingsDialog SectionState Split

## Scope
- Rule IDs: R1, R2, R5, R6, R8
- Risk: low
- Boundary: `src/ClassroomToolkit.App/Paint/PaintSettingsDialog*.cs`
- Target: reduce `PaintSettingsDialog.SectionState.cs` cognitive load by separating dirty-tracking wiring and full-default-reset behavior from section state snapshot/apply logic.

## Current Landing
`PaintSettingsDialog.SectionState.cs` mixed:
- section state record definitions
- snapshot capture logic
- apply-to-controls logic
- dirty-tracking event registration and callbacks
- full default reset flow

That made the file carry both state-shape logic and control-wiring/runtime reset orchestration.

## Change
- Added `PaintSettingsDialog.SectionDirtyTracking.cs` for dirty-tracking attach/detach and event callbacks.
- Added `PaintSettingsDialog.Defaults.cs` for `ApplyDefaultSettings()`.
- Reduced `PaintSettingsDialog.SectionState.cs` to section records and capture/apply logic.

## Verification
- `dotnet build ClassroomToolkit.sln -c Debug -p:UseSharedCompilation=false`
  - PASS: 0 warnings, 0 errors.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false`
  - PASS: 3533 passed, 0 failed.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - PASS: 29 passed, 0 failed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - PASS: all `.cs` files within line budget.

## Hotspot Review
- No dialog copy or visible control behavior was changed.
- Existing source contracts already aggregate `PaintSettingsDialog*.cs`, so the split keeps contract intent stable while lowering single-file complexity.
- `ApplyDefaultSettingsForCurrentTab()` remains in `Interactions.cs`; this batch only extracted the global all-defaults path.
- `PaintSettingsDialog.SectionState.cs` dropped from 390 lines to 191 lines; the extracted `SectionDirtyTracking` and `Defaults` partials are 117 and 92 lines respectively.

## Rollback
- Revert `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.SectionState.cs`
- Remove `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.SectionDirtyTracking.cs`
- Remove `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Defaults.cs`
- Remove this evidence file
