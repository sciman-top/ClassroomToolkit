using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanInertiaMotionPolicyTests
{
    [Fact]
    public void TryResolveReleaseVelocity_WithVelocitySampleWindow_ShouldIgnoreOldBurstSegments()
    {
        var samples = new[]
        {
            new PhotoPanVelocitySample(new Point(0, 0), TimestampTicks: 100),
            new PhotoPanVelocitySample(new Point(120, 0), TimestampTicks: 120),
            new PhotoPanVelocitySample(new Point(121, 0), TimestampTicks: 300)
        };

        var resolved = PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
            samples,
            releaseTimestampTicks: 300,
            stopwatchFrequency: 1000,
            velocityDipPerMs: out _);

        resolved.Should().BeFalse();
    }

    [Fact]
    public void TryResolveReleaseVelocity_WithVelocitySampleWindow_ShouldBiasRecentSegments()
    {
        var samples = new[]
        {
            new PhotoPanVelocitySample(new Point(0, 0), TimestampTicks: 1000),
            new PhotoPanVelocitySample(new Point(24, 0), TimestampTicks: 1040),
            new PhotoPanVelocitySample(new Point(48, 0), TimestampTicks: 1080),
            new PhotoPanVelocitySample(new Point(48, 36), TimestampTicks: 1100)
        };

        var resolved = PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
            samples,
            releaseTimestampTicks: 1100,
            stopwatchFrequency: 1000,
            velocityDipPerMs: out var velocityDipPerMs);

        resolved.Should().BeTrue();
        velocityDipPerMs.Y.Should().BeGreaterThan(velocityDipPerMs.X);
    }

    [Fact]
    public void TryResolveReleaseVelocity_ShouldReturnFalse_WhenSampleIsStale()
    {
        var resolved = PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
            previousPosition: new Point(10, 10),
            previousTimestampTicks: 100,
            lastPosition: new Point(80, 10),
            lastTimestampTicks: 120,
            releaseTimestampTicks: 301,
            stopwatchFrequency: 1000,
            velocityDipPerMs: out _);

        resolved.Should().BeFalse();
    }

    [Fact]
    public void TryResolveReleaseVelocity_ShouldClampSpeed_WhenTooFast()
    {
        var resolved = PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
            previousPosition: new Point(0, 0),
            previousTimestampTicks: 100,
            lastPosition: new Point(260, 0),
            lastTimestampTicks: 120,
            releaseTimestampTicks: 122,
            stopwatchFrequency: 1000,
            velocityDipPerMs: out var velocityDipPerMs);

        resolved.Should().BeTrue();
        velocityDipPerMs.Length.Should().BeApproximately(PhotoPanInertiaDefaults.MouseMaxReleaseSpeedDipPerMs, 0.001);
    }

    [Fact]
    public void TryResolveReleaseVelocity_ShouldRespectTouchThreshold()
    {
        var tuning = PhotoPanReleaseTuningPolicy.Resolve(
            PhotoPanPointerKind.Touch,
            PhotoPanInertiaTuning.Default);

        var resolved = PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
            new[]
            {
                new PhotoPanVelocitySample(new Point(0, 0), 1000),
                new PhotoPanVelocitySample(new Point(1.5, 0), 1030),
                new PhotoPanVelocitySample(new Point(4.5, 0), 1060)
            },
            releaseTimestampTicks: 1060,
            stopwatchFrequency: 1000,
            tuning,
            out var velocityDipPerMs);

        resolved.Should().BeTrue();
        velocityDipPerMs.X.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryResolveReleaseVelocity_ShouldAllowTouchRelease_WhenLastSampleIsSlightlyOlder()
    {
        var mouseTuning = PhotoPanReleaseTuningPolicy.Resolve(
            PhotoPanPointerKind.Mouse,
            PhotoPanInertiaTuning.Default);
        var touchTuning = PhotoPanReleaseTuningPolicy.Resolve(
            PhotoPanPointerKind.Touch,
            PhotoPanInertiaTuning.Default);
        var samples = new[]
        {
            new PhotoPanVelocitySample(new Point(0, 0), 1000),
            new PhotoPanVelocitySample(new Point(24, 0), 1040),
            new PhotoPanVelocitySample(new Point(48, 0), 1080)
        };

        var mouseResolved = PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
            samples,
            releaseTimestampTicks: 1260,
            stopwatchFrequency: 1000,
            mouseTuning,
            out _);
        var touchResolved = PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
            samples,
            releaseTimestampTicks: 1260,
            stopwatchFrequency: 1000,
            touchTuning,
            out var touchVelocityDipPerMs);

        mouseResolved.Should().BeFalse();
        touchResolved.Should().BeTrue();
        touchVelocityDipPerMs.X.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryResolveReleaseVelocity_ShouldFallbackToMinInterval_WhenSampleIntervalTooSmall()
    {
        var resolved = PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
            previousPosition: new Point(0, 0),
            previousTimestampTicks: 1000,
            lastPosition: new Point(1.2, 0),
            lastTimestampTicks: 1002,
            releaseTimestampTicks: 1002,
            stopwatchFrequency: 1000,
            velocityDipPerMs: out var velocityDipPerMs);

        resolved.Should().BeTrue();
        velocityDipPerMs.X.Should().BeApproximately(
            1.2 / PhotoPanInertiaDefaults.MouseMinVelocitySampleIntervalMs,
            0.001);
    }

    [Fact]
    public void ResolveTranslation_ShouldScaleWithElapsedMilliseconds()
    {
        var translation = PhotoPanInertiaMotionPolicy.ResolveTranslation(
            new Vector(1.2, -0.5),
            elapsedMs: 25);

        translation.X.Should().BeApproximately(30, 0.001);
        translation.Y.Should().BeApproximately(-12.5, 0.001);
    }

    [Fact]
    public void ResolveTranslation_ShouldClampDistance_WhenFrameJumpOccurs()
    {
        var translation = PhotoPanInertiaMotionPolicy.ResolveTranslation(
            new Vector(4.4, 0),
            elapsedMs: 120);

        translation.Length.Should().BeApproximately(
            PhotoPanInertiaDefaults.MouseMaxTranslationPerFrameDip,
            0.001);
    }

    [Fact]
    public void TryResolveInertiaStep_ShouldBlendCurrentAndNextVelocity_ForSmootherTravel()
    {
        var tuning = PhotoPanReleaseTuningPolicy.Resolve(
            PhotoPanPointerKind.Mouse,
            PhotoPanInertiaTuning.Default);
        var velocityDipPerMs = new Vector(1.2, 0);
        var elapsedMs = 16.0;

        var resolved = PhotoPanInertiaMotionPolicy.TryResolveInertiaStep(
            velocityDipPerMs,
            elapsedMs,
            tuning,
            out var translation,
            out var nextVelocityDipPerMs);

        var expectedNextVelocity = PhotoPanInertiaMotionPolicy.ResolveVelocityAfterDeceleration(
            velocityDipPerMs,
            elapsedMs,
            tuning);
        var expectedTranslation = PhotoPanInertiaMotionPolicy.ResolveTranslation(
            (velocityDipPerMs + expectedNextVelocity) * 0.5,
            elapsedMs,
            tuning);

        resolved.Should().BeTrue();
        nextVelocityDipPerMs.X.Should().BeApproximately(expectedNextVelocity.X, 0.001);
        translation.X.Should().BeApproximately(expectedTranslation.X, 0.001);
    }

    [Fact]
    public void ResolveFrameElapsedMilliseconds_ShouldClampToConfiguredRange()
    {
        var low = PhotoPanInertiaMotionPolicy.ResolveFrameElapsedMilliseconds(0.2);
        var high = PhotoPanInertiaMotionPolicy.ResolveFrameElapsedMilliseconds(180);

        low.Should().Be(PhotoPanInertiaDefaults.MouseFrameElapsedMinMs);
        high.Should().Be(PhotoPanInertiaDefaults.MouseFrameElapsedMaxMs);
    }

    [Fact]
    public void ShouldStopByDuration_ShouldReturnTrue_WhenDurationExceeded()
    {
        var shouldStop = PhotoPanInertiaMotionPolicy.ShouldStopByDuration(
            PhotoPanInertiaDefaults.MouseMaxDurationMs + 1);

        shouldStop.Should().BeTrue();
    }

    [Fact]
    public void ResolveVelocityAfterDeceleration_ShouldReturnZero_WhenBelowStopThreshold()
    {
        var next = PhotoPanInertiaMotionPolicy.ResolveVelocityAfterDeceleration(
            new Vector(0.03, 0),
            elapsedMs: 20);

        next.LengthSquared.Should().Be(0);
    }

    [Fact]
    public void ResolveTranslation_ShouldHonorTuningMaxDistance()
    {
        var tuning = new PhotoPanInertiaTuning(
            MouseDecelerationDipPerMs2: 0.0022,
            MouseStopSpeedDipPerMs: 0.012,
            MouseMinReleaseSpeedDipPerMs: 0.06,
            MouseMaxReleaseSpeedDipPerMs: 4.4,
            MouseMaxDurationMs: 1100,
            MouseMaxTranslationPerFrameDip: 60,
            GestureTranslationDecelerationDipPerMs2: 0.0034,
            GestureCrossPageTranslationDecelerationDipPerMs2: 0.0029);
        var translation = PhotoPanInertiaMotionPolicy.ResolveTranslation(
            new Vector(4.4, 0),
            elapsedMs: 50,
            tuning);

        translation.Length.Should().BeApproximately(60, 0.001);
    }

    [Fact]
    public void ResolveTranslation_ShouldHonorTouchReleaseTuningFrameClamp()
    {
        var tuning = PhotoPanReleaseTuningPolicy.Resolve(
            PhotoPanPointerKind.Touch,
            PhotoPanInertiaTuning.Default);

        var translation = PhotoPanInertiaMotionPolicy.ResolveTranslation(
            new Vector(6.0, 0),
            elapsedMs: 80,
            tuning);

        translation.Length.Should().BeLessThanOrEqualTo(tuning.MaxTranslationPerFrameDip);
    }

    [Fact]
    public void ShouldStopByDuration_ShouldHonorTuningDuration()
    {
        var tuning = new PhotoPanInertiaTuning(
            MouseDecelerationDipPerMs2: 0.0022,
            MouseStopSpeedDipPerMs: 0.012,
            MouseMinReleaseSpeedDipPerMs: 0.06,
            MouseMaxReleaseSpeedDipPerMs: 4.4,
            MouseMaxDurationMs: 500,
            MouseMaxTranslationPerFrameDip: 150,
            GestureTranslationDecelerationDipPerMs2: 0.0034,
            GestureCrossPageTranslationDecelerationDipPerMs2: 0.0029);

        PhotoPanInertiaMotionPolicy.ShouldStopByDuration(501, tuning).Should().BeTrue();
    }
}
