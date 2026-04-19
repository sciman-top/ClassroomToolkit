using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInputAlignmentPolicyTests
{
    [Fact]
    public void NormalizeZoomFactor_Wheel_ShouldConvertDeltaToScaleFactor()
    {
        var ok = PhotoZoomNormalizer.TryNormalizeFactor(
            PhotoZoomInputSource.Wheel,
            rawValue: 120,
            wheelBase: 1.0008,
            gestureSensitivity: 1.0,
            gestureNoiseThreshold: 0.01,
            minEventFactor: 0.85,
            maxEventFactor: 1.18,
            out var factor);

        ok.Should().BeTrue();
        factor.Should().BeApproximately(1.1005, 0.001);
    }

    [Fact]
    public void NormalizeZoomFactor_Gesture_ShouldIgnoreTinyNoise()
    {
        var ok = PhotoZoomNormalizer.TryNormalizeFactor(
            PhotoZoomInputSource.Gesture,
            rawValue: 1.005,
            wheelBase: 1.0008,
            gestureSensitivity: 1.0,
            gestureNoiseThreshold: 0.01,
            minEventFactor: 0.85,
            maxEventFactor: 1.18,
            out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void NormalizeZoomFactor_Gesture_ShouldClampLargeScaleJump()
    {
        var ok = PhotoZoomNormalizer.TryNormalizeFactor(
            PhotoZoomInputSource.Gesture,
            rawValue: 2.0,
            wheelBase: 1.0008,
            gestureSensitivity: 1.0,
            gestureNoiseThreshold: 0.01,
            minEventFactor: 0.85,
            maxEventFactor: 1.18,
            out var factor);

        ok.Should().BeTrue();
        factor.Should().Be(1.18);
    }

    [Fact]
    public void NormalizeZoomFactor_Gesture_ShouldApplySensitivity()
    {
        var ok = PhotoZoomNormalizer.TryNormalizeFactor(
            PhotoZoomInputSource.Gesture,
            rawValue: 1.05,
            wheelBase: 1.0008,
            gestureSensitivity: 0.5,
            gestureNoiseThreshold: 0.01,
            minEventFactor: 0.85,
            maxEventFactor: 1.18,
            out var factor);

        ok.Should().BeTrue();
        factor.Should().BeApproximately(1.025, 0.0001);
    }

    [Theory]
    [InlineData(true, false, PaintToolMode.Cursor, false, true)]
    [InlineData(true, false, PaintToolMode.Cursor, true, false)]
    [InlineData(false, false, PaintToolMode.Cursor, false, false)]
    [InlineData(true, true, PaintToolMode.Cursor, false, false)]
    [InlineData(true, false, PaintToolMode.Brush, false, false)]
    public void StylusCursorPolicy_ShouldMatchExpected(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool expected)
    {
        var actual = StylusCursorPolicy.ShouldPanPhoto(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive);
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void PhotoPanBeginGuardPolicy_ShouldMatchExpected(
        bool shouldPanPhoto,
        bool photoPanning,
        bool expected)
    {
        var actual = PhotoPanBeginGuardPolicy.ShouldBegin(shouldPanPhoto, photoPanning);
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, 0, 0)]
    [InlineData(true, false, 0, 1)]
    [InlineData(true, true, 1, 2)]
    [InlineData(true, false, 1, 0)]
    [InlineData(true, true, 2, 3)]
    [InlineData(true, false, 2, 0)]
    public void StylusPhotoPanRoutingPolicy_ShouldMatchExpected(
        bool shouldPanPhoto,
        bool photoPanning,
        int phase,
        int expected)
    {
        var decision = StylusPhotoPanRoutingPolicy.Resolve(
            shouldPanPhoto,
            photoPanning,
            (StylusPhotoPanPhase)phase);

        ((int)decision).Should().Be(expected);
    }

    [Fact]
    public void PhotoPanLimiter_ShouldClamp_WhenResistanceDisabled()
    {
        var value = PhotoPanLimiter.ApplyAxis(120, min: -50, max: 80, allowResistance: false);
        value.Should().Be(80);
    }

    [Fact]
    public void PhotoPanLimiter_ShouldApplyResistance_WhenOutOfBounds()
    {
        var value = PhotoPanLimiter.ApplyAxis(120, min: -50, max: 80, allowResistance: true, resistanceFactor: 0.25);
        value.Should().Be(90);
    }

    [Fact]
    public void InputConflictGuard_ShouldSuppressWheel_WhenGestureIsRecent()
    {
        var now = DateTime.UtcNow;
        var suppress = PhotoInputConflictGuard.ShouldSuppressWheelAfterGesture(
            lastGestureUtc: now.AddMilliseconds(-80),
            suppressWindowMs: 180,
            nowUtc: now);

        suppress.Should().BeTrue();
    }

    [Fact]
    public void InputConflictGuard_ShouldNotSuppressWheel_WhenGestureExpired()
    {
        var now = DateTime.UtcNow;
        var suppress = PhotoInputConflictGuard.ShouldSuppressWheelAfterGesture(
            lastGestureUtc: now.AddMilliseconds(-260),
            suppressWindowMs: 180,
            nowUtc: now);

        suppress.Should().BeFalse();
    }

    [Fact]
    public void InputConflictGuard_ShouldNotSuppressWheel_WhenGestureNeverInitialized()
    {
        var now = DateTime.UtcNow;
        var suppress = PhotoInputConflictGuard.ShouldSuppressWheelAfterGesture(
            lastGestureUtc: PhotoInputConflictDefaults.UnsetTimestampUtc,
            suppressWindowMs: 180,
            nowUtc: now);

        suppress.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, PaintToolMode.Cursor, false, false, 1, 1)]
    [InlineData(true, false, PaintToolMode.Cursor, false, false, 2, 2)]
    [InlineData(false, false, PaintToolMode.Cursor, false, false, 1, 0)]
    [InlineData(true, true, PaintToolMode.Cursor, false, false, 2, 1)]
    [InlineData(true, false, PaintToolMode.Brush, false, false, 2, 1)]
    [InlineData(true, false, PaintToolMode.Cursor, true, false, 2, 1)]
    [InlineData(true, false, PaintToolMode.Cursor, false, true, 2, 1)]
    public void PhotoManipulationRoutingPolicy_ShouldHonorActiveTouchCount(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool photoPanning,
        int activeTouchCount,
        int expected)
    {
        var decision = PhotoManipulationRoutingPolicy.Resolve(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive,
            photoPanning,
            activeTouchCount);

        ((int)decision).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, false, false, false, 0, false)]
    [InlineData(1, false, false, true, 1, false)]
    [InlineData(1, false, true, true, 1, true)]
    [InlineData(1, true, false, false, 1, true)]
    [InlineData(1, true, true, true, 2, true)]
    [InlineData(2, true, false, false, 3, true)]
    [InlineData(3, true, false, false, 4, true)]
    public void StylusPhotoPanExecutionPolicy_ShouldMatchExpected(
        int routingDecision,
        bool sourceShouldContinue,
        bool sourceShouldMarkHandled,
        bool shouldBeginPan,
        int expectedAction,
        bool expectedHandled)
    {
        var plan = StylusPhotoPanExecutionPolicy.Resolve(
            (StylusPhotoPanRoutingDecision)routingDecision,
            sourceShouldContinue,
            sourceShouldMarkHandled,
            shouldBeginPan);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldMarkHandled.Should().Be(expectedHandled);
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, false, true)]
    [InlineData(2, true, true)]
    public void PhotoManipulationEventHandlingPolicy_ShouldMatchExpected(
        int decision,
        bool expectedShouldHandle,
        bool expectedShouldMarkHandled)
    {
        var plan = PhotoManipulationEventHandlingPolicy.Resolve((PhotoManipulationRoutingDecision)decision);

        plan.ShouldHandle.Should().Be(expectedShouldHandle);
        plan.ShouldMarkHandled.Should().Be(expectedShouldMarkHandled);
    }
}
