using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class VariableWidthBrushStabilityTests
{
    [Fact]
    public void OnMove_ShouldClampStartWidthBurst_ForSlowHighPressureStart()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config.EnableRdpSimplify = false;
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);

        renderer.OnDown(BrushInputSample.CreateStylus(new Point(20, 24), now, 0.98));
        for (int i = 1; i <= 24; i++)
        {
            now += step;
            var x = 20 + (i * 2.25);
            var y = 24 + (i * 1.15);
            renderer.OnMove(BrushInputSample.CreateStylus(new Point(x, y), now, 0.98));
        }
        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(74, 52), now, 0.98));

        var points = renderer.GetLastStrokePoints();
        points.Should().NotBeNull();
        points!.Count.Should().BeGreaterThanOrEqualTo(3);

        int startWindow = Math.Min(config.StartBurstSuppressPoints + 2, points.Count);
        var startMax = points.Take(startWindow).Max(point => point.Width);
        var allowed = (12.0 * config.StartBurstMaxWidthFactor) + 0.9;
        startMax.Should().BeLessThanOrEqualTo(allowed);
    }

    [Fact]
    public void OnMove_ShouldKeepWidthTransitionContinuous_UnderPressureOscillation()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config.EnableRdpSimplify = false;
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);

        renderer.OnDown(BrushInputSample.CreateStylus(new Point(40, 120), now, 0.45));
        for (int i = 1; i <= 72; i++)
        {
            now += step;
            double progress = i / 72.0;
            var x = 40 + (progress * 560.0);
            var y = 120 + (Math.Sin(progress * 9.4) * 18.0) + (Math.Cos(progress * 19.0) * 5.0);
            var pressure = 0.2 + (0.75 * (0.5 + (Math.Sin(progress * 20.0) * 0.5)));
            renderer.OnMove(BrushInputSample.CreateStylus(new Point(x, y), now, pressure));
        }
        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(604, 136), now, 0.55));

        var points = renderer.GetLastStrokePoints();
        points.Should().NotBeNull();
        points!.Count.Should().BeGreaterThanOrEqualTo(8);

        var deltas = new List<double>(points.Count - 1);
        for (int i = 1; i < points.Count; i++)
        {
            deltas.Add(Math.Abs(points[i].Width - points[i - 1].Width));
        }

        deltas.Count.Should().BeGreaterThan(10);
        var stableWindow = deltas.Skip(Math.Min(3, deltas.Count - 1)).ToArray();
        stableWindow.Length.Should().BeGreaterThan(4);
        var sorted = stableWindow.OrderBy(v => v).ToArray();
        double p95 = sorted[(int)Math.Floor((sorted.Length - 1) * 0.95)];
        p95.Should().BeLessThan(5.2);
    }

    [Fact]
    public void OnMove_ShouldSuppressInkBlob_WhenStrokeCrossesSameAreaRepeatedly()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config.EnableRdpSimplify = false;
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);

        var trace = new[]
        {
            new Point(120, 120),
            new Point(180, 180),
            new Point(240, 120),
            new Point(180, 60),
            new Point(120, 120),
            new Point(180, 180),
            new Point(240, 120),
            new Point(180, 60),
            new Point(120, 120)
        };

        renderer.OnDown(BrushInputSample.CreateStylus(trace[0], now, 0.92));
        for (int i = 1; i < trace.Length - 1; i++)
        {
            now += step;
            renderer.OnMove(BrushInputSample.CreateStylus(trace[i], now, 0.92));
        }
        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(trace[^1], now, 0.92));

        var points = renderer.GetLastStrokePoints();
        points.Should().NotBeNull();
        points!.Count.Should().BeGreaterThanOrEqualTo(6);

        var center = new Point(180, 120);
        var nearCrossWidths = points
            .Where(p => (p.Position - center).Length <= 28.0)
            .Select(p => p.Width)
            .ToList();
        nearCrossWidths.Should().NotBeEmpty();

        double crossingMax = nearCrossWidths.Max();
        crossingMax.Should().BeLessThan(23.0);
    }

    [Fact]
    public void OnUp_ShouldNarrowTailWidth_ForCleanerCalligraphyEnding()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config.EnableRdpSimplify = false;
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);

        renderer.OnDown(BrushInputSample.CreateStylus(new Point(32, 40), now, 0.88));
        for (int i = 1; i <= 28; i++)
        {
            now += step;
            var x = 32 + (i * 4.8);
            var y = 40 + (Math.Sin(i * 0.26) * 7.0) + (i * 0.95);
            renderer.OnMove(BrushInputSample.CreateStylus(new Point(x, y), now, 0.88));
        }
        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(170, 78), now, 0.82));

        var points = renderer.GetLastStrokePoints();
        points.Should().NotBeNull();
        points!.Count.Should().BeGreaterThanOrEqualTo(4);

        double tailWidth = points[^1].Width;
        double beforeTail = points[^2].Width;

        tailWidth.Should().BeLessThan(beforeTail);
        tailWidth.Should().BeLessThanOrEqualTo(12.0 * 0.62);
    }

    [Fact]
    public void OnUp_ShouldKeepRawEndpointAtActualReleasePosition()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config.EnableRdpSimplify = false;
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        var release = new Point(168, 76);

        renderer.OnDown(BrushInputSample.CreateStylus(new Point(32, 40), now, 0.82));
        for (int i = 1; i <= 20; i++)
        {
            now += step;
            renderer.OnMove(BrushInputSample.CreateStylus(
                new Point(32 + (i * 6.0), 40 + (i * 1.8)), now, 0.82));
        }

        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(release, now, 0.82));

        var points = renderer.GetLastStrokePoints();
        points.Should().NotBeNull();
        points!.Last().Position.Should().Be(release);
    }

    [Fact]
    public void InkFlow_ShouldBeStableWhenSameSlowTraceUsesDifferentInputCadence()
    {
        var config60 = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config60.EnableRdpSimplify = false;
        var config240 = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config240.EnableRdpSimplify = false;

        var renderer60 = new VariableWidthBrushRenderer(config60);
        var renderer240 = new VariableWidthBrushRenderer(config240);
        renderer60.Initialize(Colors.Black, baseSize: 12, opacity: 255);
        renderer240.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        ReplaySlowLine(renderer60, 60);
        ReplaySlowLine(renderer240, 240);

        Math.Abs(renderer60.LastInkFlow - renderer240.LastInkFlow).Should().BeLessThan(0.12);
    }

    private static void ReplaySlowLine(VariableWidthBrushRenderer renderer, int hz)
    {
        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / hz);
        const int durationMs = 900;
        int count = Math.Max(2, (durationMs * hz) / 1000);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(40, 140), now, 0.86));
        for (int i = 1; i <= count; i++)
        {
            now += step;
            double t = i / (double)count;
            renderer.OnMove(BrushInputSample.CreateStylus(
                new Point(40 + (t * 220), 140 + (Math.Sin(t * 2.1) * 9)), now, 0.86));
        }

        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(260, 140), now, 0.86));
    }

    [Fact]
    public void OnMove_ShouldCapRawAndResampledPoints_ForLongStroke()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config.EnableRdpSimplify = false;
        config.MaxRawPointCount = 420;
        config.MaxResampledPointCount = 180;
        config.ArcLengthResampleStepPx = 0.9;
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(24, 40), now, 0.72));

        for (int i = 1; i <= 1400; i++)
        {
            now += step;
            double progress = i / 1400.0;
            double x = 24 + (progress * 1600.0);
            double y = 40 + (Math.Sin(progress * 28.0) * 26.0);
            renderer.OnMove(BrushInputSample.CreateStylus(new Point(x, y), now, 0.72));
        }

        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(1624, 60), now, 0.72));
        renderer.GetLastCoreGeometry().Should().NotBeNull();

        var points = renderer.GetLastStrokePoints();
        points.Should().NotBeNull();
        // 摊销裁剪：触发点之前允许滞后块大小的超量，最终仍收敛到上限。
        points!.Count.Should().BeLessThanOrEqualTo(
            VariableWidthBrushRenderer.ResolveRawPointTrimTriggerCount(config.MaxRawPointCount) + 1);
        renderer.LastResampledPointCount.Should().BeLessThanOrEqualTo(config.MaxResampledPointCount);
    }

    [Fact]
    public void Rendering_ShouldBeDeterministic_ForSameInputTrace()
    {
        var configA = BrushPhysicsConfig.CreateCalligraphyBalanced();
        configA.EnableRdpSimplify = false;
        var configB = BrushPhysicsConfig.CreateCalligraphyBalanced();
        configB.EnableRdpSimplify = false;

        var rendererA = new VariableWidthBrushRenderer(configA);
        var rendererB = new VariableWidthBrushRenderer(configB);
        rendererA.Initialize(Colors.Black, baseSize: 12, opacity: 255);
        rendererB.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long start = Stopwatch.Frequency;
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        ReplayDeterministicTrace(rendererA, start, step);
        ReplayDeterministicTrace(rendererB, start, step);

        var geometryA = rendererA.GetLastCoreGeometry();
        var geometryB = rendererB.GetLastCoreGeometry();
        geometryA.Should().NotBeNull();
        geometryB.Should().NotBeNull();

        var pathA = InkGeometrySerializer.Serialize(geometryA!);
        var pathB = InkGeometrySerializer.Serialize(geometryB!);
        pathA.Should().Be(pathB);
    }

    [Fact]
    public void OnUp_ShouldProduceSharperTips_WhenUsingExposedTaperStyle()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        config.EnableRdpSimplify = false;
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(20, 30), now, 0.85));
        for (int i = 1; i <= 60; i++)
        {
            now += step;
            double progress = i / 60.0;
            renderer.OnMove(BrushInputSample.CreateStylus(
                new Point(20 + progress * 420.0, 30 + Math.Sin(progress * 6.0) * 20.0),
                now,
                0.75));
        }

        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(460, 44), now, 0.7));
        renderer.GetLastCoreGeometry().Should().NotBeNull();

        var points = renderer.GetLastStrokePoints();
        points.Should().NotBeNull();
        points!.Count.Should().BeGreaterThan(12);

        double startWidth = points[0].Width;
        double midWidth = points[points.Count / 2].Width;
        double endWidth = points[^1].Width;
        startWidth.Should().BeLessThan(midWidth * 0.45);
        endWidth.Should().BeLessThan(midWidth * 0.45);
    }

    [Fact]
    public void ExposedTaper_ShouldReachARealOutlineTip_BeyondReleasePoint()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        config.EnableRdpSimplify = false;
        config.EnableMultiRibbon = true;
        config.MultiRibbonCount = 3;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(24, 120), now, 0.82));
        for (int i = 1; i <= 48; i++)
        {
            now += step;
            renderer.OnMove(BrushInputSample.CreateStylus(new Point(24 + (i * 4.5), 120), now, 0.74));
        }

        var release = new Point(252, 120);
        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(release, now, 0.68));

        var geometry = renderer.GetLastCoreGeometry();
        geometry.Should().NotBeNull();

        // Rounded caps are bounded by the very narrow release width. An exposed
        // taper must put the actual tip into the final flattened outline.
        var extension = geometry!.Bounds.Right - release.X;
        extension.Should().BeGreaterThan(2.5, "the final silhouette must contain a visible pointed tail");
    }

    [Fact]
    public void MultiRibbonExposedTaper_ShouldHaveOneForwardmostTip()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        config.EnableRdpSimplify = false;
        config.EnableMultiRibbon = true;
        config.MultiRibbonCount = 3;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(24, 120), now, 0.82));
        for (int i = 1; i <= 48; i++)
        {
            now += step;
            renderer.OnMove(BrushInputSample.CreateStylus(new Point(24 + (i * 4.5), 120), now, 0.74));
        }

        var release = new Point(252, 120);
        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(release, now, 0.68));

        var geometry = renderer.GetLastCoreGeometry();
        geometry.Should().NotBeNull();

        var flattened = geometry!.GetFlattenedPathGeometry(0.08, ToleranceType.Absolute);
        var figureMaxima = flattened.Figures
            .Select(figure => EnumerateFigurePoints(figure).Max(point => point.X))
            .ToList();
        figureMaxima.Should().NotBeEmpty();

        double forwardmost = figureMaxima.Max();
        figureMaxima.Count(maximum => maximum >= forwardmost - 0.7)
            .Should().Be(1, "only the core ribbon should own the exposed forward tip");
    }

    [Fact]
    public void PreviewGeometry_ShouldStayCloseToFinalGeometry_WhenStrokeIsReleased()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyClarity();
        config.EnableRdpSimplify = false;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(24, 120), now, 0.76));
        Point last = new Point(24, 120);
        for (int i = 1; i <= 120; i++)
        {
            now += step;
            last = new Point(24 + (i * 3.2), 120 + (Math.Sin(i * 0.12) * 12.0));
            renderer.OnMove(BrushInputSample.CreateStylus(last, now, 0.72));
        }

        var preview = renderer.GetPreviewCoreGeometry();
        preview.Should().NotBeNull();

        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(last, now, 0.72));
        var final = renderer.GetLastCoreGeometry();
        final.Should().NotBeNull();

        var previewBounds = preview!.Bounds;
        var finalBounds = final!.Bounds;
        var maxDelta = new[]
        {
            Math.Abs(previewBounds.Left - finalBounds.Left),
            Math.Abs(previewBounds.Top - finalBounds.Top),
            Math.Abs(previewBounds.Right - finalBounds.Right),
            Math.Abs(previewBounds.Bottom - finalBounds.Bottom)
        }.Max();

        maxDelta.Should().BeLessThan(
            3.5,
            "preview={0} final={1} preview and committed outlines should share endpoint and taper semantics",
            previewBounds,
            finalBounds);
    }

    [Fact]
    public void PreviewGeometry_ShouldReuseCache_WhenInputHasNotChanged()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyClarity();
        config.EnableRdpSimplify = false;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(24, 120), now, 0.76));
        for (int i = 1; i <= 72; i++)
        {
            now += step;
            renderer.OnMove(BrushInputSample.CreateStylus(new Point(24 + (i * 3.2), 120), now, 0.72));
        }

        var first = renderer.GetPreviewCoreGeometry();
        var second = renderer.GetPreviewCoreGeometry();
        first.Should().NotBeNull();
        ReferenceEquals(first, second).Should().BeTrue("an unchanged active input should reuse the frozen preview geometry");

        now += step;
        renderer.OnMove(BrushInputSample.CreateStylus(new Point(24 + (72 * 3.2) + 0.2, 120), now, 0.72));
        var afterRawMove = renderer.GetPreviewCoreGeometry();
        afterRawMove.Should().NotBeNull();
        ReferenceEquals(first, afterRawMove).Should().BeFalse("a new raw endpoint must invalidate the active preview");
    }

    private static IEnumerable<Point> EnumerateFigurePoints(PathFigure figure)
    {
        yield return figure.StartPoint;
        foreach (var segment in figure.Segments)
        {
            switch (segment)
            {
                case LineSegment line:
                    yield return line.Point;
                    break;
                case PolyLineSegment polyLine:
                    foreach (var point in polyLine.Points)
                    {
                        yield return point;
                    }
                    break;
                case BezierSegment bezier:
                    yield return bezier.Point1;
                    yield return bezier.Point2;
                    yield return bezier.Point3;
                    break;
                case PolyBezierSegment polyBezier:
                    foreach (var point in polyBezier.Points)
                    {
                        yield return point;
                    }
                    break;
                case QuadraticBezierSegment quadratic:
                    yield return quadratic.Point1;
                    yield return quadratic.Point2;
                    break;
                case PolyQuadraticBezierSegment polyQuadratic:
                    foreach (var point in polyQuadratic.Points)
                    {
                        yield return point;
                    }
                    break;
                case ArcSegment arc:
                    yield return arc.Point;
                    break;
            }
        }
    }

    [Fact]
    public void OnUp_ShouldKeepMiddleBody_ForShortFlick_WhenTaperLengthExceedsArcLength()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        config.EnableRdpSimplify = false;
        config.TaperLengthPx = 26.0;
        config.TaperStrength = 0.95;
        config.StartTaperStyle = TaperCapStyle.Exposed;
        config.EndTaperStyle = TaperCapStyle.Exposed;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(64, 64), now, 0.86));
        now += step;
        renderer.OnMove(BrushInputSample.CreateStylus(new Point(70, 66), now, 0.84));
        now += step;
        renderer.OnMove(BrushInputSample.CreateStylus(new Point(76, 69), now, 0.82));
        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(82, 72), now, 0.8));

        renderer.GetLastCoreGeometry().Should().NotBeNull();
        var resampled = renderer.GetLastResampledStrokePointsForDiagnostics();
        resampled.Should().NotBeNull();
        resampled!.Count.Should().BeGreaterThanOrEqualTo(5);

        int centerIndex = resampled.Count / 2;
        double centerWidth = resampled[centerIndex].Width;
        double maxWidth = resampled.Max(p => p.Width);
        double startMin = resampled.Take(Math.Max(2, resampled.Count / 4)).Min(p => p.Width);
        double endMin = resampled.Skip(Math.Max(0, resampled.Count - Math.Max(2, resampled.Count / 4))).Min(p => p.Width);

        centerWidth.Should().BeGreaterThan(maxWidth * 0.86);
        centerWidth.Should().BeGreaterThan(startMin * 1.5);
        centerWidth.Should().BeGreaterThan(endMin * 1.5);
    }

    [Fact]
    public void OnUp_ShouldAvoidMidThinning_ForDotLikeStroke_WithLargeTaper()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        config.EnableRdpSimplify = false;
        config.TaperLengthPx = 30.0;
        config.TaperStrength = 0.96;
        config.StartTaperStyle = TaperCapStyle.Exposed;
        config.EndTaperStyle = TaperCapStyle.Hidden;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(180, 180), now, 0.88));
        now += step;
        renderer.OnMove(BrushInputSample.CreateStylus(new Point(183, 182), now, 0.86));
        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(186, 184), now, 0.84));

        renderer.GetLastCoreGeometry().Should().NotBeNull();
        var resampled = renderer.GetLastResampledStrokePointsForDiagnostics();
        resampled.Should().NotBeNull();
        resampled!.Count.Should().BeGreaterThanOrEqualTo(3);

        int centerIndex = resampled.Count / 2;
        double centerWidth = resampled[centerIndex].Width;
        double startWidth = resampled[0].Width;
        double endWidth = resampled[^1].Width;
        double maxWidth = resampled.Max(p => p.Width);

        centerWidth.Should().BeGreaterThanOrEqualTo(maxWidth * 0.8);
        startWidth.Should().BeGreaterThanOrEqualTo(centerWidth * 1.05);
        centerWidth.Should().BeGreaterThan(endWidth * 1.2);
    }

    [Fact]
    public void DotLikeStroke_BodyWidth_ShouldStayAboveThreshold()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        config.EnableRdpSimplify = false;
        config.TaperLengthPx = 30.0;
        config.TaperStrength = 0.96;

        const double baseSize = 12.0;
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(220, 220), now, 0.9));
        now += step;
        renderer.OnMove(BrushInputSample.CreateStylus(new Point(223, 222), now, 0.86));
        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(226, 224), now, 0.84));

        var resampled = renderer.GetLastResampledStrokePointsForDiagnostics();
        resampled.Should().NotBeNull();
        resampled!.Count.Should().BeGreaterThanOrEqualTo(3);

        double bodyWidth = resampled[resampled.Count / 2].Width;
        bodyWidth.Should().BeGreaterThanOrEqualTo(baseSize * 0.28);
    }

    [Fact]
    public void DotLikeHeadMixCap_Decreasing_ShouldNotThinHeadSegment()
    {
        var highCap = RenderDotLikeResampled(headMixCap: 0.62, tailSharpMin: 0.9);
        var lowCap = RenderDotLikeResampled(headMixCap: 0.26, tailSharpMin: 0.9);

        int headWindow = Math.Max(2, Math.Min(highCap.Count, lowCap.Count) / 4);
        double highHeadAvg = highCap.Take(headWindow).Average(p => p.Width);
        double lowHeadAvg = lowCap.Take(headWindow).Average(p => p.Width);

        // Lower head cap should keep at least the same (or thicker) head body.
        lowHeadAvg.Should().BeGreaterThanOrEqualTo(highHeadAvg * 0.98);
    }

    [Fact]
    public void DotLikeTailSharpMin_Decreasing_ShouldSharpenTailTip()
    {
        var rounderTail = RenderDotLikeResampled(headMixCap: 0.42, tailSharpMin: 0.97);
        var sharperTail = RenderDotLikeResampled(headMixCap: 0.42, tailSharpMin: 0.82);

        int tailWindow = Math.Max(2, Math.Min(rounderTail.Count, sharperTail.Count) / 4);
        double rounderTailAvg = rounderTail.Skip(rounderTail.Count - tailWindow).Average(p => p.Width);
        double sharperTailAvg = sharperTail.Skip(sharperTail.Count - tailWindow).Average(p => p.Width);

        // Lower tailSharpMin should yield a sharper tail (smaller tail-tip widths).
        sharperTailAvg.Should().BeLessThanOrEqualTo(rounderTailAvg * 1.01);
    }

    private static IReadOnlyList<StrokePointData> RenderDotLikeResampled(double headMixCap, double tailSharpMin)
    {
        var config = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        config.EnableRdpSimplify = false;
        config.TaperLengthPx = 30.0;
        config.TaperStrength = 0.96;
        config.DotLikeHeadMixCap = headMixCap;
        config.DotLikeTailSharpMin = tailSharpMin;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(240, 240), now, 0.9));
        now += step;
        renderer.OnMove(BrushInputSample.CreateStylus(new Point(243, 242), now, 0.86));
        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(246, 244), now, 0.84));

        var resampled = renderer.GetLastResampledStrokePointsForDiagnostics();
        resampled.Should().NotBeNull();
        resampled!.Count.Should().BeGreaterThanOrEqualTo(3);
        return resampled;
    }

    private static void ReplayDeterministicTrace(VariableWidthBrushRenderer renderer, long startTimestamp, long step)
    {
        long now = startTimestamp;
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(40, 56), now, 0.55));

        for (int i = 1; i <= 180; i++)
        {
            now += step;
            double progress = i / 180.0;
            double x = 40 + (progress * 720.0);
            double y = 56 + (Math.Sin(progress * 12.0) * 22.0) + (Math.Cos(progress * 5.5) * 8.0);
            double pressure = 0.22 + (0.65 * (0.5 + (Math.Sin(progress * 17.0) * 0.5)));
            renderer.OnMove(BrushInputSample.CreateStylus(new Point(x, y), now, pressure));
        }

        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(760, 84), now, 0.5));
    }
}
