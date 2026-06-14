# 2026-06-14 ImageManagerWindow Navigation Split

## Scope
- Rule IDs: R1, R2, R5, R6, R8
- Risk: low
- Boundary: `src/ClassroomToolkit.App/Photos/ImageManagerWindow*.cs`
- Target: reduce `ImageManagerWindow.Navigation.cs` hotspot size by separating folder/favorites navigation from selection/multi-select/preview behavior.

## Current Landing
`ImageManagerWindow.Navigation.cs` mixed:
- favorites / recents management
- folder navigation
- selection and long-press multi-select
- preview open and delete flow

That made the file a repeated hotspot and forced contract tests to point at a single source file even when behavior was already split across the file family.

## Change
- Added `ImageManagerWindow.Favorites.cs` for favorites/recents dialog, callbacks, and recents maintenance.
- Added `ImageManagerWindow.Selection.cs` for item selection, long-press multi-select, delete flow, and preview open.
- Reduced `ImageManagerWindow.Navigation.cs` to folder open, history navigation, path input, and default-folder resolution.
- Updated `ImageManagerTouchFlowContractTests` to read the `ImageManagerWindow*.cs` file family instead of a single file path.

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
- The split follows existing behavior seams only; no new abstraction or state model was introduced.
- Favorites/recents callbacks still stay behind `SafeActionExecutionExecutor`.
- Single-tap open, long-press multi-select, and delete flow remain in the same window family and event wiring surface.
- `ImageManagerWindow.Navigation.cs` dropped from 861 lines to 289 lines; `ImageManagerWindow.Selection.cs` is now the largest file in this family at 406 lines.

## Rollback
- Revert `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Navigation.cs`
- Remove `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Favorites.cs`
- Remove `src/ClassroomToolkit.App/Photos/ImageManagerWindow.Selection.cs`
- Revert `tests/ClassroomToolkit.Tests/ImageManagerTouchFlowContractTests.cs`
- Remove this evidence file
