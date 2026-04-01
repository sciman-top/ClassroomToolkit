# 2026-04-01 Cross-Page Neighbor Outside Ink Recovery

- Rule IDs: `R1` `R2` `R3` `R6` `R8`
- Risk: Medium
- Boundary: `src/ClassroomToolkit.App/Paint/*`, `src/ClassroomToolkit.App/Ink/*`
- Current landing: cross-page neighbor ink cache / slot transform / neighbor seed path
- Target destination: keep cross-page outside-page ink rendering and offset alignment inside `Paint` + `Ink` rendering path

## Goal

Fix the regression where `PDF/图片全屏 + 跨页显示` lost left/right outside-page ink when a page switched from current page to neighbor page during brush cross-page writing.

Observed symptom:

- page 1 and page 2 both had strokes extending outside the page on left/right
- when drawing from page 1 into page 2, page 1 outside-page ink disappeared
- when drawing back from page 2 into page 1, page 2 outside-page ink disappeared and page 1 outside-page ink reappeared

## Root Cause

Neighbor-page ink bitmaps were rendered strictly at the page bitmap width.

That meant:

1. current-page runtime ink could still show outside-page left/right overflow
2. once that page became a neighbor page, its ink was re-rendered into a page-width `RenderTargetBitmap`
3. any stroke bounds extending beyond `[0, pageWidth]` were clipped away
4. because neighbor page and neighbor ink shared the same transform, there was no separate X offset available to realign a widened ink bitmap

So the page looked correct only while it was the current page; once it became a neighbor, its outside-page ink vanished.

## Changes

1. Added neighbor render-surface planning:
   - [CrossPageNeighborInkRenderSurfacePolicy.cs](E:/CODE/ClassroomToolkit/src/ClassroomToolkit.App/Paint/CrossPageNeighborInkRenderSurfacePolicy.cs)
   - Computes widened neighbor ink bitmap width from actual stroke horizontal bounds
   - Carries a `HorizontalOffsetDip` for left overflow

2. Extended neighbor ink bitmap cache entry:
   - [PaintOverlayWindow.Photo.cs](E:/CODE/ClassroomToolkit/src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.cs)
   - `InkBitmapCacheEntry` now stores `HorizontalOffsetDip`

3. Updated ink renderer to support horizontal offset:
   - [InkStrokeRenderer.cs](E:/CODE/ClassroomToolkit/src/ClassroomToolkit.App/Ink/InkStrokeRenderer.cs)
   - `RenderPage(...)` now accepts `horizontalOffsetDip`
   - Renders strokes under `TranslateTransform(horizontalOffsetDip, 0)`

4. Restored cross-page neighbor ink offset pipeline:
   - [PaintOverlayWindow.Photo.CrossPage.cs](E:/CODE/ClassroomToolkit/src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs)
   - resolves stroke horizontal bounds
   - widens neighbor ink render surface when needed
   - stores offset in cache
   - keeps neighbor ink slot tag with `BaseTop + HorizontalOffsetDip`
   - applies separate transforms for page image and ink image
   - reuses offset in async render replacement and visible-slot priming

5. Restored interactive seed-path offset propagation:
   - [PaintOverlayWindow.Photo.Navigation.cs](E:/CODE/ClassroomToolkit/src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs)
   - seeded neighbor ink frame now carries cached `HorizontalOffsetDip`

6. Added regression tests:
   - [CrossPageNeighborInkRenderSurfacePolicyTests.cs](E:/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/CrossPageNeighborInkRenderSurfacePolicyTests.cs)
   - [CrossPageNeighborInkRenderSurfaceContractTests.cs](E:/CODE/ClassroomToolkit/tests/ClassroomToolkit.Tests/CrossPageNeighborInkRenderSurfaceContractTests.cs)

## Verification

### RED -> GREEN

1. RED
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~CrossPageNeighborInkRenderSurfacePolicyTests|FullyQualifiedName~CrossPageNeighborInkRenderSurfaceContractTests"`
   - Result before implementation: compile failed because `CrossPageNeighborInkRenderSurfacePolicy` did not exist

2. GREEN
   - same command after implementation
   - Result: pass `4/4`

### Hard Gates

1. Build
   - `dotnet build ClassroomToolkit.sln -c Debug`
   - Result: pass, `0 warning`, `0 error`

2. Test
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug`
   - Result: pass, `3036/3036`

3. Contract / Invariant
   - `dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c Debug --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"`
   - Result: pass, `24/24`

4. Hotspot
   - `powershell -File scripts/quality/check-hotspot-line-budgets.ps1`
   - Result: `status=PASS`

## Platform N/A

- Type: `platform_na`
- Item: `codex status`
- Reason: non-interactive terminal returned `stdin is not a terminal`
- Alternative verification:
  - `codex --version` -> `codex-cli 0.118.0`
  - `codex --help` succeeded
- Evidence link: this file
- Expires at: `2026-04-08`

## Rollback

1. Revert these files:
   - `src/ClassroomToolkit.App/Paint/CrossPageNeighborInkRenderSurfacePolicy.cs`
   - `src/ClassroomToolkit.App/Ink/InkStrokeRenderer.cs`
   - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.cs`
   - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.CrossPage.cs`
   - `src/ClassroomToolkit.App/Paint/PaintOverlayWindow.Photo.Navigation.cs`
   - `tests/ClassroomToolkit.Tests/CrossPageNeighborInkRenderSurfacePolicyTests.cs`
   - `tests/ClassroomToolkit.Tests/CrossPageNeighborInkRenderSurfaceContractTests.cs`

2. Preserved pre-cleanup user stash remains:
   - `stash@{0}: pre-crosspage-clearall-cleanup-20260401`
