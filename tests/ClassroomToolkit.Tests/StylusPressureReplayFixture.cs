using System.Collections.Generic;

namespace ClassroomToolkit.Tests;

public readonly record struct StylusPressureSample(double X, double Y, double Pressure);

public static class StylusPressureReplayFixture
{
    public static IReadOnlyList<StylusPressureSample> LegacyPseudoPressureTrace { get; } = BuildTrace(new[]
    {
        0.0, 1.0, 0.0, 1.0, 0.0, 1.0, 0.0, 1.0, 0.0
    });

    public static IReadOnlyList<StylusPressureSample> ThresholdEdgeTrace { get; } = BuildTrace(new[]
    {
        0.00006, 0.00012, 0.00018, 0.99982, 0.99988, 0.99994
    });

    public static IReadOnlyList<StylusPressureSample> ModernContinuousTrace { get; } = BuildTrace(new[]
    {
        0.22, 0.31, 0.44, 0.56, 0.68, 0.79, 0.73, 0.61, 0.49
    });

    private static IReadOnlyList<StylusPressureSample> BuildTrace(IReadOnlyList<double> pressures)
    {
        var samples = new List<StylusPressureSample>(pressures.Count);
        const double startX = 24.0;
        const double startY = 36.0;
        const double stepX = 6.0;
        const double stepY = 0.8;

        for (var i = 0; i < pressures.Count; i++)
        {
            samples.Add(new StylusPressureSample(
                startX + (i * stepX),
                startY + (i * stepY),
                pressures[i]));
        }

        return samples;
    }
}
