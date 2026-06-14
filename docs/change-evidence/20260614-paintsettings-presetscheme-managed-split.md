# 2026-06-14 PaintSettingsDialog Preset Scheme Split

## Scope
- Rule IDs: R1, R2, R5, R6, R8
- Risk: low
- Boundary: `src/ClassroomToolkit.App/Paint/PaintSettingsDialog*.cs`
- Target: reduce settings dialog preset hotspot complexity by separating preset flow, managed-control wiring, and managed-parameter snapshot/apply responsibilities.

## Current Landing
`PaintSettingsDialog.PresetScheme.cs` mixed:
- preset selection and apply flow
- convert-to-custom behavior
- preset hint and recommendation refresh
- managed control event registration and manual override demotion
- managed control tooltip/enabled state updates
- custom managed parameter snapshot capture/apply/debug formatting

That made the preset entrypoint carry both state-machine flow and low-level control plumbing.

## Change
- Added `PaintSettingsDialog.PresetManagedControls.cs` for managed-control event handlers, attach/detach, manual override demotion, and visual state updates.
- Added `PaintSettingsDialog.PresetManagedParameters.cs` for custom snapshot initialization/save, managed parameter capture/apply, and debug formatting.
- Reduced `PaintSettingsDialog.PresetScheme.cs` to preset selection/apply/hint flow.

## Verification
- `dotnet build ClassroomToolkit.sln -c Debug -p:UseSharedCompilation=false`
  - PASS: 0 warnings, 0 errors.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false`
  - PASS: full test suite completed successfully.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug -p:UseSharedCompilation=false --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - PASS: contract/invariant filter completed successfully.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - PASS: all `.cs` files within line budget.

## Hotspot Review
- No XAML, visible copy, persisted setting names, or preset policy semantics changed.
- `OnPresetSchemeChanged()`, `OnConvertToCustomEditingClick()`, `ApplyPresetScheme()`, `ResolveInitialPresetScheme()`, and `UpdatePresetHint()` remain in the preset entrypoint.
- Managed control event hookup and custom snapshot/apply behavior moved without signature changes.
- `PaintSettingsDialog.PresetScheme.cs` dropped from 258 lines to 87 lines.
- The largest `PaintSettingsDialog*.cs` files are now `ClassifierPackage.cs` and `State.cs` at 240 lines each.

## Rollback
- Revert `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.PresetScheme.cs`
- Remove `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.PresetManagedControls.cs`
- Remove `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.PresetManagedParameters.cs`
- Remove this evidence file
