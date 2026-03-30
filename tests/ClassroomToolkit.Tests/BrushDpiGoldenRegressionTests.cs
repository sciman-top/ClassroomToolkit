using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BrushDpiGoldenRegressionTests
{
    private static readonly double[] DpiScales = { 1.0, 1.25, 1.5, 2.0 };

    [Fact]
    public void DpiGoldenHashes_ShouldMatchBaseline()
    {
        var current = BuildCurrentHashes();
        string baselinePath = ResolveBaselinePath();

        if (ShouldUpdateBaseline())
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            var updatedBaseline = new DpiGoldenBaseline
            {
                Version = 1,
                UpdatedAtUtc = DateTime.UtcNow,
                Hashes = current
            };
            var json = JsonSerializer.Serialize(updatedBaseline, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(baselinePath, json, Encoding.UTF8);
            return;
        }

        File.Exists(baselinePath).Should().BeTrue($"missing baseline file: {baselinePath}. Set CTOOLKIT_UPDATE_DPI_GOLDEN=1 to regenerate.");
        var baselineContent = File.ReadAllText(baselinePath, Encoding.UTF8);
        var baseline = JsonSerializer.Deserialize<DpiGoldenBaseline>(baselineContent);
        baseline.Should().NotBeNull();
        baseline!.Hashes.Should().NotBeNull();

        current.Keys.Should().BeEquivalentTo(baseline.Hashes.Keys);
        foreach (var pair in current)
        {
            baseline.Hashes[pair.Key].Should().Be(pair.Value, $"dpi golden mismatch for key={pair.Key}");
        }
    }

    private static Dictionary<string, string> BuildCurrentHashes()
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (double scale in DpiScales)
        {
            hashes[$"CalligraphyClarity@{scale:F2}"] = RenderNormalizedGeometryHash(BrushPhysicsConfig.CreateCalligraphyClarity(), scale);
            hashes[$"CalligraphyInkFeel@{scale:F2}"] = RenderNormalizedGeometryHash(BrushPhysicsConfig.CreateCalligraphyInkFeel(), scale);
        }
        return hashes;
    }

    private static string RenderNormalizedGeometryHash(BrushPhysicsConfig config, double scale)
    {
        config.EnableRdpSimplify = false;
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12.0 * scale, opacity: 255);

        var trace = BuildBaseTrace();
        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(Scale(trace[0], scale), now, 0.82));

        for (int i = 1; i < trace.Count - 1; i++)
        {
            now += step;
            double pressure = 0.3 + (0.62 * (0.5 + (Math.Sin(i * 0.31) * 0.5)));
            renderer.OnMove(BrushInputSample.CreateStylus(Scale(trace[i], scale), now, pressure));
        }

        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(Scale(trace[^1], scale), now, 0.58));

        var geometry = renderer.GetLastCoreGeometry();
        geometry.Should().NotBeNull();

        var normalized = geometry!.CloneCurrentValue();
        if (Math.Abs(scale - 1.0) > 0.0001)
        {
            normalized.Transform = new ScaleTransform(1.0 / scale, 1.0 / scale);
        }

        string path = InkGeometrySerializer.Serialize(normalized);
        return ComputeSha256(path);
    }

    private static List<Point> BuildBaseTrace()
    {
        return new List<Point>
        {
            new(28, 42),
            new(66, 54),
            new(112, 63),
            new(168, 76),
            new(236, 93),
            new(302, 118),
            new(374, 150),
            new(442, 177),
            new(508, 188),
            new(584, 174),
            new(652, 142),
            new(720, 108)
        };
    }

    private static Point Scale(Point p, double scale)
    {
        return new Point(p.X * scale, p.Y * scale);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static bool ShouldUpdateBaseline()
    {
        var raw = Environment.GetEnvironmentVariable("CTOOLKIT_UPDATE_DPI_GOLDEN");
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

    private static string ResolveBaselinePath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(
            baseDir,
            "..",
            "..",
            "..",
            "..",
            "Baselines",
            "brush-dpi-golden.json"));
    }

    private sealed class DpiGoldenBaseline
    {
        public int Version { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public Dictionary<string, string> Hashes { get; set; } = new(StringComparer.Ordinal);
    }
}
