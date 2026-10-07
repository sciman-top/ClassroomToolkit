using System.Windows.Media;
using System.Windows;
using System;
using WpfPoint
=
System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoInkCoordinateMapper
{
    internal static Matrix CreateForwardMatrix(
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY)
    {
        var matrix = Matrix.Identity;
        matrix.Scale(pageScaleX * photoScaleX, pageScaleY * photoScaleY);
        matrix.Translate(translateX, translateY);
        return matrix;
    }

    internal static Matrix CreateInverseMatrix(
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY,
        double epsilon = PhotoTransformMathDefaults.InverseScaleEpsilon)
    {
        return TryCreateInverseMatrix(
            pageScaleX,
            pageScaleY,
            photoScaleX,
            photoScaleY,
            translateX,
            translateY,
            out var matrix,
            epsilon)
            ? matrix
            : Matrix.Identity;
    }

    internal static bool TryCreateInverseMatrix(
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY,
        out Matrix matrix,
        double epsilon = PhotoTransformMathDefaults.InverseScaleEpsilon)
    {
        var scaleX = pageScaleX * photoScaleX;
        var scaleY = pageScaleY * photoScaleY;
        if (Math.Abs(scaleX) < epsilon || Math.Abs(scaleY) < epsilon)
        {
            matrix = Matrix.Identity;
            return false;
        }

        matrix = Matrix.Identity;
        matrix.Scale(1.0 / scaleX, 1.0 / scaleY);
        matrix.Translate(-translateX / scaleX, -translateY / scaleY);
        return true;
    }

    internal static WpfPoint ToPhotoSpace(
        WpfPoint point,
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY)
    {
        var inverse = CreateInverseMatrix(
            pageScaleX,
            pageScaleY,
            photoScaleX,
            photoScaleY,
            translateX,
            translateY);
        return inverse.Transform(point);
    }

    internal static WpfPoint ResolveZoomAnchoredTranslation(
        WpfPoint viewportAnchor,
        WpfPoint photoPoint,
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY)
    {
        return new WpfPoint(
            viewportAnchor.X - photoPoint.X * pageScaleX * photoScaleX,
            viewportAnchor.Y - photoPoint.Y * pageScaleY * photoScaleY);
    }

    internal static Geometry ToPhotoGeometry(
        Geometry geometry,
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY)
    {
        var inverse = CreateInverseMatrix(
            pageScaleX,
            pageScaleY,
            photoScaleX,
            photoScaleY,
            translateX,
            translateY);
        var clone = geometry.Clone();
        clone.Transform = new MatrixTransform(inverse);
        var flattened = clone.GetFlattenedPathGeometry();
        if (flattened.CanFreeze)
        {
            flattened.Freeze();
        }

        return flattened;
    }

    internal static Geometry ToScreenGeometry(
        Geometry geometry,
        double pageScaleX,
        double pageScaleY,
        double photoScaleX,
        double photoScaleY,
        double translateX,
        double translateY)
    {
        var transform = CreateForwardMatrix(
            pageScaleX,
            pageScaleY,
            photoScaleX,
            photoScaleY,
            translateX,
            translateY);
        var clone = geometry.Clone();
        clone.Transform = new MatrixTransform(transform);
        if (clone.CanFreeze)
        {
            clone.Freeze();
        }

        return clone;
    }
}

internal static class PhotoInputAlignmentDefaults
{
    internal const double GestureSensitivityMin = 0.2;
    internal const double GestureSensitivityMax = 3.0;
    internal const double MinEventFactorFloor = 0.01;
    internal const double IgnoreFactorDelta = 0.001;
    internal const double PanResistanceFactorDefault = 0.42;
    internal const double PanResistanceFactorMin = 0.05;
    internal const double PanResistanceFactorMax = 0.95;
}

internal enum PhotoZoomInputSource
{
    Wheel,
    Gesture,
    Keyboard
}

internal static class PhotoZoomNormalizer
{
    internal static bool TryNormalizeFactor(
        PhotoZoomInputSource source,
        double rawValue,
        double wheelBase,
        double gestureSensitivity,
        double gestureNoiseThreshold,
        double minEventFactor,
        double maxEventFactor,
        out double factor)
    {
        factor = 1.0;
        var candidate = source switch
        {
            PhotoZoomInputSource.Wheel => Math.Pow(wheelBase, rawValue),
            PhotoZoomInputSource.Gesture => rawValue,
            PhotoZoomInputSource.Keyboard => rawValue,
            _ => rawValue
        };

        if (double.IsNaN(candidate) || double.IsInfinity(candidate) || candidate <= 0)
        {
            return false;
        }

        if (source == PhotoZoomInputSource.Gesture)
        {
            var sensitivity = Math.Clamp(
                gestureSensitivity,
                PhotoInputAlignmentDefaults.GestureSensitivityMin,
                PhotoInputAlignmentDefaults.GestureSensitivityMax);
            candidate = 1.0 + ((candidate - 1.0) * sensitivity);
        }

        if (source == PhotoZoomInputSource.Gesture && Math.Abs(candidate - 1.0) < Math.Max(0.0, gestureNoiseThreshold))
        {
            return false;
        }

        var minFactor = Math.Max(
            PhotoInputAlignmentDefaults.MinEventFactorFloor,
            Math.Min(minEventFactor, maxEventFactor));
        var maxFactor = Math.Max(minFactor, Math.Max(minEventFactor, maxEventFactor));
        candidate = Math.Clamp(candidate, minFactor, maxFactor);
        if (Math.Abs(candidate - 1.0) < PhotoInputAlignmentDefaults.IgnoreFactorDelta)
        {
            return false;
        }

        factor = candidate;
        return true;
    }
}

internal enum StylusPhotoPanPhase
{
    Down,
    Move,
    Up
}

internal enum StylusPhotoPanRoutingDecision
{
    PassThrough,
    BeginPan,
    UpdatePan,
    EndPan
}

internal enum StylusPhotoPanExecutionAction
{
    PassThrough,
    ReturnWithoutPan,
    BeginPan,
    UpdatePan,
    EndPan
}

internal readonly record struct StylusPhotoPanExecutionPlan(
    StylusPhotoPanExecutionAction Action,
    bool ShouldMarkHandled);

internal enum PhotoManipulationRoutingDecision
{
    Ignore,
    Consume,
    Handle
}

internal readonly record struct PhotoManipulationEventHandlingPlan(
    bool ShouldHandle,
    bool ShouldMarkHandled);

internal static class PhotoPanLimiter
{
    internal static double ApplyAxis(
        double value,
        double min,
        double max,
        bool allowResistance,
        double resistanceFactor = PhotoInputAlignmentDefaults.PanResistanceFactorDefault)
    {
        if (min > max)
        {
            (min, max) = (max, min);
        }
        if (!allowResistance)
        {
            return Math.Clamp(value, min, max);
        }
        var factor = Math.Clamp(
            resistanceFactor,
            PhotoInputAlignmentDefaults.PanResistanceFactorMin,
            PhotoInputAlignmentDefaults.PanResistanceFactorMax);
        if (value < min)
        {
            return min - ((min - value) * factor);
        }
        if (value > max)
        {
            return max + ((value - max) * factor);
        }
        return value;
    }
}

internal static class PhotoInputConflictGuard
{
    internal static bool ShouldSuppressWheelAfterGesture(DateTime lastGestureUtc, int suppressWindowMs, DateTime nowUtc)
    {
        if (lastGestureUtc == PhotoInputConflictDefaults.UnsetTimestampUtc)
        {
            return false;
        }
        var window = Math.Max(PhotoInputConflictDefaults.SuppressWindowMinMs, suppressWindowMs);
        if (window == 0)
        {
            return false;
        }
        return (nowUtc - lastGestureUtc).TotalMilliseconds <= window;
    }
}

internal static class PhotoInputConflictDefaults
{
    internal const int SuppressWindowMinMs = 0;
    internal static readonly DateTime UnsetTimestampUtc = DateTime.MinValue;
}

internal enum PhotoNavigationInkViewportSyncAction
{
    None = 0,
    UpdatePanCompensation = 1,
    ResetPanCompensation = 2
}

internal static class PhotoInkInteropPolicies
{
    internal static Rect ResolveBoundsPhotoInkCurrentPageClip(
        bool photoInkModeActive,
        bool crossPageDisplayActive,
        bool photoFullscreenActive,
        bool usePhotoTransform,
        Rect currentPageScreenRect,
        double pageWidthDip,
        double pageHeightDip)
    {
        if (!photoInkModeActive
            || !crossPageDisplayActive
            || photoFullscreenActive)
        {
            return Rect.Empty;
        }

        if (usePhotoTransform)
        {
            if (pageWidthDip <= 0 || pageHeightDip <= 0)
            {
                return Rect.Empty;
            }

            return new Rect(0, 0, pageWidthDip, pageHeightDip);
        }

        if (currentPageScreenRect.IsEmpty
            || currentPageScreenRect.Width <= 0
            || currentPageScreenRect.Height <= 0)
        {
            return Rect.Empty;
        }

        return currentPageScreenRect;
    }

    private const double NonZeroOffsetEpsilon = 0.0001;

    internal static bool ShouldApplyCompensation(
        bool photoInkModeActive,
        Transform? rasterRenderTransform,
        TranslateTransform panCompensation)
    {
        if (!photoInkModeActive || !ReferenceEquals(rasterRenderTransform, panCompensation))
        {
            return false;
        }

        return Math.Abs(panCompensation.X) > NonZeroOffsetEpsilon
            || Math.Abs(panCompensation.Y) > NonZeroOffsetEpsilon;
    }

    internal static Geometry AdjustToRasterSpace(
        Geometry geometry,
        double panCompensationX,
        double panCompensationY)
    {
        var clone = geometry.Clone();
        clone.Transform = new TranslateTransform(-panCompensationX, -panCompensationY);
        var adjusted = clone.GetFlattenedPathGeometry();
        if (adjusted.CanFreeze)
        {
            adjusted.Freeze();
        }

        return adjusted;
    }

    internal static Vector ResolvePhotoInkPanCompensation(
        bool photoInkModeActive,
        double currentTranslateX,
        double currentTranslateY,
        double lastRedrawTranslateX,
        double lastRedrawTranslateY)
    {
        if (!photoInkModeActive)
        {
            return new Vector(0, 0);
        }

        return new Vector(
            currentTranslateX - lastRedrawTranslateX,
            currentTranslateY - lastRedrawTranslateY);
    }

    internal static bool ShouldRequest(
        bool photoInkModeActive,
        double currentTranslateX,
        double currentTranslateY,
        double lastRedrawTranslateX,
        double lastRedrawTranslateY,
        double thresholdDip = InkRuntimeTimingDefaults.PhotoPanRedrawThresholdDip)
    {
        return photoInkModeActive
            && PhotoPanPolicies.ShouldRefresh(
                lastRedrawTranslateX,
                lastRedrawTranslateY,
                currentTranslateX,
                currentTranslateY,
                thresholdDip);
    }

    internal static Rect ResolveBoundsPhotoInkPreviewClip(
        bool photoInkModeActive,
        bool crossPageDisplayActive,
        bool photoFullscreenActive,
        bool usePhotoTransform,
        Rect currentPageScreenRect,
        double pageWidthDip,
        double pageHeightDip)
    {
        if (!photoInkModeActive || !crossPageDisplayActive || photoFullscreenActive)
        {
            return Rect.Empty;
        }

        if (!currentPageScreenRect.IsEmpty
            && currentPageScreenRect.Width > 0
            && currentPageScreenRect.Height > 0)
        {
            // Preview layer is in screen space; prefer current page screen rect
            // to avoid one-frame cross-page preview bleed at seam transitions.
            return currentPageScreenRect;
        }

        if (!usePhotoTransform || pageWidthDip <= 0 || pageHeightDip <= 0)
        {
            return Rect.Empty;
        }

        return new Rect(0, 0, pageWidthDip, pageHeightDip);
    }

    internal static bool ShouldRenderInteractiveInkInPhotoSpace(
        bool photoModeActive,
        Transform? rasterRenderTransform,
        Transform? photoContentTransform)
    {
        // Interactive and persisted ink are rendered into a viewport-sized raster surface.
        // Keep that surface in screen space; otherwise strokes stored in photo coordinates can be
        // clipped away before the photo transform is applied.
        return false;
    }

    internal static bool ShouldRequestImmediateRedraw(
        bool photoModeActive,
        Transform? rasterRenderTransform,
        Transform? photoContentTransform,
        bool crossPageBrushContinuationActive = false)
    {
        return photoModeActive && crossPageBrushContinuationActive;
    }

    internal static bool ShouldRender(
        bool photoInkModeActive,
        bool usePhotoTransform,
        Rect strokeBounds,
        Matrix photoTransformMatrix,
        Rect viewportBounds)
    {
        if (strokeBounds.IsEmpty || viewportBounds.IsEmpty)
        {
            return false;
        }

        var visibleBounds = photoInkModeActive && usePhotoTransform
            ? Rect.Transform(strokeBounds, photoTransformMatrix)
            : strokeBounds;

        return visibleBounds.IntersectsWith(viewportBounds);
    }

    internal static bool ShouldPanPhoto(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive)
    {
        return photoModeActive
            && !boardActive
            && mode == PaintToolMode.Cursor
            && !inkOperationActive;
    }

    internal static bool ShouldBegin(bool shouldPanPhoto, bool photoPanning)
    {
        return shouldPanPhoto && !photoPanning;
    }

    internal static StylusPhotoPanRoutingDecision ResolveStylusPhotoPanRouting(
        bool shouldPanPhoto,
        bool photoPanning,
        StylusPhotoPanPhase phase)
    {
        if (!shouldPanPhoto)
        {
            return StylusPhotoPanRoutingDecision.PassThrough;
        }

        return phase switch
        {
            StylusPhotoPanPhase.Down => StylusPhotoPanRoutingDecision.BeginPan,
            StylusPhotoPanPhase.Move when photoPanning => StylusPhotoPanRoutingDecision.UpdatePan,
            StylusPhotoPanPhase.Up when photoPanning => StylusPhotoPanRoutingDecision.EndPan,
            _ => StylusPhotoPanRoutingDecision.PassThrough
        };
    }

    internal static StylusPhotoPanExecutionPlan ResolveStylusPhotoPanExecution(
        StylusPhotoPanRoutingDecision routingDecision,
        bool sourceShouldContinue,
        bool sourceShouldMarkHandled,
        bool shouldBeginPan)
    {
        return routingDecision switch
        {
            StylusPhotoPanRoutingDecision.PassThrough => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.PassThrough,
                ShouldMarkHandled: false),
            StylusPhotoPanRoutingDecision.BeginPan when !sourceShouldContinue => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.ReturnWithoutPan,
                ShouldMarkHandled: sourceShouldMarkHandled),
            StylusPhotoPanRoutingDecision.BeginPan when !shouldBeginPan => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.ReturnWithoutPan,
                ShouldMarkHandled: true),
            StylusPhotoPanRoutingDecision.BeginPan => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.BeginPan,
                ShouldMarkHandled: true),
            StylusPhotoPanRoutingDecision.UpdatePan => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.UpdatePan,
                ShouldMarkHandled: true),
            StylusPhotoPanRoutingDecision.EndPan => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.EndPan,
                ShouldMarkHandled: true),
            _ => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.PassThrough,
                ShouldMarkHandled: false)
        };
    }

    internal static PhotoManipulationRoutingDecision ResolvePhotoManipulationRouting(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool photoPanning,
        int activeTouchCount)
    {
        if (boardActive)
        {
            return PhotoManipulationRoutingDecision.Consume;
        }
        if (!photoModeActive)
        {
            return PhotoManipulationRoutingDecision.Ignore;
        }
        if (inkOperationActive || photoPanning)
        {
            return PhotoManipulationRoutingDecision.Consume;
        }
        return PhotoWindowPolicies.ShouldUseManipulation(activeTouchCount)
            ? PhotoManipulationRoutingDecision.Handle
            : PhotoManipulationRoutingDecision.Consume;
    }

    internal static PhotoManipulationEventHandlingPlan ResolvePhotoManipulationEventHandling(PhotoManipulationRoutingDecision decision)
    {
        return decision switch
        {
            PhotoManipulationRoutingDecision.Handle => new PhotoManipulationEventHandlingPlan(
                ShouldHandle: true,
                ShouldMarkHandled: true),
            PhotoManipulationRoutingDecision.Consume => new PhotoManipulationEventHandlingPlan(
                ShouldHandle: false,
                ShouldMarkHandled: true),
            _ => new PhotoManipulationEventHandlingPlan(
                ShouldHandle: false,
                ShouldMarkHandled: false)
        };
    }

    internal static double ResolveTranslateYBeforeLoad(
        double currentTranslateY,
        double targetTranslateY,
        bool pageChanged,
        bool photoInkModeActive,
        bool crossPageDisplayActive)
    {
        if (pageChanged && photoInkModeActive && crossPageDisplayActive)
        {
            return targetTranslateY;
        }

        return currentTranslateY;
    }

    internal static PhotoNavigationInkViewportSyncAction ResolveAction(
        bool photoInkModeActive,
        bool interactiveSwitch)
    {
        if (!photoInkModeActive)
        {
            return PhotoNavigationInkViewportSyncAction.None;
        }

        return interactiveSwitch
            ? PhotoNavigationInkViewportSyncAction.UpdatePanCompensation
            : PhotoNavigationInkViewportSyncAction.ResetPanCompensation;
    }
}
