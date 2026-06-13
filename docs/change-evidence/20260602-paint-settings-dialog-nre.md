# 2026-06-02 Paint Settings Dialog NRE

## Scope
- Rule IDs: R2, R3, R6, R8
- Risk: low
- Boundary: `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.LayoutAndLabels.cs`
- Target: prevent the paint toolbar settings button from crashing while constructing `PaintSettingsDialog`.

## Root Cause
`Slider.ValueChanged` can fire during XAML loading before all sibling value labels and preview elements are initialized. `UpdateBrushSizeLabel()` only checked `BrushSizeValue`, then accessed `BrushSize2Value`, `BrushSize3Value`, and preview elements that could still be null.

## Change
- Added complete null guards for the control groups used by brush-size, opacity, eraser-size, and calligraphy threshold label updates.
- Added an STA construction regression test for `PaintSettingsDialog` with the same theme resources loaded by the app.

## Verification
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter FullyQualifiedName~PaintSettingsDialogConstructionTests`
  - PASS: 1 passed.
- `dotnet build ClassroomToolkit.sln -c Debug`
  - PASS: 0 warnings, 0 errors.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
  - PASS: 3524 passed.
- `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
  - PASS: 29 passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1`
  - PASS: all `.cs` files within line budget.

## Hotspot Review
- `PaintSettingsDialog.LayoutAndLabels.cs`: guards are limited to initialization-sensitive UI update helpers; normal post-load label updates still run unchanged.
- `PaintSettingsDialogConstructionTests.cs`: constructs the dialog on an STA thread and loads app theme dictionaries, covering the original XAML construction path.

## Rollback
- Revert `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.LayoutAndLabels.cs`.
- Remove `tests/ClassroomToolkit.Tests/PaintSettingsDialogConstructionTests.cs`.
- Remove this evidence file.
