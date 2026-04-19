using System;
using System.Collections.Generic;
using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct PhotoPanVelocitySample(
    System.Windows.Point Position,
    long TimestampTicks);

internal static class PhotoPanInertiaMotionPolicy
{
    internal static bool TryResolveReleaseVelocity(
        IReadOnlyList<PhotoPanVelocitySample> samples,
        long releaseTimestampTicks,
        long stopwatchFrequency,
        out Vector velocityDipPerMs)
    {
        return TryResolveReleaseVelocity(
            samples,
            releaseTimestampTicks,
            stopwatchFrequency,
            PhotoPanInertiaTuning.Default,
            out velocityDipPerMs);
    }

    internal static bool TryResolveReleaseVelocity(
        IReadOnlyList<PhotoPanVelocitySample> samples,
        long releaseTimestampTicks,
        long stopwatchFrequency,
        PhotoPanInertiaTuning tuning,
        out Vector velocityDipPerMs)
    {
        return TryResolveReleaseVelocity(
            samples,
            releaseTimestampTicks,
            stopwatchFrequency,
            PhotoPanReleaseTuningPolicy.Resolve(PhotoPanPointerKind.Mouse, tuning),
            out velocityDipPerMs);
    }

    internal static bool TryResolveReleaseVelocity(
        IReadOnlyList<PhotoPanVelocitySample> samples,
        long releaseTimestampTicks,
        long stopwatchFrequency,
        PhotoPanReleaseTuning tuning,
        out Vector velocityDipPerMs)
    {
        velocityDipPerMs = default;
        if (samples == null
            || samples.Count < 2
            || releaseTimestampTicks <= 0
            || stopwatchFrequency <= 0)
        {
            return false;
        }

        var lastSample = samples[^1];
        if (lastSample.TimestampTicks <= 0 || releaseTimestampTicks < lastSample.TimestampTicks)
        {
            return false;
        }

        var sampleAgeMs = (releaseTimestampTicks - lastSample.TimestampTicks) * 1000.0 / stopwatchFrequency;
        if (sampleAgeMs > tuning.MaxVelocitySampleAgeMs)
        {
            return false;
        }

        var velocitySampleWindowMs = Math.Max(
            PhotoPanInertiaDefaults.MouseMinVelocitySampleIntervalMs,
            tuning.VelocitySampleWindowMs);
        var velocityWindowTicks = (long)Math.Ceiling(
            velocitySampleWindowMs * stopwatchFrequency / 1000.0);
        var minAllowedTimestampTicks = Math.Max(0, lastSample.TimestampTicks - velocityWindowTicks);

        var weightedVelocity = new Vector();
        var totalWeight = 0.0;
        var validSegmentCount = 0;
        for (var i = 1; i < samples.Count; i++)
        {
            var previous = samples[i - 1];
            var current = samples[i];
            if (previous.TimestampTicks <= 0 || current.TimestampTicks <= previous.TimestampTicks)
            {
                continue;
            }
            if (current.TimestampTicks < minAllowedTimestampTicks)
            {
                continue;
            }

            var elapsedMs = (current.TimestampTicks - previous.TimestampTicks) * 1000.0 / stopwatchFrequency;
            var effectiveElapsedMs = Math.Max(elapsedMs, PhotoPanInertiaDefaults.MouseMinVelocitySampleIntervalMs);
            var delta = current.Position - previous.Position;
            if (delta.Length < tuning.MinVelocitySampleDistanceDip)
            {
                continue;
            }

            var ageMs = (lastSample.TimestampTicks - current.TimestampTicks) * 1000.0 / stopwatchFrequency;
            var clampedAgeMs = Math.Clamp(ageMs, 0, velocitySampleWindowMs);
            var recencyFactor = 1.0 + (
                (velocitySampleWindowMs - clampedAgeMs)
                / velocitySampleWindowMs
                * Math.Max(0, tuning.VelocityRecentWeightGain));

            var segmentVelocity = new Vector(delta.X / effectiveElapsedMs, delta.Y / effectiveElapsedMs);
            weightedVelocity += segmentVelocity * recencyFactor;
            totalWeight += recencyFactor;
            validSegmentCount++;
        }

        if (validSegmentCount <= 0 || totalWeight <= 0)
        {
            return false;
        }

        var rawVelocity = weightedVelocity / totalWeight;
        var speed = rawVelocity.Length;
        if (speed < tuning.MinReleaseSpeedDipPerMs)
        {
            return false;
        }

        if (speed > tuning.MaxReleaseSpeedDipPerMs)
        {
            rawVelocity *= tuning.MaxReleaseSpeedDipPerMs / speed;
        }

        velocityDipPerMs = rawVelocity;
        return true;
    }

    internal static bool TryResolveReleaseVelocity(
        System.Windows.Point previousPosition,
        long previousTimestampTicks,
        System.Windows.Point lastPosition,
        long lastTimestampTicks,
        long releaseTimestampTicks,
        long stopwatchFrequency,
        out Vector velocityDipPerMs)
    {
        return TryResolveReleaseVelocity(
            previousPosition,
            previousTimestampTicks,
            lastPosition,
            lastTimestampTicks,
            releaseTimestampTicks,
            stopwatchFrequency,
            PhotoPanInertiaTuning.Default,
            out velocityDipPerMs);
    }

    internal static bool TryResolveReleaseVelocity(
        System.Windows.Point previousPosition,
        long previousTimestampTicks,
        System.Windows.Point lastPosition,
        long lastTimestampTicks,
        long releaseTimestampTicks,
        long stopwatchFrequency,
        PhotoPanInertiaTuning tuning,
        out Vector velocityDipPerMs)
    {
        return TryResolveReleaseVelocity(
            previousPosition,
            previousTimestampTicks,
            lastPosition,
            lastTimestampTicks,
            releaseTimestampTicks,
            stopwatchFrequency,
            PhotoPanReleaseTuningPolicy.Resolve(PhotoPanPointerKind.Mouse, tuning),
            out velocityDipPerMs);
    }

    internal static bool TryResolveReleaseVelocity(
        System.Windows.Point previousPosition,
        long previousTimestampTicks,
        System.Windows.Point lastPosition,
        long lastTimestampTicks,
        long releaseTimestampTicks,
        long stopwatchFrequency,
        PhotoPanReleaseTuning tuning,
        out Vector velocityDipPerMs)
    {
        return TryResolveReleaseVelocity(
            new[]
            {
                new PhotoPanVelocitySample(previousPosition, previousTimestampTicks),
                new PhotoPanVelocitySample(lastPosition, lastTimestampTicks)
            },
            releaseTimestampTicks,
            stopwatchFrequency,
            tuning,
            out velocityDipPerMs);
    }

    internal static Vector ResolveTranslation(Vector velocityDipPerMs, double elapsedMs)
    {
        return ResolveTranslation(velocityDipPerMs, elapsedMs, PhotoPanInertiaTuning.Default);
    }

    internal static Vector ResolveTranslation(Vector velocityDipPerMs, double elapsedMs, PhotoPanInertiaTuning tuning)
    {
        return ResolveTranslation(
            velocityDipPerMs,
            elapsedMs,
            PhotoPanReleaseTuningPolicy.Resolve(PhotoPanPointerKind.Mouse, tuning));
    }

    internal static Vector ResolveTranslation(
        Vector velocityDipPerMs,
        double elapsedMs,
        PhotoPanReleaseTuning tuning)
    {
        if (elapsedMs <= 0 || velocityDipPerMs.LengthSquared <= 0)
        {
            return default;
        }

        var translation = new Vector(
            velocityDipPerMs.X * elapsedMs,
            velocityDipPerMs.Y * elapsedMs);
        var distance = translation.Length;
        if (distance <= 0)
        {
            return default;
        }
        if (distance > tuning.MaxTranslationPerFrameDip)
        {
            var scale = tuning.MaxTranslationPerFrameDip / distance;
            translation *= scale;
        }

        return translation;
    }

    internal static bool TryResolveInertiaStep(
        Vector velocityDipPerMs,
        double elapsedMs,
        PhotoPanReleaseTuning tuning,
        out Vector translation,
        out Vector nextVelocityDipPerMs)
    {
        translation = default;
        nextVelocityDipPerMs = velocityDipPerMs;
        if (elapsedMs <= 0 || velocityDipPerMs.LengthSquared <= 0)
        {
            return false;
        }

        nextVelocityDipPerMs = ResolveVelocityAfterDeceleration(
            velocityDipPerMs,
            elapsedMs,
            tuning);
        var blendedVelocity = nextVelocityDipPerMs.LengthSquared > 0
            ? (velocityDipPerMs + nextVelocityDipPerMs) * 0.5
            : velocityDipPerMs * 0.5;
        translation = ResolveTranslation(
            blendedVelocity,
            elapsedMs,
            tuning);
        return translation.LengthSquared > 0;
    }

    internal static double ResolveFrameElapsedMilliseconds(double elapsedMs)
    {
        if (double.IsNaN(elapsedMs) || double.IsInfinity(elapsedMs) || elapsedMs <= 0)
        {
            return 0;
        }

        return Math.Clamp(
            elapsedMs,
            PhotoPanInertiaDefaults.MouseFrameElapsedMinMs,
            PhotoPanInertiaDefaults.MouseFrameElapsedMaxMs);
    }

    internal static bool ShouldStopByDuration(double durationMs)
    {
        return ShouldStopByDuration(durationMs, PhotoPanInertiaTuning.Default);
    }

    internal static bool ShouldStopByDuration(double durationMs, PhotoPanInertiaTuning tuning)
    {
        return ShouldStopByDuration(
            durationMs,
            PhotoPanReleaseTuningPolicy.Resolve(PhotoPanPointerKind.Mouse, tuning));
    }

    internal static bool ShouldStopByDuration(double durationMs, PhotoPanReleaseTuning tuning)
    {
        if (double.IsNaN(durationMs) || double.IsInfinity(durationMs))
        {
            return true;
        }

        return durationMs >= tuning.MaxDurationMs;
    }

    internal static Vector ResolveVelocityAfterDeceleration(Vector velocityDipPerMs, double elapsedMs)
    {
        return ResolveVelocityAfterDeceleration(velocityDipPerMs, elapsedMs, PhotoPanInertiaTuning.Default);
    }

    internal static Vector ResolveVelocityAfterDeceleration(
        Vector velocityDipPerMs,
        double elapsedMs,
        PhotoPanInertiaTuning tuning)
    {
        return ResolveVelocityAfterDeceleration(
            velocityDipPerMs,
            elapsedMs,
            PhotoPanReleaseTuningPolicy.Resolve(PhotoPanPointerKind.Mouse, tuning));
    }

    internal static Vector ResolveVelocityAfterDeceleration(
        Vector velocityDipPerMs,
        double elapsedMs,
        PhotoPanReleaseTuning tuning)
    {
        if (elapsedMs <= 0 || velocityDipPerMs.LengthSquared <= 0)
        {
            return velocityDipPerMs;
        }

        var speed = velocityDipPerMs.Length;
        var nextSpeed = speed - (tuning.DecelerationDipPerMs2 * elapsedMs);
        if (nextSpeed <= tuning.StopSpeedDipPerMs)
        {
            return default;
        }

        return velocityDipPerMs * (nextSpeed / speed);
    }
}
