using System.Diagnostics;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private static double ResolveStylusPressureGamma(ClassroomWritingMode mode)
    {
        return mode switch
        {
            ClassroomWritingMode.Stable => StylusRuntimeDefaults.PressureGammaStable,
            ClassroomWritingMode.Responsive => StylusRuntimeDefaults.PressureGammaResponsive,
            _ => StylusRuntimeDefaults.PressureGammaDefault
        };
    }

    private void MarkPhotoGestureInput()
    {
        _lastPhotoGestureInputUtc = GetCurrentUtcTimestamp();
    }

    private void MarkPhotoZoomInput()
    {
        _lastPhotoZoomInputUtc = GetCurrentUtcTimestamp();
    }

    private bool IsPhotoZoomInteractionActive()
    {
        return PhotoInputConflictGuard.ShouldSuppressWheelAfterGesture(
            _lastPhotoZoomInputUtc,
            PhotoZoomInteractionWindowMs,
            GetCurrentUtcTimestamp());
    }

    private bool ShouldSuppressPhotoWheelFromRecentGesture()
    {
        return PhotoInputConflictGuard.ShouldSuppressWheelAfterGesture(
            _lastPhotoGestureInputUtc,
            PhotoWheelSuppressAfterGestureMs,
            GetCurrentUtcTimestamp());
    }

    private void LogPhotoInputTelemetry(string eventType, string payload)
    {
        if (!_photoInputTelemetryEnabled)
        {
            return;
        }
        Debug.WriteLine(
            $"[PhotoInputTelemetry] type={eventType}; {payload}; " +
            $"scale={_photoScale.ScaleX:0.###}; tx={_photoTranslate.X:0.##}; ty={_photoTranslate.Y:0.##}");
    }

    private bool IsCrossPageFirstInputTraceActive()
    {
        return false;
    }

    private void BeginCrossPageFirstInputTrace(int fromPage, int toPage)
    {
    }

    private void MarkCrossPageFirstInputStage(string stage, string? details = null)
    {
    }

    private void EndCrossPageFirstInputTrace(string outcome)
    {
    }
}
