# 2026-06-14 MainWindow.Photo ImageManager Split

## Scope
- Rule IDs: R1, R2, R5, R6, R8
- Risk: low
- Boundary: `src/ClassroomToolkit.App/MainWindow.Photo*.cs`
- Target: reduce `MainWindow.Photo.cs` hotspot size by separating image-manager coordination from photo overlay entry/navigation/focus behavior.

## Current Landing
`MainWindow.Photo.cs` mixed:
- image-manager create/open/close/state-change handling
- favorites / recents / left-panel / show-ink persistence sync
- photo selection to overlay entry
- photo navigation and focus recovery
- foreground retouch

This made the file a broad coordination hotspot even though part of the logic was already conceptually image-manager-only.

## Change
- Added `MainWindow.Photo.ImageManager.cs` for image-manager lifecycle and settings synchronization.
- Kept `MainWindow.Photo.cs` focused on photo entry, navigation, focus, photo-mode transitions, and foreground retouch.
- Updated source-contract tests that previously hard-coded `MainWindow.Photo.cs` to aggregate the `MainWindow.Photo*.cs` file family.

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
- No user-visible behavior was changed; this batch only moved whole method groups along existing responsibility seams.
- Methods with direct source contracts around photo entry and focus fallback stayed in the original `MainWindow.Photo.cs`.
- Image-manager event wiring and close cleanup remain in the same partial family and still write back to the same settings fields.
- `MainWindow.Photo.cs` dropped from 573 lines to 438 lines; `MainWindow.Photo.ImageManager.cs` now holds the extracted 146-line image-manager coordination slice.

## Rollback
- Revert `src/ClassroomToolkit.App/MainWindow.Photo.cs`
- Remove `src/ClassroomToolkit.App/MainWindow.Photo.ImageManager.cs`
- Revert `tests/ClassroomToolkit.Tests/MainWindowPhotoFocusDispatchContractTests.cs`
- Revert `tests/ClassroomToolkit.Tests/App/RegionCaptureWhiteboardIntegrationContractTests.cs`
- Remove this evidence file
