using System.Collections.Generic;
using System.Windows.Input;
using System.Windows;
using System;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct PhotoEnterTransformInitPlan(
    bool ShouldApplyUnifiedTransform,
    bool ShouldMarkUserDirtyAfterUnifiedApply,
    bool ShouldMarkUnifiedTransformReady,
    bool ShouldTryStoredTransform,
    bool ShouldResetIdentity);

internal static class PhotoHorizontalPanRangeDefaults
{
    internal const double MinSlackDip = 24.0;
    internal const double SlackRatio = 0.06;
}

internal static class PhotoInertiaProfileDefaults
{
    internal const string Standard = "standard";
    internal const string Sensitive = "sensitive";
    internal const string Heavy = "heavy";

    internal static string Normalize(string? rawProfile)
    {
        var normalized = (rawProfile ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "SENSITIVE" => Sensitive,
            "HEAVY" => Heavy,
            _ => Standard
        };
    }
}

internal static class PhotoPanDragActivationDefaults
{
    internal const double CrossPageDragDeltaYThresholdDip = 5.0;
}

internal static class PhotoPanInertiaDefaults
{
    internal const int MouseTickIntervalMs = 16;
    internal const double MouseDecelerationDipPerMs2 = 0.0022;
    internal const double MouseStopSpeedDipPerMs = 0.012;
    internal const double MouseFrameElapsedMinMs = 1.0;
    internal const double MouseFrameElapsedMaxMs = 34.0;
    internal const double MouseMaxDurationMs = 1100.0;
    internal const double MouseMaxTranslationPerFrameDip = 150.0;
    internal const double MouseMinReleaseSpeedDipPerMs = 0.06;
    internal const double MouseMaxReleaseSpeedDipPerMs = 4.4;
    internal const double MouseMinVelocitySampleDistanceDip = 0.9;
    internal const double MouseMaxVelocitySampleAgeMs = 140;
    internal const double MouseMinVelocitySampleIntervalMs = 6;
    internal const double MouseVelocitySampleWindowMs = 120;
    internal const double MouseVelocitySampleHistoryMaxAgeMs = 220;
    internal const int MouseVelocitySampleCapacity = 12;
    internal const double MouseVelocityRecentWeightGain = 0.75;
    internal const double TouchMinVelocitySampleDistanceDip = 0.55;
    internal const double TouchMaxVelocitySampleAgeMs = 220;
    internal const double TouchVelocitySampleWindowMs = 170;
    internal const double TouchVelocityRecentWeightGain = 1.0;
    internal const double GestureTranslationDecelerationDipPerMs2 = 0.0034;
    internal const double GestureCrossPageTranslationDecelerationDipPerMs2 = 0.0029;
}

internal readonly record struct PhotoPanVelocitySample(
    System.Windows.Point Position,
    long TimestampTicks);

internal readonly record struct PhotoPanInertiaTuning(
    double MouseDecelerationDipPerMs2,
    double MouseStopSpeedDipPerMs,
    double MouseMinReleaseSpeedDipPerMs,
    double MouseMaxReleaseSpeedDipPerMs,
    double MouseMaxDurationMs,
    double MouseMaxTranslationPerFrameDip,
    double GestureTranslationDecelerationDipPerMs2,
    double GestureCrossPageTranslationDecelerationDipPerMs2)
{
    internal static PhotoPanInertiaTuning Default => new(
        PhotoPanInertiaDefaults.MouseDecelerationDipPerMs2,
        PhotoPanInertiaDefaults.MouseStopSpeedDipPerMs,
        PhotoPanInertiaDefaults.MouseMinReleaseSpeedDipPerMs,
        PhotoPanInertiaDefaults.MouseMaxReleaseSpeedDipPerMs,
        PhotoPanInertiaDefaults.MouseMaxDurationMs,
        PhotoPanInertiaDefaults.MouseMaxTranslationPerFrameDip,
        PhotoPanInertiaDefaults.GestureTranslationDecelerationDipPerMs2,
        PhotoPanInertiaDefaults.GestureCrossPageTranslationDecelerationDipPerMs2);
}

internal enum PhotoPanMouseExecutionAction
{
    PassThrough,
    UpdatePan,
    EndPan
}

internal readonly record struct PhotoPanMouseExecutionPlan(
    PhotoPanMouseExecutionAction Action,
    bool ShouldMarkHandled);

internal enum PhotoPanMouseMoveRoutingDecision
{
    PassThrough,
    UpdatePan,
    EndPan
}

internal enum PhotoPanPointerKind
{
    Mouse,
    Stylus,
    Touch
}

internal readonly record struct PhotoPanReleaseTuning(
    double DecelerationDipPerMs2,
    double StopSpeedDipPerMs,
    double MinReleaseSpeedDipPerMs,
    double MaxReleaseSpeedDipPerMs,
    double MaxDurationMs,
    double MaxTranslationPerFrameDip,
    double VelocitySampleWindowMs,
    double MaxVelocitySampleAgeMs,
    double MinVelocitySampleDistanceDip,
    double VelocityRecentWeightGain);

internal static class PhotoPanPolicies
{
    internal static PhotoEnterTransformInitPlan ResolvePhotoEnterTransformInit(
        bool crossPageDisplayEnabled,
        bool rememberPhotoTransform,
        bool photoUnifiedTransformReady,
        bool hadUserTransformDirty)
    {
        if (!rememberPhotoTransform)
        {
            return new PhotoEnterTransformInitPlan(
                ShouldApplyUnifiedTransform: false,
                ShouldMarkUserDirtyAfterUnifiedApply: false,
                ShouldMarkUnifiedTransformReady: false,
                ShouldTryStoredTransform: false,
                ShouldResetIdentity: true);
        }

        if (crossPageDisplayEnabled)
        {
            if (photoUnifiedTransformReady)
            {
                return new PhotoEnterTransformInitPlan(
                    ShouldApplyUnifiedTransform: true,
                    ShouldMarkUserDirtyAfterUnifiedApply: true,
                    ShouldMarkUnifiedTransformReady: false,
                    ShouldTryStoredTransform: false,
                    ShouldResetIdentity: false);
            }

            if (hadUserTransformDirty)
            {
                return new PhotoEnterTransformInitPlan(
                    ShouldApplyUnifiedTransform: true,
                    ShouldMarkUserDirtyAfterUnifiedApply: false,
                    ShouldMarkUnifiedTransformReady: true,
                    ShouldTryStoredTransform: false,
                    ShouldResetIdentity: false);
            }

            return new PhotoEnterTransformInitPlan(
                ShouldApplyUnifiedTransform: false,
                ShouldMarkUserDirtyAfterUnifiedApply: false,
                ShouldMarkUnifiedTransformReady: false,
                ShouldTryStoredTransform: false,
                ShouldResetIdentity: true);
        }

        return new PhotoEnterTransformInitPlan(
            ShouldApplyUnifiedTransform: false,
            ShouldMarkUserDirtyAfterUnifiedApply: false,
            ShouldMarkUnifiedTransformReady: false,
            ShouldTryStoredTransform: true,
            ShouldResetIdentity: false);
    }

    internal static (double MinX, double MaxX) ResolvePhotoHorizontalPanRange(
        double viewportWidth,
        double scaledWidth,
        bool includeSlack)
    {
        if (viewportWidth <= 0 || scaledWidth <= 0)
        {
            return (0, 0);
        }

        var slack = includeSlack
            ? Math.Max(PhotoHorizontalPanRangeDefaults.MinSlackDip, viewportWidth * PhotoHorizontalPanRangeDefaults.SlackRatio)
            : 0.0;
        if (scaledWidth <= viewportWidth)
        {
            // Allow moving narrow pages to both edges instead of forcing center.
            var minX = -slack;
            var maxX = (viewportWidth - scaledWidth) + slack;
            return (minX, maxX);
        }

        return ((viewportWidth - scaledWidth) - slack, slack);
    }

    internal static bool ShouldActivateCrossPageDrag(
        bool crossPageDisplayActive,
        double deltaYDip,
        double thresholdDip = PhotoPanDragActivationDefaults.CrossPageDragDeltaYThresholdDip)
    {
        return crossPageDisplayActive
            && System.Math.Abs(deltaYDip) > thresholdDip;
    }

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
            PhotoPanPolicies.ResolveReleaseTuning(PhotoPanPointerKind.Mouse, tuning),
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
            PhotoPanPolicies.ResolveReleaseTuning(PhotoPanPointerKind.Mouse, tuning),
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
            PhotoPanPolicies.ResolveReleaseTuning(PhotoPanPointerKind.Mouse, tuning));
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
            PhotoPanPolicies.ResolveReleaseTuning(PhotoPanPointerKind.Mouse, tuning));
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
            PhotoPanPolicies.ResolveReleaseTuning(PhotoPanPointerKind.Mouse, tuning));
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

    private static readonly PhotoPanInertiaTuning Sensitive = new(
        MouseDecelerationDipPerMs2: 0.0026,
        MouseStopSpeedDipPerMs: 0.013,
        MouseMinReleaseSpeedDipPerMs: 0.045,
        MouseMaxReleaseSpeedDipPerMs: 5.2,
        MouseMaxDurationMs: 980.0,
        MouseMaxTranslationPerFrameDip: 175.0,
        GestureTranslationDecelerationDipPerMs2: 0.0040,
        GestureCrossPageTranslationDecelerationDipPerMs2: 0.0034);

    private static readonly PhotoPanInertiaTuning Heavy = new(
        MouseDecelerationDipPerMs2: 0.0016,
        MouseStopSpeedDipPerMs: 0.009,
        MouseMinReleaseSpeedDipPerMs: 0.05,
        MouseMaxReleaseSpeedDipPerMs: 5.0,
        MouseMaxDurationMs: 1500.0,
        MouseMaxTranslationPerFrameDip: 190.0,
        GestureTranslationDecelerationDipPerMs2: 0.0026,
        GestureCrossPageTranslationDecelerationDipPerMs2: 0.0022);

    internal static PhotoPanInertiaTuning ResolveInertiaProfile(string? profile)
    {
        return PhotoInertiaProfileDefaults.Normalize(profile) switch
        {
            PhotoInertiaProfileDefaults.Sensitive => Sensitive,
            PhotoInertiaProfileDefaults.Heavy => Heavy,
            _ => PhotoPanInertiaTuning.Default
        };
    }

    internal static bool ShouldRefresh(
        double lastRefreshTranslateX,
        double lastRefreshTranslateY,
        double currentTranslateX,
        double currentTranslateY,
        double thresholdDip = 0.75)
    {
        return System.Math.Abs(currentTranslateX - lastRefreshTranslateX) >= thresholdDip
            || System.Math.Abs(currentTranslateY - lastRefreshTranslateY) >= thresholdDip;
    }

    internal static bool ShouldEndPanModeSwitch(
        bool photoPanning,
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive)
    {
        if (!photoPanning)
        {
            return false;
        }

        return !PhotoInkInteropPolicies.ShouldPanPhoto(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive);
    }

    internal static PhotoPanMouseExecutionPlan ResolveMove(PhotoPanMouseMoveRoutingDecision decision)
    {
        return decision switch
        {
            PhotoPanMouseMoveRoutingDecision.UpdatePan => new PhotoPanMouseExecutionPlan(
                Action: PhotoPanMouseExecutionAction.UpdatePan,
                ShouldMarkHandled: true),
            PhotoPanMouseMoveRoutingDecision.EndPan => new PhotoPanMouseExecutionPlan(
                Action: PhotoPanMouseExecutionAction.EndPan,
                ShouldMarkHandled: true),
            _ => new PhotoPanMouseExecutionPlan(
                Action: PhotoPanMouseExecutionAction.PassThrough,
                ShouldMarkHandled: false)
        };
    }

    internal static PhotoPanMouseExecutionPlan ResolveEnd(
        bool isMousePhotoPanActive,
        bool shouldEndPan)
    {
        if (!isMousePhotoPanActive || !shouldEndPan)
        {
            return new PhotoPanMouseExecutionPlan(
                Action: PhotoPanMouseExecutionAction.PassThrough,
                ShouldMarkHandled: false);
        }

        return new PhotoPanMouseExecutionPlan(
            Action: PhotoPanMouseExecutionAction.EndPan,
            ShouldMarkHandled: true);
    }

    internal static PhotoPanMouseMoveRoutingDecision ResolveMouseMoveRouting(
        bool isMousePhotoPanActive,
        bool shouldAllowPhotoPan,
        MouseButtonState leftButton,
        MouseButtonState rightButton)
    {
        if (!isMousePhotoPanActive)
        {
            return PhotoPanMouseMoveRoutingDecision.PassThrough;
        }

        return PhotoPanPolicies.ShouldEndPanTermination(
            shouldAllowPhotoPan,
            leftButton,
            rightButton)
            ? PhotoPanMouseMoveRoutingDecision.EndPan
            : PhotoPanMouseMoveRoutingDecision.UpdatePan;
    }

    internal static bool ShouldHandlePhotoPan(
        bool photoPanning,
        bool photoModeActive,
        PaintToolMode mode,
        bool inkOperationActive)
    {
        return photoPanning
               && photoModeActive
               && mode == PaintToolMode.Cursor
               && !inkOperationActive;
    }

    internal static PhotoPanReleaseTuning ResolveReleaseTuning(
        PhotoPanPointerKind pointerKind,
        PhotoPanInertiaTuning tuning)
    {
        return pointerKind switch
        {
            PhotoPanPointerKind.Touch => new PhotoPanReleaseTuning(
                DecelerationDipPerMs2: tuning.MouseDecelerationDipPerMs2 * 0.74,
                StopSpeedDipPerMs: tuning.MouseStopSpeedDipPerMs,
                MinReleaseSpeedDipPerMs: Math.Max(0.03, tuning.MouseMinReleaseSpeedDipPerMs * 0.58),
                MaxReleaseSpeedDipPerMs: Math.Min(6.2, tuning.MouseMaxReleaseSpeedDipPerMs * 1.22),
                MaxDurationMs: Math.Max(1350.0, tuning.MouseMaxDurationMs * 1.15),
                MaxTranslationPerFrameDip: Math.Max(210.0, tuning.MouseMaxTranslationPerFrameDip * 1.2),
                VelocitySampleWindowMs: Math.Max(
                    PhotoPanInertiaDefaults.TouchVelocitySampleWindowMs,
                    PhotoPanInertiaDefaults.MouseVelocitySampleWindowMs * 1.25),
                MaxVelocitySampleAgeMs: Math.Max(
                    PhotoPanInertiaDefaults.TouchMaxVelocitySampleAgeMs,
                    PhotoPanInertiaDefaults.MouseMaxVelocitySampleAgeMs * 1.4),
                MinVelocitySampleDistanceDip: Math.Min(
                    PhotoPanInertiaDefaults.MouseMinVelocitySampleDistanceDip,
                    PhotoPanInertiaDefaults.TouchMinVelocitySampleDistanceDip),
                VelocityRecentWeightGain: Math.Max(
                    PhotoPanInertiaDefaults.TouchVelocityRecentWeightGain,
                    PhotoPanInertiaDefaults.MouseVelocityRecentWeightGain + 0.15)),
            _ => new PhotoPanReleaseTuning(
                DecelerationDipPerMs2: tuning.MouseDecelerationDipPerMs2,
                StopSpeedDipPerMs: tuning.MouseStopSpeedDipPerMs,
                MinReleaseSpeedDipPerMs: tuning.MouseMinReleaseSpeedDipPerMs,
                MaxReleaseSpeedDipPerMs: tuning.MouseMaxReleaseSpeedDipPerMs,
                MaxDurationMs: tuning.MouseMaxDurationMs,
                MaxTranslationPerFrameDip: tuning.MouseMaxTranslationPerFrameDip,
                VelocitySampleWindowMs: PhotoPanInertiaDefaults.MouseVelocitySampleWindowMs,
                MaxVelocitySampleAgeMs: PhotoPanInertiaDefaults.MouseMaxVelocitySampleAgeMs,
                MinVelocitySampleDistanceDip: PhotoPanInertiaDefaults.MouseMinVelocitySampleDistanceDip,
                VelocityRecentWeightGain: PhotoPanInertiaDefaults.MouseVelocityRecentWeightGain)
        };
    }

    internal static bool ShouldEndPanTermination(
        bool shouldAllowPhotoPan,
        MouseButtonState leftButton,
        MouseButtonState rightButton)
    {
        if (!shouldAllowPhotoPan)
        {
            return true;
        }

        return leftButton != MouseButtonState.Pressed
               && rightButton != MouseButtonState.Pressed;
    }

    internal const double OverlapRatio = 0.12;
    internal const double MinStepDip = 24.0;

    internal static double ResolveStep(double viewportHeight)
    {
        return Math.Max(MinStepDip, viewportHeight * (1.0 - OverlapRatio));
    }
}
