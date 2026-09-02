namespace ClassroomToolkit.App.Paint;

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
