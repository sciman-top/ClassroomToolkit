using System;
using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct StylusOrientationSample(
    double? AzimuthRadians,
    double? AltitudeRadians,
    double? TiltXRadians,
    double? TiltYRadians)
{
    public bool HasAny => AzimuthRadians.HasValue || (TiltXRadians.HasValue && TiltYRadians.HasValue);
}

internal static class StylusOrientationResolver
{
    public static StylusOrientationSample Resolve(StylusPoint stylusPoint)
    {
        double? azimuth = ResolveCircularRadians(stylusPoint, StylusPointProperties.AzimuthOrientation);
        double? altitude = ResolveLinearRadians(stylusPoint, StylusPointProperties.AltitudeOrientation, Math.PI * 0.5);
        double? tiltX = ResolveSignedRadians(stylusPoint, StylusPointProperties.XTiltOrientation, Math.PI * 0.5);
        double? tiltY = ResolveSignedRadians(stylusPoint, StylusPointProperties.YTiltOrientation, Math.PI * 0.5);

        return new StylusOrientationSample(azimuth, altitude, tiltX, tiltY);
    }

    private static double? ResolveCircularRadians(StylusPoint stylusPoint, StylusPointProperty property)
    {
        if (!TryGetRawWithRange(stylusPoint, property, out var raw, out var min, out var max))
        {
            return null;
        }

        var range = max - min;
        if (range < 1)
        {
            return null;
        }

        var normalized = Math.Clamp((raw - min) / range, 0.0, 1.0);
        return normalized * Math.PI * 2.0;
    }

    private static double? ResolveLinearRadians(StylusPoint stylusPoint, StylusPointProperty property, double maxRadians)
    {
        if (!TryGetRawWithRange(stylusPoint, property, out var raw, out var min, out var max))
        {
            return null;
        }

        var range = max - min;
        if (range < 1)
        {
            return null;
        }

        var normalized = Math.Clamp((raw - min) / range, 0.0, 1.0);
        return normalized * maxRadians;
    }

    private static double? ResolveSignedRadians(StylusPoint stylusPoint, StylusPointProperty property, double maxRadians)
    {
        if (!TryGetRawWithRange(stylusPoint, property, out var raw, out var min, out var max))
        {
            return null;
        }

        var halfRange = (max - min) * 0.5;
        if (halfRange < 1)
        {
            return null;
        }

        var mid = min + halfRange;
        var signed = Math.Clamp((raw - mid) / halfRange, -1.0, 1.0);
        return signed * maxRadians;
    }

    private static bool TryGetRawWithRange(
        StylusPoint stylusPoint,
        StylusPointProperty property,
        out double raw,
        out double min,
        out double max)
    {
        raw = 0;
        min = 0;
        max = 0;

        var description = stylusPoint.Description;
        if (!description.HasProperty(property))
        {
            return false;
        }

        var resolveResult = PaintActionInvoker.TryInvoke(() =>
        {
            var resolvedRaw = stylusPoint.GetPropertyValue(property);
            var info = description.GetPropertyInfo(property);
            var resolvedMin = info.Minimum;
            var resolvedMax = info.Maximum;
            var valid = double.IsFinite(resolvedRaw) && double.IsFinite(resolvedMin) && double.IsFinite(resolvedMax);
            return (Valid: valid, Raw: resolvedRaw, Min: resolvedMin, Max: resolvedMax);
        }, fallback: (Valid: false, Raw: 0d, Min: 0d, Max: 0d));
        if (!resolveResult.Valid)
        {
            return false;
        }

        raw = resolveResult.Raw;
        min = resolveResult.Min;
        max = resolveResult.Max;
        return true;
    }
}

