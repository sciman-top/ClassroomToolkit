using System;
using System.Diagnostics;
using System.Windows;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint.Brushes;

internal readonly record struct BrushInputSample(
    WpfPoint Position,
    long TimestampTicks,
    double Pressure,
    bool HasPressure,
    double? AzimuthRadians = null,
    double? AltitudeRadians = null,
    double? TiltXRadians = null,
    double? TiltYRadians = null)
{
    public bool HasAzimuthOrientation => AzimuthRadians.HasValue;
    public bool HasTiltOrientation => TiltXRadians.HasValue && TiltYRadians.HasValue;
    public bool HasAnyOrientation => HasAzimuthOrientation || HasTiltOrientation;

    public static BrushInputSample CreatePointer(
        WpfPoint position,
        double? azimuthRadians = null,
        double? altitudeRadians = null,
        double? tiltXRadians = null,
        double? tiltYRadians = null)
    {
        return new BrushInputSample(
            position,
            Stopwatch.GetTimestamp(),
            Pressure: 0.5,
            HasPressure: false,
            azimuthRadians,
            altitudeRadians,
            tiltXRadians,
            tiltYRadians);
    }

    public static BrushInputSample CreatePointer(
        WpfPoint position,
        long timestampTicks,
        double? azimuthRadians = null,
        double? altitudeRadians = null,
        double? tiltXRadians = null,
        double? tiltYRadians = null)
    {
        return new BrushInputSample(
            position,
            timestampTicks,
            Pressure: 0.5,
            HasPressure: false,
            azimuthRadians,
            altitudeRadians,
            tiltXRadians,
            tiltYRadians);
    }

    public static BrushInputSample CreateStylus(
        WpfPoint position,
        double pressure,
        double? azimuthRadians = null,
        double? altitudeRadians = null,
        double? tiltXRadians = null,
        double? tiltYRadians = null)
    {
        return CreateStylus(
            position,
            Stopwatch.GetTimestamp(),
            pressure,
            azimuthRadians,
            altitudeRadians,
            tiltXRadians,
            tiltYRadians);
    }

    public static BrushInputSample CreateStylus(
        WpfPoint position,
        long timestampTicks,
        double pressure,
        double? azimuthRadians = null,
        double? altitudeRadians = null,
        double? tiltXRadians = null,
        double? tiltYRadians = null)
    {
        var normalizedPressure = pressure;
        if (!double.IsFinite(normalizedPressure))
        {
            normalizedPressure = 0.5;
        }
        normalizedPressure = Math.Clamp(normalizedPressure, 0.0, 1.0);
        return new BrushInputSample(
            position,
            timestampTicks,
            normalizedPressure,
            HasPressure: true,
            azimuthRadians,
            altitudeRadians,
            tiltXRadians,
            tiltYRadians);
    }
}
