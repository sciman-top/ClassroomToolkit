using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace ClassroomToolkit.App.Paint.Brushes;

internal readonly record struct BrushMoveTelemetrySnapshot(
    string PresetName,
    string ModeTag,
    double DtAvgMs,
    double DtP95Ms,
    double DtMaxMs,
    double AllocAvgBytes,
    double AllocP95Bytes,
    long AllocMaxBytes,
    double RawAvgPoints,
    double RawP95Points,
    int RawMaxPoints,
    double ResampledAvgPoints,
    double ResampledP95Points,
    int ResampledMaxPoints,
    double EffectiveTaperBaseAvgDip,
    double EffectiveTaperBaseP95Dip,
    double EffectiveTaperBaseMinDip,
    double EffectiveTaperBaseMaxDip);

internal partial class VariableWidthBrushRenderer
{
    // env 门控目录：设置 CTOOLKIT_BRUSH_WIDTH_PROFILE_DIR 后每笔落盘
    // width-vs-弧长 CSV，供参数调优做数据驱动对比；未设置时零开销。
    private static readonly string? WidthProfileDirectory = ResolveWidthProfileDirectoryFromEnvironment();

    private static string? ResolveWidthProfileDirectoryFromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("CTOOLKIT_BRUSH_WIDTH_PROFILE_DIR");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var directory = raw.Trim();
        return Directory.Exists(directory) ? directory : null;
    }

    private void WriteWidthProfileCsvIfEnabled()
    {
        var directory = WidthProfileDirectory;
        if (directory == null || _points.Count < 2)
        {
            return;
        }

        try
        {
            var samples = BuildCenterlineSamplesFinal();
            if (samples.Count < 2)
            {
                return;
            }

            var builder = new StringBuilder(samples.Count * 32);
            builder.AppendLine("arclength_dip,width_dip,speed_px_per_ms");
            builder.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"0,{samples[0].Width:F4},{samples[0].Speed:F4}"));
            double cumulative = 0.0;
            for (int i = 1; i < samples.Count; i++)
            {
                cumulative += (samples[i].Position - samples[i - 1].Position).Length;
                builder.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{cumulative:F3},{samples[i].Width:F4},{samples[i].Speed:F4}"));
            }

            var fileName = $"brush-width-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{_config.PresetName}.csv";
            File.WriteAllText(Path.Combine(directory, fileName), builder.ToString());
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            // 诊断输出失败绝不影响笔画收口。
        }
    }

    internal bool TryGetMoveTelemetrySnapshotForDiagnostics(out BrushMoveTelemetrySnapshot snapshot)
    {
        return _moveTelemetry.TryGetLastSnapshot(out snapshot);
    }

    private bool IsMoveTelemetryEnabled()
    {
        return _config.EnableDebugMoveTelemetry || BrushMoveTelemetryFlag;
    }

    private static bool ResolveTelemetryFlagFromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("CTOOLKIT_BRUSH_TELEMETRY");
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

    private sealed class BrushMoveTelemetry
    {
        private const int Capacity = 256;
        private readonly double[] _dtMs = new double[Capacity];
        private readonly long[] _allocBytes = new long[Capacity];
        private readonly int[] _rawPoints = new int[Capacity];
        private readonly int[] _resampledPoints = new int[Capacity];
        private readonly double[] _effectiveTaperBaseDip = new double[Capacity];
        private readonly double[] _scratchDtMs = new double[Capacity];
        private readonly long[] _scratchAllocBytes = new long[Capacity];
        private readonly int[] _scratchRawPoints = new int[Capacity];
        private readonly int[] _scratchResampledPoints = new int[Capacity];
        private readonly double[] _scratchEffectiveTaperBaseDip = new double[Capacity];
        private int _count;
        private int _index;
        private long _sequence;
        private BrushMoveTelemetrySnapshot _lastSnapshot;
        private bool _hasSnapshot;

        public void Record(
            double dtMs,
            long allocBytes,
            int rawPoints,
            int resampledPoints,
            double effectiveTaperBaseDip,
            string presetName,
            string modeTag)
        {
            _dtMs[_index] = Math.Max(0.0, dtMs);
            _allocBytes[_index] = Math.Max(0, allocBytes);
            _rawPoints[_index] = Math.Max(0, rawPoints);
            _resampledPoints[_index] = Math.Max(0, resampledPoints);
            _effectiveTaperBaseDip[_index] = Math.Max(0.0, effectiveTaperBaseDip);
            _index = (_index + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);
            _sequence++;

            if (_count < 16 || (_sequence % 64) != 0)
            {
                return;
            }

            EmitSnapshot(presetName, modeTag);
        }

        private void EmitSnapshot(string presetName, string modeTag)
        {
            int count = _count;
            if (count <= 0)
            {
                return;
            }

            double dtSum = 0.0;
            double dtMax = 0.0;
            long allocSum = 0;
            long allocMax = 0;
            long rawSum = 0;
            int rawMax = 0;
            long resampledSum = 0;
            int resampledMax = 0;
            double effectiveBaseSum = 0.0;
            double effectiveBaseMin = double.MaxValue;
            double effectiveBaseMax = 0.0;

            for (int i = 0; i < count; i++)
            {
                double dt = _dtMs[i];
                long alloc = _allocBytes[i];
                int raw = _rawPoints[i];
                int resampled = _resampledPoints[i];
                double effectiveBase = _effectiveTaperBaseDip[i];

                dtSum += dt;
                if (dt > dtMax)
                {
                    dtMax = dt;
                }

                allocSum += alloc;
                if (alloc > allocMax)
                {
                    allocMax = alloc;
                }

                rawSum += raw;
                if (raw > rawMax)
                {
                    rawMax = raw;
                }

                resampledSum += resampled;
                if (resampled > resampledMax)
                {
                    resampledMax = resampled;
                }

                effectiveBaseSum += effectiveBase;
                if (effectiveBase < effectiveBaseMin)
                {
                    effectiveBaseMin = effectiveBase;
                }
                if (effectiveBase > effectiveBaseMax)
                {
                    effectiveBaseMax = effectiveBase;
                }

                _scratchDtMs[i] = dt;
                _scratchAllocBytes[i] = alloc;
                _scratchRawPoints[i] = raw;
                _scratchResampledPoints[i] = resampled;
                _scratchEffectiveTaperBaseDip[i] = effectiveBase;
            }

            double dtAvg = dtSum / count;
            double allocAvg = (double)allocSum / count;
            double rawAvg = (double)rawSum / count;
            double resampledAvg = (double)resampledSum / count;
            double effectiveBaseAvg = effectiveBaseSum / count;
            if (effectiveBaseMin == double.MaxValue)
            {
                effectiveBaseMin = 0.0;
            }

            double dtP95 = PercentileInPlace(_scratchDtMs, count, 0.95);
            double allocP95 = PercentileInPlace(_scratchAllocBytes, count, 0.95);
            double rawP95 = PercentileInPlace(_scratchRawPoints, count, 0.95);
            double resampledP95 = PercentileInPlace(_scratchResampledPoints, count, 0.95);
            double effectiveBaseP95 = PercentileInPlace(_scratchEffectiveTaperBaseDip, count, 0.95);

            _lastSnapshot = new BrushMoveTelemetrySnapshot(
                presetName,
                modeTag,
                dtAvg,
                dtP95,
                dtMax,
                allocAvg,
                allocP95,
                allocMax,
                rawAvg,
                rawP95,
                rawMax,
                resampledAvg,
                resampledP95,
                resampledMax,
                effectiveBaseAvg,
                effectiveBaseP95,
                effectiveBaseMin,
                effectiveBaseMax);
            _hasSnapshot = true;

            Debug.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"[BrushMoveTelemetry] preset={presetName} mode={modeTag} " +
                    $"dt_ms(avg/p95/max)={dtAvg:F3}/{dtP95:F3}/{dtMax:F3} " +
                    $"alloc_bytes(avg/p95/max)={allocAvg:F0}/{allocP95:F0}/{allocMax} " +
                    $"points_raw(avg/p95/max)={rawAvg:F1}/{rawP95:F1}/{rawMax} " +
                    $"points_resampled(avg/p95/max)={resampledAvg:F1}/{resampledP95:F1}/{resampledMax} " +
                    $"taper_base_dip(avg/p95/min/max)={effectiveBaseAvg:F2}/{effectiveBaseP95:F2}/{effectiveBaseMin:F2}/{effectiveBaseMax:F2}"));
        }

        public bool TryGetLastSnapshot(out BrushMoveTelemetrySnapshot snapshot)
        {
            snapshot = _lastSnapshot;
            return _hasSnapshot;
        }

        private static double PercentileInPlace(double[] values, int count, double q)
        {
            if (count <= 0)
            {
                return 0.0;
            }

            Array.Sort(values, 0, count);
            int idx = Math.Clamp((int)Math.Ceiling((count - 1) * q), 0, count - 1);
            return values[idx];
        }

        private static double PercentileInPlace(long[] values, int count, double q)
        {
            if (count <= 0)
            {
                return 0.0;
            }

            Array.Sort(values, 0, count);
            int idx = Math.Clamp((int)Math.Ceiling((count - 1) * q), 0, count - 1);
            return values[idx];
        }

        private static double PercentileInPlace(int[] values, int count, double q)
        {
            if (count <= 0)
            {
                return 0.0;
            }

            Array.Sort(values, 0, count);
            int idx = Math.Clamp((int)Math.Ceiling((count - 1) * q), 0, count - 1);
            return values[idx];
        }
    }
}
