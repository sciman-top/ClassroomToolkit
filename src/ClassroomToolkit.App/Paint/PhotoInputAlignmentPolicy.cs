namespace ClassroomToolkit.App.Paint;

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
            var sensitivity = Math.Clamp(gestureSensitivity, 0.2, 3.0);
            candidate = 1.0 + ((candidate - 1.0) * sensitivity);
        }

        if (source == PhotoZoomInputSource.Gesture && Math.Abs(candidate - 1.0) < Math.Max(0.0, gestureNoiseThreshold))
        {
            return false;
        }

        var minFactor = Math.Max(0.01, Math.Min(minEventFactor, maxEventFactor));
        var maxFactor = Math.Max(minFactor, Math.Max(minEventFactor, maxEventFactor));
        candidate = Math.Clamp(candidate, minFactor, maxFactor);
        if (Math.Abs(candidate - 1.0) < 0.001)
        {
            return false;
        }

        factor = candidate;
        return true;
    }
}

internal static class StylusCursorPolicy
{
    internal static bool ShouldPanPhoto(bool photoModeActive, bool boardActive, PaintToolMode mode)
    {
        return photoModeActive && !boardActive && mode == PaintToolMode.Cursor;
    }
}

internal static class PhotoPanLimiter
{
    internal static double ApplyAxis(double value, double min, double max, bool allowResistance, double resistanceFactor = 0.35)
    {
        if (min > max)
        {
            (min, max) = (max, min);
        }
        if (!allowResistance)
        {
            return Math.Clamp(value, min, max);
        }
        var factor = Math.Clamp(resistanceFactor, 0.05, 0.95);
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
        if (lastGestureUtc == DateTime.MinValue)
        {
            return false;
        }
        var window = Math.Max(0, suppressWindowMs);
        if (window == 0)
        {
            return false;
        }
        return (nowUtc - lastGestureUtc).TotalMilliseconds <= window;
    }
}
