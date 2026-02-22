using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BrushTelemetrySnapshotTests
{
    private static readonly object OutputFileLock = new();

    [Fact]
    public void TelemetrySnapshot_ShouldCoverClarityAndInkFeel_WithTaperBaseStats()
    {
        var snapshots = new List<BrushMoveTelemetrySnapshot>
        {
            ReplayAndCapture(BrushPhysicsConfig.CreateCalligraphyClarity(), "Clarity"),
            ReplayAndCapture(BrushPhysicsConfig.CreateCalligraphyInkFeel(), "InkFeel")
        };

        snapshots.Should().HaveCount(2);
        foreach (var snapshot in snapshots)
        {
            snapshot.DtP95Ms.Should().BeGreaterThanOrEqualTo(0);
            snapshot.AllocP95Bytes.Should().BeGreaterThanOrEqualTo(0);
            snapshot.EffectiveTaperBaseAvgDip.Should().BeGreaterThan(0);
            snapshot.EffectiveTaperBaseMaxDip.Should().BeGreaterThanOrEqualTo(snapshot.EffectiveTaperBaseMinDip);
        }

        WriteSnapshotsIfRequested(snapshots);
    }

    private static BrushMoveTelemetrySnapshot ReplayAndCapture(BrushPhysicsConfig config, string expectedMode)
    {
        config.EnableRdpSimplify = false;
        config.EnableDebugMoveTelemetry = true;
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(24, 40), now, 0.72));

        for (int i = 1; i <= 160; i++)
        {
            now += step;
            double progress = i / 160.0;
            double x = 24 + (progress * 720.0);
            double y = 40 + (Math.Sin(progress * 11.0) * 22.0) + (Math.Cos(progress * 5.8) * 8.0);
            double pressure = 0.28 + (0.6 * (0.5 + (Math.Sin(progress * 18.0) * 0.5)));
            renderer.OnMove(BrushInputSample.CreateStylus(new Point(x, y), now, pressure));
            if ((i % 16) == 0)
            {
                _ = renderer.GetPreviewCoreGeometry();
            }
        }

        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(744, 72), now, 0.56));

        bool ok = renderer.TryGetMoveTelemetrySnapshotForDiagnostics(out var snapshot);
        ok.Should().BeTrue();
        snapshot.PresetName.Should().NotBeNullOrWhiteSpace();
        snapshot.ModeTag.Should().Be(expectedMode);
        return snapshot;
    }

    private static void WriteSnapshotsIfRequested(IReadOnlyList<BrushMoveTelemetrySnapshot> snapshots)
    {
        var outputPath = Environment.GetEnvironmentVariable("CTOOLKIT_TELEMETRY_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var lines = new List<string>(snapshots.Count);
        foreach (var snapshot in snapshots)
        {
            var payload = new
            {
                preset = snapshot.PresetName,
                mode = snapshot.ModeTag,
                dt_avg_ms = snapshot.DtAvgMs,
                dt_p95_ms = snapshot.DtP95Ms,
                dt_max_ms = snapshot.DtMaxMs,
                alloc_avg_bytes = snapshot.AllocAvgBytes,
                alloc_p95_bytes = snapshot.AllocP95Bytes,
                alloc_max_bytes = snapshot.AllocMaxBytes,
                raw_avg_points = snapshot.RawAvgPoints,
                raw_p95_points = snapshot.RawP95Points,
                raw_max_points = snapshot.RawMaxPoints,
                resampled_avg_points = snapshot.ResampledAvgPoints,
                resampled_p95_points = snapshot.ResampledP95Points,
                resampled_max_points = snapshot.ResampledMaxPoints,
                taper_base_avg_dip = snapshot.EffectiveTaperBaseAvgDip,
                taper_base_p95_dip = snapshot.EffectiveTaperBaseP95Dip,
                taper_base_min_dip = snapshot.EffectiveTaperBaseMinDip,
                taper_base_max_dip = snapshot.EffectiveTaperBaseMaxDip,
                captured_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            lines.Add(JsonSerializer.Serialize(payload));
        }

        lock (OutputFileLock)
        {
            File.AppendAllLines(outputPath, lines);
        }
    }
}
