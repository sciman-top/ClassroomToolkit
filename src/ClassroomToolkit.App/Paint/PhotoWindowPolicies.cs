using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Application.UseCases.Photos;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows;
using System;
using WpfPoint
=
System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

internal static class AuxWindowKeyRoutingHandler
{
    internal static bool TryHandle(
        Key key,
        bool overlayVisible,
        Func<Key, bool> tryHandlePhotoKey,
        bool canRoutePresentationInput,
        Func<Key, bool> tryForwardPresentationKey)
    {
        ArgumentNullException.ThrowIfNull(tryHandlePhotoKey);
        ArgumentNullException.ThrowIfNull(tryForwardPresentationKey);

        if (!overlayVisible)
        {
            return false;
        }

        var photoHandled = SafeActionExecutionExecutor.TryExecute(
            () => tryHandlePhotoKey(key),
            fallback: false);

        if (photoHandled)
        {
            return true;
        }

        if (!canRoutePresentationInput || !PresentationKeyCommandPolicy.TryMap(key, out _))
        {
            return false;
        }

        return SafeActionExecutionExecutor.TryExecute(
            () => tryForwardPresentationKey(key),
            fallback: false);
    }
}

internal static class AuxWindowWheelRoutingHandler
{
    internal static bool TryHandle(
        int delta,
        bool overlayVisible,
        bool canRoutePresentationInput,
        Func<int, bool> tryForwardPresentationWheel)
    {
        ArgumentNullException.ThrowIfNull(tryForwardPresentationWheel);

        if (!overlayVisible || !canRoutePresentationInput || delta == 0)
        {
            return false;
        }

        return SafeActionExecutionExecutor.TryExecute(
            () => tryForwardPresentationWheel(delta),
            fallback: false);
    }
}

internal static class PhotoBackgroundVisibilityPolicy
{
    internal static Visibility Resolve(
        bool photoModeActive,
        bool boardActive,
        bool hasBackgroundSource)
    {
        return photoModeActive && !boardActive && hasBackgroundSource
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}

internal static class PhotoContentTransformPolicy
{
    internal static bool ShouldApplyPhotoTransform(
        bool enabledRequested,
        bool photoModeActive,
        bool boardActive,
        bool transformAvailable)
    {
        // RasterImage is backed by a viewport-sized bitmap. Applying the photo transform to that
        // bitmap makes off-viewport photo-space strokes render outside the bitmap and disappear
        // when the page is panned back into view.
        return false;
    }
}

internal static class PhotoCrossPageSequencePolicy
{
    internal static (IReadOnlyList<string> Sequence, int CurrentIndex) Normalize(
        IReadOnlyList<string>? sequence,
        int currentIndex)
    {
        var source = sequence?.ToList() ?? new List<string>();
        if (source.Count == 0)
        {
            return (Array.Empty<string>(), -1);
        }

        string? currentPath = currentIndex >= 0 && currentIndex < source.Count
            ? source[currentIndex]
            : null;

        var imageOnly = source
            .Where(path => PhotoNavigationPlanner.ClassifyPath(path) == PhotoFileType.Image)
            .ToList();
        if (imageOnly.Count == 0)
        {
            return (Array.Empty<string>(), -1);
        }

        var normalizedIndex = -1;
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            normalizedIndex = imageOnly.FindIndex(path =>
                string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase));
        }

        if (normalizedIndex < 0)
        {
            normalizedIndex = Math.Clamp(currentIndex, 0, imageOnly.Count - 1);
        }

        return (imageOnly, normalizedIndex);
    }
}

internal static class PhotoDocumentRuntimeDefaults
{
    internal const double PdfDefaultDpi = 96;
    internal const int PdfCacheLimit = 6;
    internal const long PdfCacheMaxBytes = 100L * 1024L * 1024L;
    internal const int PdfCacheTryEnterTimeoutMs = 50;
    internal const int PdfPrefetchTryEnterTimeoutMs = 100;
    internal const int PdfPrefetchDelayMs = 120;
    internal const int NeighborPageCacheLimit = 5;
}

internal static class PhotoInteractionModePolicy
{
    internal static bool IsPhotoNavigationEnabled(bool photoModeActive, bool boardActive)
    {
        return photoModeActive && !boardActive;
    }

    internal static bool IsPhotoTransformEnabled(bool photoModeActive, bool boardActive)
    {
        return photoModeActive && !boardActive;
    }

    internal static bool IsPhotoOrBoardActive(bool photoModeActive, bool boardActive)
    {
        return photoModeActive || boardActive;
    }

    internal static bool IsCrossPageDisplayActive(
        bool photoModeActive,
        bool boardActive,
        bool crossPageDisplayEnabled)
    {
        return crossPageDisplayEnabled
            && IsPhotoTransformEnabled(photoModeActive, boardActive);
    }
}

internal readonly record struct PhotoRightButtonDownExecutionPlan(
    bool ShouldArmPending,
    bool ShouldTryBeginPan);

internal static class PhotoRightButtonDownExecutionPolicy
{
    internal static PhotoRightButtonDownExecutionPlan Resolve(
        bool shouldArmPending,
        bool shouldAllowPan)
    {
        return new PhotoRightButtonDownExecutionPlan(
            ShouldArmPending: shouldArmPending,
            ShouldTryBeginPan: shouldAllowPan);
    }
}

internal enum PhotoRightButtonUpAction
{
    PassThrough,
    ShowContextMenu
}

internal readonly record struct PhotoRightButtonUpExecutionPlan(
    PhotoRightButtonUpAction Action,
    bool ShouldMarkHandled,
    bool ShouldClearPending);

internal static class PhotoRightButtonUpExecutionPolicy
{
    internal static PhotoRightButtonUpExecutionPlan Resolve(bool shouldShowContextMenuOnUp)
    {
        if (!shouldShowContextMenuOnUp)
        {
            return new PhotoRightButtonUpExecutionPlan(
                Action: PhotoRightButtonUpAction.PassThrough,
                ShouldMarkHandled: false,
                ShouldClearPending: false);
        }

        return new PhotoRightButtonUpExecutionPlan(
            Action: PhotoRightButtonUpAction.ShowContextMenu,
            ShouldMarkHandled: true,
            ShouldClearPending: true);
    }
}

internal static class PhotoRightClickContextMenuDefaults
{
    internal const double MinThresholdDip = 0.0;
    internal const double CancelMoveThresholdDip = 6.0;
}

internal static class PhotoRightClickContextMenuPolicy
{
    internal static bool ShouldArmPending(
        bool photoModeActive,
        bool photoFullscreen,
        PaintToolMode mode)
    {
        return photoModeActive && photoFullscreen && mode == PaintToolMode.Cursor;
    }

    internal static bool ShouldCancelPendingByMove(
        Vector delta,
        double thresholdDip = PhotoRightClickContextMenuDefaults.CancelMoveThresholdDip)
    {
        var threshold = Math.Max(PhotoRightClickContextMenuDefaults.MinThresholdDip, thresholdDip);
        return delta.Length > threshold;
    }

    internal static bool ShouldShowContextMenuOnUp(
        bool rightClickPending,
        bool photoModeActive,
        bool photoFullscreen,
        PaintToolMode mode)
    {
        return rightClickPending && ShouldArmPending(photoModeActive, photoFullscreen, mode);
    }
}

internal static class PhotoRightClickPendingStateUpdater
{
    internal static void Arm(
        ref bool pending,
        ref WpfPoint start,
        WpfPoint point)
    {
        pending = true;
        start = point;
    }

    internal static void Clear(ref bool pending)
    {
        pending = false;
    }

    internal static void UpdateByMove(
        ref bool pending,
        WpfPoint start,
        WpfPoint current)
    {
        if (!pending)
        {
            return;
        }

        var delta = current - start;
        if (PhotoRightClickContextMenuPolicy.ShouldCancelPendingByMove(delta))
        {
            pending = false;
        }
    }
}

internal readonly record struct PhotoTitleBarDragZOrderPlan(
    bool CanDrag,
    bool RequestZOrderBeforeDrag,
    bool RequestZOrderAfterDrag,
    bool ForceAfterDrag);

internal static class PhotoTitleBarDragZOrderPolicy
{
    internal static PhotoTitleBarDragZOrderPlan Resolve(
        bool photoModeActive,
        bool photoFullscreen,
        MouseButton changedButton)
    {
        var canDrag = photoModeActive
            && !photoFullscreen
            && changedButton == MouseButton.Left;
        if (!canDrag)
        {
            return new PhotoTitleBarDragZOrderPlan(
                CanDrag: false,
                RequestZOrderBeforeDrag: false,
                RequestZOrderAfterDrag: false,
                ForceAfterDrag: false);
        }

        return new PhotoTitleBarDragZOrderPlan(
            CanDrag: true,
            RequestZOrderBeforeDrag: false,
            RequestZOrderAfterDrag: true,
            ForceAfterDrag: false);
    }
}

internal static class PhotoTouchInteractionPolicy
{
    internal static bool ShouldUseManipulation(int activeTouchCount)
    {
        // WPF only starts a manipulation when the originating TouchDown is
        // left unhandled.  A one-finger manipulation is the canonical pan
        // path; a second finger upgrades the same stream to pinch/translate.
        return activeTouchCount >= 1;
    }

    internal static bool ShouldUseManipulationZoom(int activeTouchCount)
    {
        return activeTouchCount >= 2;
    }

    internal static bool ShouldIgnorePromotedTouchStylus(TabletDeviceType tabletDeviceType)
    {
        return tabletDeviceType == TabletDeviceType.Touch;
    }
}

internal static class PhotoWindowModeZOrderRetouchPolicy
{
    internal static bool ShouldRequest(bool photoModeActive, bool fullscreenChanged)
    {
        return photoModeActive && fullscreenChanged;
    }

    internal static bool ShouldForceEnforce(bool fullscreen)
    {
        return fullscreen;
    }
}

internal static class PhotoWindowStateRestorePolicy
{
    internal static bool ShouldArmFullscreenRestore(bool photoFullscreen)
    {
        return photoFullscreen;
    }

    internal static bool ShouldRestoreFullscreen(bool pendingFullscreenRestore, WindowState windowState)
    {
        return pendingFullscreenRestore && windowState != WindowState.Minimized;
    }
}
