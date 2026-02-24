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
    [InlineData(true, false, PaintToolMode.Cursor, true)]
    [InlineData(false, false, PaintToolMode.Cursor, false)]
    [InlineData(true, true, PaintToolMode.Cursor, false)]
    [InlineData(true, false, PaintToolMode.Brush, false)]
    public void StylusCursorPolicy_ShouldMatchExpected(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool expected)
    {
        var actual = StylusCursorPolicy.ShouldPanPhoto(photoModeActive, boardActive, mode);
        actual.Should().Be(expected);
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
}
