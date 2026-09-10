using System;
using System.Diagnostics;
using System.Globalization;

namespace ClassroomToolkit.App.Paint.Brushes;

/// <summary>
/// 输入→呈现端到端延迟与设备压感可用性的轻量遥测。
/// env 门控：CTOOLKIT_INK_LATENCY_TELEMETRY=1，默认关闭、只统计不干预行为。
/// 目的：为书写手感改造建立"先测量后动手"的基线（延迟评估），
/// 并区分真压感设备与恒 0/1 伪压感设备的占比（宽度模型归因）。
/// Environment.TickCount 粒度约 10-15ms，足够评估一帧级别的调度延迟。
/// </summary>
internal static class BrushInputLatencyTelemetry
{
    private const int Capacity = 128;
    private const int MinSamplesForSnapshot = 32;
    private const long SnapshotEmitStride = 64;
    private const int MaxPlausibleLatencyMs = 500;

    private static readonly bool Enabled = ResolveEnabledFromEnvironment();
    private static readonly object Gate = new();
    private static readonly double[] LatencyMs = new double[Capacity];
    private static readonly double[] Scratch = new double[Capacity];
    private static int _count;
    private static int _index;
    private static long _sequence;
    private static int _lastInputEventTick = int.MinValue;
    private static long _stylusSampleTotal;
    private static long _stylusPressureAvailable;

    internal static bool IsEnabled => Enabled;

    private static bool ResolveEnabledFromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("CTOOLKIT_INK_LATENCY_TELEMETRY");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        raw = raw.Trim();
        return string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
    }

    internal static void RecordInputEventTick(int eventTick)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Gate)
        {
            _lastInputEventTick = eventTick;
        }
    }

    internal static void RecordPresentedTick(int presentTick)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Gate)
        {
            if (_lastInputEventTick == int.MinValue)
            {
                return;
            }

            int deltaMs = unchecked(presentTick - _lastInputEventTick);
            _lastInputEventTick = int.MinValue;
            if (deltaMs < 0 || deltaMs > MaxPlausibleLatencyMs)
            {
                return;
            }

            LatencyMs[_index] = deltaMs;
            _index = (_index + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);
            _sequence++;
            if (_count >= MinSamplesForSnapshot && (_sequence % SnapshotEmitStride) == 0)
            {
                EmitLatencySnapshot();
            }
        }
    }

    internal static void CountStylusPressureSample(bool pressureAvailable)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Gate)
        {
            _stylusSampleTotal++;
            if (pressureAvailable)
            {
                _stylusPressureAvailable++;
            }

            if ((_stylusSampleTotal % 512) == 0)
            {
                double ratio = _stylusPressureAvailable / (double)_stylusSampleTotal;
                Debug.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"[BrushInputLatencyTelemetry] stylus_pressure_available_ratio={ratio:F3} samples={_stylusSampleTotal}"));
            }
        }
    }

    private static void EmitLatencySnapshot()
    {
        int count = _count;
        double sum = 0;
        double max = 0;
        for (int i = 0; i < count; i++)
        {
            double value = LatencyMs[i];
            sum += value;
            if (value > max)
            {
                max = value;
            }

            Scratch[i] = value;
        }

        Array.Sort(Scratch, 0, count);
        int p95Index = Math.Clamp((int)Math.Ceiling((count - 1) * 0.95), 0, count - 1);
        Debug.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"[BrushInputLatencyTelemetry] input_to_present_ms(avg/p95/max)={sum / count:F1}/{Scratch[p95Index]:F0}/{max:F0} samples={count}"));
    }
}
