using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassroomToolkit.App.Paint;

internal static class InkRedrawTelemetryPolicy
{
    internal const string EnvironmentFlagName = "CTK_INK_REDRAW_TELEMETRY";

    internal static bool ResolveEnabledFromEnvironment()
    {
        return IsEnabledValue(Environment.GetEnvironmentVariable(EnvironmentFlagName));
    }

    internal static bool IsEnabledValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "1" => true,
            "TRUE" => true,
            "ON" => true,
            "YES" => true,
            "ENABLED" => true,
            _ => false
        };
    }

    internal static void AppendSample(Queue<double> samples, double value, int windowSize)
    {
        if (samples == null || windowSize <= 0 || !double.IsFinite(value))
        {
            return;
        }

        samples.Enqueue(value);
        while (samples.Count > windowSize)
        {
            samples.Dequeue();
        }
    }

    internal static double Percentile(IReadOnlyCollection<double> samples, double percentile)
    {
        if (samples == null || samples.Count == 0)
        {
            return 0;
        }

        var p = Math.Clamp(percentile, 0.0, 1.0);
        var sorted = samples.OrderBy(x => x).ToArray();
        var index = (int)Math.Floor((sorted.Length - 1) * p);
        return sorted[index];
    }

    internal static bool ShouldEmitLog(
        int sampleCount,
        DateTime nowUtc,
        DateTime lastLogUtc,
        int minSampleStride,
        double minIntervalSeconds)
    {
        if (sampleCount <= 0)
        {
            return false;
        }

        if (sampleCount < minSampleStride && (nowUtc - lastLogUtc).TotalSeconds < minIntervalSeconds)
        {
            return false;
        }

        if (sampleCount % minSampleStride != 0 && (nowUtc - lastLogUtc).TotalSeconds < minIntervalSeconds)
        {
            return false;
        }

        return true;
    }
}
