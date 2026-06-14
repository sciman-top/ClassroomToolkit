# 2026-06-14 PaintSettingsDialog Interactions Split

## Scope
- Rule IDs: R1, R2, R5, R6, R8
- Risk: low
- Boundary: `src/ClassroomToolkit.App/Paint/PaintSettingsDialog*.cs`
- Target: reduce settings dialog hotspot complexity by separating window lifecycle and defaults-restore orchestration from interaction confirmation/change handlers.

## Current Landing
`PaintSettingsDialog.Interactions.cs` mixed:
- dialog loaded/closed lifecycle handling
- persisted settings confirmation
- cancel action
- restore current/all defaults flows
- brush slider and brush-style change callbacks
- active brush size resolution

That made the file combine dialog shell behavior, reset orchestration, and ordinary interaction callbacks.

## Change
- Added `PaintSettingsDialog.Lifecycle.cs` for `OnDialogLoaded()` and `OnDialogClosed()`.
- Added `PaintSettingsDialog.Restore.cs` for current-tab/all-default restore flows and `ApplyDefaultSettingsForCurrentTab()`.
- Reduced `PaintSettingsDialog.Interactions.cs` to confirm/cancel handlers, brush change callbacks, and active brush-size resolution.

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
- `OnDialogLoaded()` still ensures the dialog is visible before deferred `SizeToContent` commit.
- `OnDialogClosed()` still detaches dirty-tracking, preset-managed, classroom-writing-mode, loaded, and closed handlers.
- Current-tab restore still warns for the first tab because it can also restore scene parameters.
- `PaintSettingsDialog.Interactions.cs` dropped from 289 lines to 108 lines; the largest `PaintSettingsDialog*.cs` file is now `PaintSettingsDialog.Selection.cs` at 277 lines.

## Rollback
- Revert `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Interactions.cs`
- Remove `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Lifecycle.cs`
- Remove `src/ClassroomToolkit.App/Paint/PaintSettingsDialog.Restore.cs`
- Remove this evidence file
