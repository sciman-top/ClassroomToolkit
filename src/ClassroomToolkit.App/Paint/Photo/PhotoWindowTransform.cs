using System.Windows;
using System;
using WpfPoint
=
System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

internal enum PhotoLoadedBitmapTransformPath
{
    ApplyUnifiedTransform = 0,
    FitToViewport = 1,
    TryStoredTransformThenFit = 2
}

internal readonly record struct PhotoManipulationDeltaExecutionPlan(
    bool ShouldApplyTranslation,
    bool ShouldLogPanTelemetry,
    bool ShouldRequestCrossPageUpdate);

internal static class PhotoTransformMathDefaults
{
    internal const double InverseScaleEpsilon = 0.0001;
}

internal static class PhotoTransformTimingDefaults
{
    internal const int WheelSuppressAfterGestureMs = 180;
    internal const int ZoomInteractionWindowMs = 180;
    internal const int RenderQualityRestoreDelayMs = 180;
    // Wheel input arrives in coarse 120-unit steps.  A frame-based response
    // keeps the target scale unchanged while letting the visual scale converge
    // over several compositor frames instead of jumping one whole notch.
    internal const double SmoothZoomResponseMs = 78.0;
    internal const double SmoothZoomFrameEpsilon = 0.0005;
    internal const int TransformSaveDebounceMs = 120;
    internal const int UnifiedTransformBroadcastDebounceMs = 300;
}

internal static class PhotoTransformViewportDefaults
{
    internal const double MinUsableViewportDip = 1.0;
    internal const double DefaultScale = 1.0;
    internal const double MinScale = 0.2;
    internal const double MaxScale = 4.0;
}

internal static class PhotoUnifiedTransformDefaults
{
    internal const double DefaultTranslateDip = 0.0;

    internal static double NormalizeScale(double value)
    {
        return double.IsFinite(value)
            ? Math.Clamp(value, PhotoTransformViewportDefaults.MinScale, PhotoTransformViewportDefaults.MaxScale)
            : PhotoTransformViewportDefaults.DefaultScale;
    }

    internal static double NormalizeTranslation(double value)
    {
        return double.IsFinite(value) ? value : DefaultTranslateDip;
    }
}

internal static class PhotoZoomInputDefaults
{
    internal const double WheelZoomBaseDefault = 1.0008;
    internal const double WheelZoomBaseMin = 1.0002;
    internal const double WheelZoomBaseMax = 1.0020;
    internal const double GestureSensitivityDefault = 1.0;
    internal const double GestureSensitivityMin = 0.5;
    internal const double GestureSensitivityMax = 1.8;
    internal const double GestureZoomNoiseThreshold = 0.01;
    internal const double ZoomMinEventFactor = 0.85;
    internal const double ZoomMaxEventFactor = 1.18;
    internal const double ScaleApplyEpsilon = 0.001;
    internal const double ManipulationTranslationEpsilonDip = 0.01;
}

internal static class PhotoWindowTransformPolicies
{
    internal static PhotoLoadedBitmapTransformPath ResolvePhotoLoadedBitmapTransformPath(
        bool useCrossPageUnifiedPath,
        bool rememberPhotoTransform,
        bool photoUnifiedTransformReady)
    {
        if (!rememberPhotoTransform)
        {
            return PhotoLoadedBitmapTransformPath.FitToViewport;
        }

        if (!useCrossPageUnifiedPath)
        {
            return PhotoLoadedBitmapTransformPath.TryStoredTransformThenFit;
        }

        return photoUnifiedTransformReady
            ? PhotoLoadedBitmapTransformPath.ApplyUnifiedTransform
            : PhotoLoadedBitmapTransformPath.FitToViewport;
    }

    internal static PhotoManipulationEventHandlingPlan ResolvePhotoManipulationAdmission(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool photoPanning,
        int activeTouchCount)
    {
        var decision = PhotoInkInteropPolicies.ResolvePhotoManipulationRouting(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive,
            photoPanning,
            activeTouchCount);
        return PhotoInkInteropPolicies.ResolvePhotoManipulationEventHandling(decision);
    }

    internal static PhotoManipulationDeltaExecutionPlan ResolvePhotoManipulationDeltaExecution(
        Vector translation,
        double translationEpsilonDip,
        bool crossPageDisplayActive)
    {
        var shouldApplyTranslation =
            Math.Abs(translation.X) > translationEpsilonDip
            || Math.Abs(translation.Y) > translationEpsilonDip;
        return new PhotoManipulationDeltaExecutionPlan(
            ShouldApplyTranslation: shouldApplyTranslation,
            ShouldLogPanTelemetry: shouldApplyTranslation,
            ShouldRequestCrossPageUpdate: crossPageDisplayActive);
    }

    internal static double ResolveTranslationDeceleration(bool crossPageDisplayActive)
    {
        return ResolveTranslationDeceleration(
            crossPageDisplayActive,
            PhotoPanInertiaTuning.Default);
    }

    internal static double ResolveTranslationDeceleration(
        bool crossPageDisplayActive,
        PhotoPanInertiaTuning tuning)
    {
        return crossPageDisplayActive
            ? tuning.GestureCrossPageTranslationDecelerationDipPerMs2
            : tuning.GestureTranslationDecelerationDipPerMs2;
    }

    internal static bool ShouldResetUserDirtyState(bool rememberPhotoTransform)
    {
        return !rememberPhotoTransform;
    }

    internal static bool ShouldResetUnifiedTransformState(bool rememberPhotoTransform)
    {
        return !rememberPhotoTransform;
    }

    internal static bool ShouldApplyRuntimeTransform(
        bool rememberPhotoTransform,
        bool photoInkModeActive,
        bool crossPageDisplayActive)
    {
        return rememberPhotoTransform
            && photoInkModeActive
            && crossPageDisplayActive;
    }

    internal static WpfPoint ResolveViewportCenter(FrameworkElement? viewport)
    {
        if (viewport == null)
        {
            return default;
        }

        return ResolveViewportCenter(viewport.ActualWidth, viewport.ActualHeight);
    }

    internal static WpfPoint ResolveViewportCenter(double viewportWidth, double viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return default;
        }

        return new WpfPoint(viewportWidth * 0.5, viewportHeight * 0.5);
    }
}
