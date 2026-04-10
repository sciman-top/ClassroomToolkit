using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Presentation;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Services.Presentation;
using MediaColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void DispatchSessionEvent(UiSessionEvent sessionEvent)
    {
        var transition = _sessionCoordinator.Dispatch(sessionEvent);
        SafeActionExecutionExecutor.TryExecute(
            () => UiSessionTransitionOccurred?.Invoke(transition),
            ex => Debug.WriteLine($"[UiSessionTransitionOccurred] callback failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private void ApplySessionOverlayTopmost(bool topmostRequired)
    {
        if (!topmostRequired)
        {
            return;
        }

        EnsureOverlayTopmost(enforceZOrder: false);
        if (UiSessionFloatingZOrderRequestPolicy.TryResolveForOverlayTopmost(topmostRequired, out var request))
        {
            SafeActionExecutionExecutor.TryExecute(
                () => FloatingZOrderRequested?.Invoke(request),
                ex => Debug.WriteLine($"[FloatingZOrderRequested] session callback failed: {ex.GetType().Name} - {ex.Message}"));
        }
    }

    private void EnsureOverlayTopmost(bool enforceZOrder)
    {
        if (!OverlayTopmostApplyGatePolicy.ShouldApply(IsVisible, WindowState))
        {
            return;
        }

        WindowTopmostExecutor.ApplyNoActivate(this, enabled: true, enforceZOrder: enforceZOrder);
    }

    private void ApplySessionNavigationMode(UiNavigationMode _)
    {
        UpdateWpsNavHookState();
        UpdateInputPassthrough();
        UpdateFocusAcceptance();
    }

    private void ApplySessionInkVisibility(UiInkVisibility _)
    {
        // Place-holder effect hook for future ink visibility policy.
    }

    private void ApplySessionWidgetVisibility(UiSessionWidgetVisibility _)
    {
        if (UiSessionFloatingZOrderRequestPolicy.TryResolveForWidgetVisibility(_, out var request))
        {
            SafeActionExecutionExecutor.TryExecute(
                () => FloatingZOrderRequested?.Invoke(request),
                ex => Debug.WriteLine($"[FloatingZOrderRequested] session callback failed: {ex.GetType().Name} - {ex.Message}"));
        }
    }

    private static UiToolMode MapSessionToolMode(PaintToolMode mode)
    {
        return mode == PaintToolMode.Cursor ? UiToolMode.Cursor : UiToolMode.Draw;
    }

    private static PresentationSourceKind MapPresentationSource(PresentationType type)
    {
        return SessionSceneSourceMapper.MapPresentationSource(MapPresentationForegroundSource(type));
    }

    private static PresentationForegroundSource MapPresentationForegroundSource(PresentationType type)
    {
        return type switch
        {
            PresentationType.Office => PresentationForegroundSource.Office,
            PresentationType.Wps => PresentationForegroundSource.Wps,
            _ => PresentationForegroundSource.Unknown
        };
    }

    private static PhotoSourceKind MapPhotoSource(bool isPdf)
    {
        return SessionSceneSourceMapper.MapPhotoSource(isPdf);
    }

    private static void LogSessionTransition(UiSessionTransition transition)
    {
        Debug.WriteLine(
            $"[UiSession] #{transition.Id} evt={transition.Event.GetType().Name} " +
            $"scene={transition.Previous.Scene}->{transition.Current.Scene} " +
            $"tool={transition.Previous.ToolMode}->{transition.Current.ToolMode} " +
            $"nav={transition.Previous.NavigationMode}->{transition.Current.NavigationMode} " +
            $"focus={transition.Previous.FocusOwner}->{transition.Current.FocusOwner} " +
            $"widgets={transition.Current.RollCallVisible}/{transition.Current.LauncherVisible}/{transition.Current.ToolbarVisible} " +
            $"inkDirty={transition.Previous.InkDirty}->{transition.Current.InkDirty}");
    }

    private void UpdateCursor(PaintToolMode mode)
    {
        System.Windows.Input.Cursor cursor = mode switch
        {
            PaintToolMode.Cursor => System.Windows.Input.Cursors.Arrow,
            PaintToolMode.Brush => CustomCursors.GetBrushCursor(_brushColor),
            PaintToolMode.Eraser => CustomCursors.GetEraserCursor(_eraserSize),
            PaintToolMode.Shape => System.Windows.Input.Cursors.Cross,
            PaintToolMode.RegionErase => CustomCursors.RegionErase,
            _ => System.Windows.Input.Cursors.Arrow
        };

        Cursor = cursor;
    }

    public void SetBrush(MediaColor color, double size, byte opacity)
    {
        _brushColor = color;
        _brushSize = Math.Max(1.0, size);
        _brushOpacity = opacity;
        if (_mode == PaintToolMode.Brush)
        {
            UpdateCursor(PaintToolMode.Brush);
        }
    }

    public void SetEraserSize(double size)
    {
        _eraserSize = Math.Max(4.0, size);
        if (_mode == PaintToolMode.Eraser)
        {
            UpdateCursor(PaintToolMode.Eraser);
        }
    }

    public void SetShapeType(PaintShapeType type)
    {
        if (_shapeType == PaintShapeType.Triangle)
        {
            CancelPendingTriangleDraft($"shape-switch:{_shapeType}->{type}");
        }

        _shapeType = type;
        if (_shapeType != PaintShapeType.Triangle)
        {
            ResetTriangleState();
        }
    }

    public void ClearAll()
    {
        if (_hasDrawing)
        {
            PushHistory();
        }
        ClearSurface();
        _visualHost.Clear();
        ClearShapePreview();
        ClearRegionSelection();
        _hasDrawing = false;
        if (_inkStrokes.Count > 0)
        {
            _inkStrokes.Clear();
        }
        if (_inkRecordEnabled)
        {
            NotifyInkStateChanged(updateActiveSnapshot: true);
        }

        if (_photoModeActive)
        {
            ClearPhotoInkStateAfterClearAll();
        }
    }

    private void ClearPhotoInkStateAfterClearAll()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return;
        }

        var sourcePath = _currentDocumentPath;
        var pageIndex = Math.Max(1, _currentPageIndex);
        var currentCacheKey = BuildPhotoModeCacheKey(sourcePath, pageIndex, _photoDocumentIsPdf);
        _inkSidecarAutoSaveTimer?.Stop();
        _inkSidecarAutoSaveGate.NextGeneration();
        if (!string.IsNullOrWhiteSpace(_currentCacheKey))
        {
            _photoCache.Remove(_currentCacheKey);
            InvalidateNeighborInkCache(_currentCacheKey);
        }
        if (!string.IsNullOrWhiteSpace(currentCacheKey))
        {
            _photoCache.Remove(currentCacheKey);
            InvalidateNeighborInkCache(currentCacheKey);
        }

        MarkInkPageModified(sourcePath, pageIndex, "empty", Array.Empty<InkStrokeData>());
        ClearInkWalSnapshot(sourcePath, pageIndex);

        if (_inkSaveEnabled && _inkPersistence != null)
        {
            _ = SafeActionExecutionExecutor.TryExecute(
                () =>
                {
                    PersistInkHistorySnapshot(sourcePath, pageIndex, new List<InkStrokeData>(), _inkPersistence);
                    _inkExport?.RemoveCompositeOutputsForPage(sourcePath, pageIndex);
                });
        }

        _neighborInkCache.Clear();
        _neighborInkRenderPending.Clear();
        _neighborInkSidecarLoadPending.Clear();
        ClearNeighborInkVisuals(clearSlotIdentity: true);
        RequestCrossPageDisplayUpdate(CrossPageUpdateSources.InkStateChanged);
    }

    private bool TryEnforceRuntimeEmptyGuardForCurrentPage(bool clearSurfaceWhenDrawing = true)
    {
        if (!IsRuntimeInkPageExplicitlyCleared(_currentDocumentPath, Math.Max(1, _currentPageIndex)))
        {
            return false;
        }

        if (_inkStrokes.Count > 0)
        {
            _inkStrokes.Clear();
        }

        if (!string.IsNullOrWhiteSpace(_currentCacheKey))
        {
            _photoCache.Remove(_currentCacheKey);
            InvalidateNeighborInkCache(_currentCacheKey);
        }

        if (clearSurfaceWhenDrawing && _hasDrawing)
        {
            ClearSurface();
            _hasDrawing = false;
        }

        return true;
    }

    public MediaColor CurrentBrushColor => _brushColor;
    public byte CurrentBrushOpacity => _brushOpacity;
    public string CurrentDocumentName => _currentDocumentName;
    public string CurrentDocumentPath => _currentDocumentPath;
    public ClassroomToolkit.Application.UseCases.Photos.PhotoFileType CurrentPhotoFileType => !_photoModeActive
        ? ClassroomToolkit.Application.UseCases.Photos.PhotoFileType.Unknown
        : _photoDocumentIsPdf
            ? ClassroomToolkit.Application.UseCases.Photos.PhotoFileType.Pdf
            : ClassroomToolkit.Application.UseCases.Photos.PhotoNavigationPlanner.ClassifyPath(_currentDocumentPath);
    public DateTime CurrentCourseDate => _currentCourseDate;
    public int CurrentPageIndex => _currentPageIndex;
}
