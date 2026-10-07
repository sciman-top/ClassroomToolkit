using ClassroomToolkit.App.Presentation;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using System.Drawing;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows;
using System;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct RegionCaptureInitialPassthroughDecision(
    bool ShouldCancel,
    RegionScreenCapturePassthroughInputKind InputKind,
    System.Drawing.Point? ScreenPoint);

internal readonly record struct RegionCaptureResumeTriggerDecision(
    bool ShouldClearDirectWhiteboardEntryArm,
    bool ShouldResumeRegionCapture);

internal enum RegionSelectionCompletionDecision
{
    KeepWaiting = 0,
    Accept = 1
}

internal static class SessionSceneSourceMapper
{
    internal static PresentationSourceKind MapPresentationSource(PresentationForegroundSource source)
    {
        return source switch
        {
            PresentationForegroundSource.Wps => PresentationSourceKind.Wps,
            PresentationForegroundSource.Office => PresentationSourceKind.PowerPoint,
            _ => PresentationSourceKind.Unknown
        };
    }

    internal static PhotoSourceKind MapPhotoSource(bool isPdf)
    {
        return isPdf ? PhotoSourceKind.Pdf : PhotoSourceKind.Image;
    }
}

internal sealed record WhiteboardResumeScene(
    UiSceneKind Scene,
    PhotoSourceKind PhotoSource,
    PresentationSourceKind PresentationSource);

internal static class WhiteboardResumeSceneResolver
{
    internal static WhiteboardResumeScene Resolve(
        bool photoModeActive,
        bool photoIsPdf,
        PresentationForegroundSource fullscreenPresentationSource)
    {
        if (photoModeActive)
        {
            return new WhiteboardResumeScene(
                UiSceneKind.PhotoFullscreen,
                SessionSceneSourceMapper.MapPhotoSource(photoIsPdf),
                PresentationSourceKind.Unknown);
        }

        if (fullscreenPresentationSource != PresentationForegroundSource.Unknown)
        {
            return new WhiteboardResumeScene(
                UiSceneKind.PresentationFullscreen,
                PhotoSourceKind.Unknown,
                SessionSceneSourceMapper.MapPresentationSource(fullscreenPresentationSource));
        }

        return new WhiteboardResumeScene(
            UiSceneKind.Idle,
            PhotoSourceKind.Unknown,
            PresentationSourceKind.Unknown);
    }
}

internal static class WindowScreenBoundsResolver
{
    private static readonly NativeCursorWindowGeometryInteropAdapter WindowGeometryAdapter = new();

    internal static bool TryResolve(Window? window, out Rectangle bounds, out double dpiScaleX, out double dpiScaleY)
    {
        bounds = Rectangle.Empty;
        dpiScaleX = 1.0;
        dpiScaleY = 1.0;
        if (window == null)
        {
            return false;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero
            && WindowGeometryAdapter.TryGetWindowRect(handle, out var left, out var top, out var right, out var bottom))
        {
            bounds = SceneResolversPolicies.ResolveFromScreenRect(
                left,
                top,
                right,
                bottom);
            return true;
        }

        if (!window.IsLoaded && !window.IsVisible)
        {
            return false;
        }

        bounds = SceneResolversPolicies.ResolveFromDip(
            window.Left,
            window.Top,
            Math.Max(window.ActualWidth, 1),
            Math.Max(window.ActualHeight, 1),
            dpiScaleX,
            dpiScaleY);
        return true;
    }
}

internal static class SceneResolversPolicies
{
    internal static RegionCaptureInitialPassthroughDecision ResolveRegionCaptureInitialPassthrough(
        int pointerScreenX,
        int pointerScreenY,
        IReadOnlyCollection<Rectangle>? passthroughRegions)
    {
        if (passthroughRegions == null)
        {
            return new RegionCaptureInitialPassthroughDecision(
                ShouldCancel: false,
                InputKind: RegionScreenCapturePassthroughInputKind.None,
                ScreenPoint: null);
        }

        foreach (var region in passthroughRegions)
        {
            if (region.Width <= 0 || region.Height <= 0)
            {
                continue;
            }

            if (region.Contains(pointerScreenX, pointerScreenY))
            {
                return new RegionCaptureInitialPassthroughDecision(
                    ShouldCancel: true,
                    InputKind: RegionScreenCapturePassthroughInputKind.PointerMove,
                    ScreenPoint: new System.Drawing.Point(pointerScreenX, pointerScreenY));
            }
        }

        return new RegionCaptureInitialPassthroughDecision(
            ShouldCancel: false,
            InputKind: RegionScreenCapturePassthroughInputKind.None,
            ScreenPoint: null);
    }

    internal static RegionCaptureResumeTriggerDecision ResolveRegionCaptureResumeTrigger(
        bool resumeArmed,
        bool toolbarVisible,
        bool toolbarLoaded,
        bool boardActive,
        bool overlayWhiteboardActive,
        bool pointerInsideToolbar)
    {
        if (!resumeArmed || !toolbarVisible || !toolbarLoaded)
        {
            return new RegionCaptureResumeTriggerDecision(
                ShouldClearDirectWhiteboardEntryArm: false,
                ShouldResumeRegionCapture: false);
        }

        if (boardActive || overlayWhiteboardActive)
        {
            return new RegionCaptureResumeTriggerDecision(
                ShouldClearDirectWhiteboardEntryArm: true,
                ShouldResumeRegionCapture: false);
        }

        if (pointerInsideToolbar)
        {
            return new RegionCaptureResumeTriggerDecision(
                ShouldClearDirectWhiteboardEntryArm: false,
                ShouldResumeRegionCapture: false);
        }

        return new RegionCaptureResumeTriggerDecision(
            ShouldClearDirectWhiteboardEntryArm: false,
            ShouldResumeRegionCapture: true);
    }

    internal const double MinimumSelectionSize = 4;

    internal static RegionSelectionCompletionDecision ResolvePointerRelease(double width, double height)
    {
        return width >= MinimumSelectionSize && height >= MinimumSelectionSize
            ? RegionSelectionCompletionDecision.Accept
            : RegionSelectionCompletionDecision.KeepWaiting;
    }

    internal static Rectangle ResolveFromDip(
        double leftDip,
        double topDip,
        double widthDip,
        double heightDip,
        double dpiScaleX,
        double dpiScaleY)
    {
        var scaleX = dpiScaleX > 0 ? dpiScaleX : 1.0;
        var scaleY = dpiScaleY > 0 ? dpiScaleY : 1.0;
        var left = (int)Math.Floor(leftDip * scaleX);
        var top = (int)Math.Floor(topDip * scaleY);
        var width = Math.Max((int)Math.Ceiling(Math.Max(widthDip, 1.0) * scaleX), 1);
        var height = Math.Max((int)Math.Ceiling(Math.Max(heightDip, 1.0) * scaleY), 1);
        return new Rectangle(left, top, width, height);
    }

    internal static Rectangle ResolveFromScreenRect(int left, int top, int right, int bottom)
    {
        var width = Math.Max(right - left, 1);
        var height = Math.Max(bottom - top, 1);
        return new Rectangle(left, top, width, height);
    }
}
