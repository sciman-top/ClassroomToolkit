# 2026-05-14 ink redraw scheduled path consolidation

## Scope

- Rule IDs: R1/R2/R3/R6/R8, E4
- Risk level: low
- Boundary: `PaintOverlayWindow.Ink.Rendering.cs` redraw scheduling path and source contract coverage.
- Goal: remove duplicated scheduled redraw execution logic while preserving throttle behavior, redraw version checks, cross-page visual sync completion, and diagnostics.

## Basis

- Existing optimization plan points to `PaintOverlayWindow.Ink.Rendering.cs` as the next high-value large file family.
- Static review found the throttled and direct `RequestInkRedraw` dispatcher branches duplicated the same pending stamp reset, version freshness check, `_redrawInProgress` guard, `RedrawInkSurface`, completion sync, and diagnostics callback.
- Structural baseline from `HEAD`: `RedrawInkSurface();` and `OnInkRedrawCompleted();` appeared twice in the scheduling path; after this change each appears once through the shared helper.

## Change

- Added `RunPendingInkRedraw()` as the single scheduled redraw execution path.
- Both throttled and direct dispatcher branches now call the shared helper.
- Updated `PaintOverlayInkRedrawTelemetryContractTests` to lock the shared path and prevent duplicated redraw/completion calls from returning.
- Added `ResolveStoredInkRenderGeometry()` so stored stroke, ribbon, and bloom rendering share the same photo-transform decision path.
- Split draw command, pen cache key, brush/pen cache helpers, opacity packing, and layer-step helper into `PaintOverlayWindow.Ink.Rendering.Cache.cs`.

## Verification

- Focused tests:
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintOverlayInkRedrawTelemetryContractTests|FullyQualifiedName~PaintOverlayClearAllCrossPageRecoveryContractTests"`: passed, 7 passed.
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintOverlayInkRedrawTelemetryContractTests|FullyQualifiedName~PhotoInkRenderPolicyTests|FullyQualifiedName~InkStrokeRendererCompositeTests"`: passed, 9 passed.
  - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~PaintOverlayInkRedrawTelemetryContractTests|FullyQualifiedName~InkStrokeRendererCompositeTests|FullyQualifiedName~BrushPerformanceGuardTests"`: passed, 13 passed.
- Hard gate:
  - build: `dotnet build ClassroomToolkit.sln -c Debug`: passed, 0 warnings, 0 errors.
  - test: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`: passed, 3488 passed.
  - contract/invariant: `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`: passed, 29 passed.
  - hotspot: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality\check-hotspot-line-budgets.ps1`: passed.
  - whitespace: `git diff --check`: passed with only existing LF/CRLF normalization warnings for touched files.

## Hotspot Review

- `RequestInkRedraw` still keeps the throttled token guard before executing delayed redraw work.
- Direct and throttled paths still clear `_redrawPending`, reset `_pendingInkRedrawVersionStamp`, re-request stale redraws, and use `_redrawInProgress` as before.
- `OnInkRedrawCompleted` and `OnRedrawCompleted` remain tied to actual redraw execution.
- Stored stroke, ribbon, and bloom geometry still use photo transform directly when `RasterImage.RenderTransform` is `_photoContentTransform`; otherwise photo-mode geometries still pass through `ToScreenGeometry`.
- Cache helper split is partial-class only. It does not alter cache limits, key shape, opacity quantization, pen width quantization, or batch rendering behavior.
- `PaintOverlayWindow.Ink.Rendering.cs` is reduced from 874 to 759 lines; the new cache partial is 124 lines.
- No brush geometry, calligraphy rendering, photo ink transform, persistence format, or touch input behavior was changed.

## Gate Rerun Note

- A parallel focused test/build attempt hit `CS2012` file locks from `VBCSCompiler`.
- Recovery command: `dotnet build-server shutdown`.
- The hard gate was then rerun serially and passed.
- A later parallel build hit WPF temporary project generated-member errors; `dotnet build-server shutdown` plus serial rerun restored a clean build.

## Rollback

```powershell
git restore -- src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Rendering.cs src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Ink.Rendering.Cache.cs tests/ClassroomToolkit.Tests/PaintOverlayInkRedrawTelemetryContractTests.cs docs/change-evidence/20260514-ink-redraw-scheduled-path-consolidation.md
```
