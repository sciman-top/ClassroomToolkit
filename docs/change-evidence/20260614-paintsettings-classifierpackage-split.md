# 2026-06-14 PaintSettingsDialog Classifier Package Split

## Scope
- Rule IDs: R1, R2, R5, R6, R8
- Risk: low
- Boundary: `src/ClassroomToolkit.App/Paint/PaintSettingsDialog*.cs`
- Target: reduce settings dialog classifier-package hotspot complexity by separating button entrypoints, import/rollback flow, and status/rollback helpers.

## Current Landing
`PaintSettingsDialog.ClassifierPackage.cs` mixed:
- file export/import button entrypoints
- clipboard export/import button entrypoints
- import confirmation/apply/rollback flow
- status text refresh and status-summary formatting
- override normalization and rollback button state helpers

That made one file carry both UI entrypoints and lower-level classifier-package state handling.

## Change
- Added `PaintSettingsDialog.ClassifierPackage.Import.cs` for import, rollback, file read/write helpers, and applying working overrides.
- Added `PaintSettingsDialog.ClassifierPackage.Status.cs` for status text refresh, override normalization, rollback button state, and warning helper.
- Reduced `PaintSettingsDialog.ClassifierPackage.cs` to button entrypoints for file and clipboard flows.

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
- No XAML event names, visible copy, persisted setting names, or classifier package policy semantics changed.
- `OnExportClassifierPackageClick()`, `OnImportClassifierPackageClick()`, `OnCopyClassifierPackageClick()`, and `OnImportClassifierPackageFromClipboardClick()` remain as XAML entrypoints.
- Existing callers in initialization, defaults restore, and section state still use the same status helper method names.
- `PaintSettingsDialog.ClassifierPackage.cs` dropped from 240 lines to 105 lines.
- The largest `PaintSettingsDialog*.cs` file after this split is `PaintSettingsDialog.State.cs` at 240 lines.

## Rollback
- Revert `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.ClassifierPackage.cs`
- Remove `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.ClassifierPackage.Import.cs`
- Remove `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.ClassifierPackage.Status.cs`
- Remove this evidence file
