using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using Xunit;

namespace ClassroomToolkit.Tests.Paint;

public sealed class StylusBatchDispatchPolicyTests
{
    [Fact]
    public void ResolveStepTicks_ShouldGuardAgainstZeroSampleCount()
    {
        StylusExecutionPolicies.ResolveStepTicks(spanTicks: 100, sampleCount: 0).Should().Be(100);
    }

    [Fact]
    public void ResolveBatchStartTicks_ShouldBackdateBySegmentCount()
    {
        var start = StylusExecutionPolicies.ResolveBatchStartTicks(
            nowTicks: 1000,
            stepTicks: 10,
            sampleCount: 4);

        start.Should().Be(970);
    }
}

public sealed class StylusBatchTimingPolicyTests
{
    [Fact]
    public void ResolveSpanTicks_ShouldUseFallback_WhenSampleCountIsZero()
    {
        var span = StylusExecutionPolicies.ResolveSpanTicks(
            stopwatchFrequency: 1000,
            nowTicks: 0,
            sampleCount: 0,
            hasPreviousTimestamp: false,
            lastTimestampTicks: 0);

        span.Should().Be(4); // 1000 / 240 -> floor 4
    }

    [Fact]
    public void ResolveSpanTicks_ShouldClampObservedSpan_WhenPreviousTimestampExists()
    {
        var span = StylusExecutionPolicies.ResolveSpanTicks(
            stopwatchFrequency: 1000,
            nowTicks: 3000,
            sampleCount: 10,
            hasPreviousTimestamp: true,
            lastTimestampTicks: 1000);

        span.Should().Be(220); // maxPerSample=22, maxSpan=220
    }
}

public sealed class StylusDownExecutionPolicyTests
{
    [Theory]
    [InlineData(true, false, false, true, (int)StylusDownExecutionAction.None, false, false)]
    [InlineData(false, true, false, true, (int)StylusDownExecutionAction.None, false, false)]
    [InlineData(false, false, true, true, (int)StylusDownExecutionAction.None, false, false)]
    [InlineData(false, false, false, true, (int)StylusDownExecutionAction.HandleFirstStylusPoint, true, true)]
    [InlineData(false, false, false, false, (int)StylusDownExecutionAction.HandlePointerPosition, true, true)]
    public void Resolve_ShouldReturnExpectedPlan(
        bool photoLoading,
        bool handledByPhotoPan,
        bool shouldIgnoreFromPhotoControls,
        bool hasStylusPoints,
        int expectedAction,
        bool expectedReset,
        bool expectedHandled)
    {
        var plan = StylusExecutionPolicies.ResolveStylusDownExecution(
            photoLoading,
            handledByPhotoPan,
            shouldIgnoreFromPhotoControls,
            hasStylusPoints);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldResetTimestampState.Should().Be(expectedReset);
        plan.ShouldMarkHandled.Should().Be(expectedHandled);
    }
}

public sealed class StylusInterpolationPolicyTests
{
    [Fact]
    public void ResolveInterpolationStepDip_ShouldClampToConfiguredRange()
    {
        var step = StylusExecutionPolicies.ResolveInterpolationStepDip(
            brushSize: 100,
            distance: 800,
            totalTicks: 10,
            stopwatchFrequency: 1000);

        step.Should().Be(StylusInterpolationDefaults.InterpolationStepMaxDip);
    }

    [Fact]
    public void ShouldInterpolate_ShouldUseConfiguredDistanceMultiplier()
    {
        StylusExecutionPolicies.ShouldInterpolate(distance: 10, interpolationStepDip: 10).Should().BeFalse();
        StylusExecutionPolicies.ShouldInterpolate(distance: 15, interpolationStepDip: 10).Should().BeTrue();
    }

    [Fact]
    public void ResolveMaxSegments_ShouldRespectSpeedBandAndSlowFrameBonus()
    {
        StylusExecutionPolicies.ResolveMaxSegments(speedDipPerMs: 3.5, dtMs: 2)
            .Should().Be(StylusInterpolationDefaults.FastSpeedMaxSegments);

        StylusExecutionPolicies.ResolveMaxSegments(speedDipPerMs: 1.0, dtMs: 12)
            .Should().Be(8);
    }

    [Fact]
    public void LerpNullableAngle_ShouldCrossZeroAtTheShortestArc()
    {
        double from = 359.0 * Math.PI / 180.0;
        double to = 1.0 * Math.PI / 180.0;

        var midpoint = StylusExecutionPolicies.LerpNullableAngle(from, to, 0.5);

        midpoint.Should().NotBeNull();
        midpoint!.Value.Should().BeApproximately(0.0, 0.000001);
        StylusExecutionPolicies.LerpNullableAngle(null, to, 0.5)
            .Should().BeApproximately(to, 0.000001);
    }
}

public sealed class StylusMoveExecutionPolicyTests
{
    [Theory]
    [InlineData(true, false, true, true, (int)PaintToolMode.Brush, true, false, (int)StylusMoveExecutionAction.None, false)]
    [InlineData(false, true, true, true, (int)PaintToolMode.Brush, true, false, (int)StylusMoveExecutionAction.None, false)]
    [InlineData(false, false, false, true, (int)PaintToolMode.Brush, true, false, (int)StylusMoveExecutionAction.None, false)]
    [InlineData(false, false, true, false, (int)PaintToolMode.Brush, true, false, (int)StylusMoveExecutionAction.HandlePointerPosition, true)]
    [InlineData(false, false, true, true, (int)PaintToolMode.Brush, true, false, (int)StylusMoveExecutionAction.HandleBrushBatch, true)]
    [InlineData(false, false, true, true, (int)PaintToolMode.Eraser, false, false, (int)StylusMoveExecutionAction.HandleStylusPointsIndividually, true)]
    [InlineData(false, false, true, true, (int)PaintToolMode.Brush, true, true, (int)StylusMoveExecutionAction.HandleStylusPointsIndividually, true)]
    public void Resolve_ShouldReturnExpectedPlan(
        bool photoLoading,
        bool handledByPhotoPan,
        bool inkOperationActive,
        bool hasStylusPoints,
        int mode,
        bool strokeInProgress,
        bool crossPageDisplayActive,
        int expectedAction,
        bool expectedHandled)
    {
        var plan = StylusExecutionPolicies.ResolveStylusMoveExecution(
            photoLoading,
            handledByPhotoPan,
            inkOperationActive,
            hasStylusPoints,
            (PaintToolMode)mode,
            strokeInProgress,
            crossPageDisplayActive);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldMarkHandled.Should().Be(expectedHandled);
    }
}

public sealed class StylusSampleTimestampPolicyTests
{
    [Fact]
    public void ResolveBatchSpanTicks_ShouldUseFallback_WhenStateHasNoTimestamp()
    {
        var span = StylusExecutionPolicies.ResolveBatchSpanTicks(
            stopwatchFrequency: 1000,
            nowTicks: 0,
            sampleCount: 0,
            state: StylusSampleTimestampState.Default);

        span.Should().Be(4);
    }

    [Fact]
    public void ResolveBatchSpanTicks_ShouldUseObservedSpan_WhenStateHasTimestamp()
    {
        var span = StylusExecutionPolicies.ResolveBatchSpanTicks(
            stopwatchFrequency: 1000,
            nowTicks: 3000,
            sampleCount: 10,
            state: new StylusSampleTimestampState(
                HasTimestamp: true,
                LastTimestampTicks: 1000));

        span.Should().Be(220);
    }

    [Fact]
    public void EnsureMonotonicTimestamp_ShouldAdvance_WhenTimestampNotGreaterThanPrevious()
    {
        var timestamp = StylusExecutionPolicies.EnsureMonotonicTimestamp(
            timestampTicks: 500,
            state: new StylusSampleTimestampState(
                HasTimestamp: true,
                LastTimestampTicks: 500));

        timestamp.Should().Be(501);
    }
}

public sealed class StylusUpExecutionPolicyTests
{
    [Theory]
    [InlineData(true, false, true, true, (int)StylusUpExecutionAction.None, false)]
    [InlineData(false, true, true, true, (int)StylusUpExecutionAction.None, false)]
    [InlineData(false, false, false, true, (int)StylusUpExecutionAction.None, false)]
    [InlineData(false, false, true, true, (int)StylusUpExecutionAction.HandleLastStylusPoint, true)]
    [InlineData(false, false, true, false, (int)StylusUpExecutionAction.HandlePointerPosition, true)]
    public void Resolve_ShouldReturnExpectedPlan(
        bool photoLoading,
        bool handledByPhotoPan,
        bool inkOperationActive,
        bool hasStylusPoints,
        int expectedAction,
        bool expectedHandled)
    {
        var plan = StylusExecutionPolicies.ResolveStylusUpExecution(
            photoLoading,
            handledByPhotoPan,
            inkOperationActive,
            hasStylusPoints);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldMarkHandled.Should().Be(expectedHandled);
    }
}
