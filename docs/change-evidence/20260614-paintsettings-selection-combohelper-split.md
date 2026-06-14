# 2026-06-14 PaintSettingsDialog Selection Split

## Scope
- Rule IDs: R1, R2, R5, R6, R8
- Risk: low
- Boundary: `src/ClassroomToolkit.App/Paint/PaintSettingsDialog*.cs`
- Target: reduce settings dialog complexity by separating generic ComboBox/value helper behavior from domain-specific option selection.

## Current Landing
`PaintSettingsDialog.Selection.cs` mixed:
- shape, brush, preset, writing mode, toolbar scale, and ink export scope selection
- string/double tag selection helper behavior
- int/double ComboBox ensure/select/resolve helper behavior
- percent/byte conversion and clamping helpers

That made business option selection and generic control plumbing share one file.

## Change
- Added `PaintSettingsDialog.ComboSelection.cs` for generic ComboBox tag selection, int/double option helpers, toolbar scale nearest-value helper, and numeric conversion helpers.
- Reduced `PaintSettingsDialog.Selection.cs` to domain-specific option selection and resolution methods.

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
- No XAML, visible copy, persisted setting names, or settings file semantics changed.
- Existing initialization, confirm, defaults restore, preset, and section-state logic still call the same helper method names.
- `PaintSettingsDialog.Selection.cs` dropped from 277 lines to 135 lines.
- `PaintSettingsDialog.ComboSelection.cs` is 148 lines and carries no dialog-specific reset or persistence logic.
- The largest `PaintSettingsDialog*.cs` file after this split is `PaintSettingsDialog.PresetScheme.cs` at 258 lines.

## Rollback
- Revert `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Selection.cs`
- Remove `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.ComboSelection.cs`
- Remove this evidence file
