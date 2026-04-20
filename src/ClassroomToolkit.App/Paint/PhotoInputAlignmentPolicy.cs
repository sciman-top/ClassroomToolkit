namespace ClassroomToolkit.App.Paint;

internal enum PhotoZoomInputSource
{
    Wheel,
    Gesture,
    Keyboard
}

internal static class PhotoZoomNormalizer
{
    internal static bool TryNormalizeFactor(
        PhotoZoomInputSource source,
        double rawValue,
        double wheelBase,
        double gestureSensitivity,
        double gestureNoiseThreshold,
        double minEventFactor,
        double maxEventFactor,
        out double factor)
    {
        factor = 1.0;
        var candidate = source switch
        {
            PhotoZoomInputSource.Wheel => Math.Pow(wheelBase, rawValue),
            PhotoZoomInputSource.Gesture => rawValue,
            PhotoZoomInputSource.Keyboard => rawValue,
            _ => rawValue
        };

        if (double.IsNaN(candidate) || double.IsInfinity(candidate) || candidate <= 0)
        {
            return false;
        }

        if (source == PhotoZoomInputSource.Gesture)
        {
            var sensitivity = Math.Clamp(
                gestureSensitivity,
                PhotoInputAlignmentDefaults.GestureSensitivityMin,
                PhotoInputAlignmentDefaults.GestureSensitivityMax);
            candidate = 1.0 + ((candidate - 1.0) * sensitivity);
        }

        if (source == PhotoZoomInputSource.Gesture && Math.Abs(candidate - 1.0) < Math.Max(0.0, gestureNoiseThreshold))
        {
            return false;
        }

        var minFactor = Math.Max(
            PhotoInputAlignmentDefaults.MinEventFactorFloor,
            Math.Min(minEventFactor, maxEventFactor));
        var maxFactor = Math.Max(minFactor, Math.Max(minEventFactor, maxEventFactor));
        candidate = Math.Clamp(candidate, minFactor, maxFactor);
        if (Math.Abs(candidate - 1.0) < PhotoInputAlignmentDefaults.IgnoreFactorDelta)
        {
            return false;
        }

        factor = candidate;
        return true;
    }
}

internal static class StylusCursorPolicy
{
    internal static bool ShouldPanPhoto(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive)
    {
        return photoModeActive
            && !boardActive
            && mode == PaintToolMode.Cursor
            && !inkOperationActive;
    }
}

internal static class PhotoPanBeginGuardPolicy
{
    internal static bool ShouldBegin(bool shouldPanPhoto, bool photoPanning)
    {
        return shouldPanPhoto && !photoPanning;
    }
}

internal enum StylusPhotoPanPhase
{
    Down,
    Move,
    Up
}

internal enum StylusPhotoPanRoutingDecision
{
    PassThrough,
    BeginPan,
    UpdatePan,
    EndPan
}

internal static class StylusPhotoPanRoutingPolicy
{
    internal static StylusPhotoPanRoutingDecision Resolve(
        bool shouldPanPhoto,
        bool photoPanning,
        StylusPhotoPanPhase phase)
    {
        if (!shouldPanPhoto)
        {
            return StylusPhotoPanRoutingDecision.PassThrough;
        }

        return phase switch
        {
            StylusPhotoPanPhase.Down => StylusPhotoPanRoutingDecision.BeginPan,
            StylusPhotoPanPhase.Move when photoPanning => StylusPhotoPanRoutingDecision.UpdatePan,
            StylusPhotoPanPhase.Up when photoPanning => StylusPhotoPanRoutingDecision.EndPan,
            _ => StylusPhotoPanRoutingDecision.PassThrough
        };
    }
}

internal enum StylusPhotoPanExecutionAction
{
    PassThrough,
    ReturnWithoutPan,
    BeginPan,
    UpdatePan,
    EndPan
}

internal readonly record struct StylusPhotoPanExecutionPlan(
    StylusPhotoPanExecutionAction Action,
    bool ShouldMarkHandled);

internal static class StylusPhotoPanExecutionPolicy
{
    internal static StylusPhotoPanExecutionPlan Resolve(
        StylusPhotoPanRoutingDecision routingDecision,
        bool sourceShouldContinue,
        bool sourceShouldMarkHandled,
        bool shouldBeginPan)
    {
        return routingDecision switch
        {
            StylusPhotoPanRoutingDecision.PassThrough => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.PassThrough,
                ShouldMarkHandled: false),
            StylusPhotoPanRoutingDecision.BeginPan when !sourceShouldContinue => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.ReturnWithoutPan,
                ShouldMarkHandled: sourceShouldMarkHandled),
            StylusPhotoPanRoutingDecision.BeginPan when !shouldBeginPan => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.ReturnWithoutPan,
                ShouldMarkHandled: true),
            StylusPhotoPanRoutingDecision.BeginPan => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.BeginPan,
                ShouldMarkHandled: true),
            StylusPhotoPanRoutingDecision.UpdatePan => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.UpdatePan,
                ShouldMarkHandled: true),
            StylusPhotoPanRoutingDecision.EndPan => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.EndPan,
                ShouldMarkHandled: true),
            _ => new StylusPhotoPanExecutionPlan(
                Action: StylusPhotoPanExecutionAction.PassThrough,
                ShouldMarkHandled: false)
        };
    }
}

internal enum PhotoManipulationRoutingDecision
{
    Ignore,
    Consume,
    Handle
}

internal static class PhotoManipulationRoutingPolicy
{
    internal static PhotoManipulationRoutingDecision Resolve(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool photoPanning,
        int activeTouchCount)
    {
        if (boardActive)
        {
            return PhotoManipulationRoutingDecision.Consume;
        }
        if (!photoModeActive)
        {
            return PhotoManipulationRoutingDecision.Ignore;
        }
        if (inkOperationActive || photoPanning)
        {
            return PhotoManipulationRoutingDecision.Consume;
        }
        return PhotoTouchInteractionPolicy.ShouldUseManipulationZoom(activeTouchCount)
            ? PhotoManipulationRoutingDecision.Handle
            : PhotoManipulationRoutingDecision.Consume;
    }
}

internal readonly record struct PhotoManipulationEventHandlingPlan(
    bool ShouldHandle,
    bool ShouldMarkHandled);

internal static class PhotoManipulationEventHandlingPolicy
{
    internal static PhotoManipulationEventHandlingPlan Resolve(PhotoManipulationRoutingDecision decision)
    {
        return decision switch
        {
            PhotoManipulationRoutingDecision.Handle => new PhotoManipulationEventHandlingPlan(
                ShouldHandle: true,
                ShouldMarkHandled: true),
            PhotoManipulationRoutingDecision.Consume => new PhotoManipulationEventHandlingPlan(
                ShouldHandle: false,
                ShouldMarkHandled: true),
            _ => new PhotoManipulationEventHandlingPlan(
                ShouldHandle: false,
                ShouldMarkHandled: false)
        };
    }
}

internal static class PhotoPanLimiter
{
    internal static double ApplyAxis(
        double value,
        double min,
        double max,
        bool allowResistance,
        double resistanceFactor = PhotoInputAlignmentDefaults.PanResistanceFactorDefault)
    {
        if (min > max)
        {
            (min, max) = (max, min);
        }
        if (!allowResistance)
        {
            return Math.Clamp(value, min, max);
        }
        var factor = Math.Clamp(
            resistanceFactor,
            PhotoInputAlignmentDefaults.PanResistanceFactorMin,
            PhotoInputAlignmentDefaults.PanResistanceFactorMax);
        if (value < min)
        {
            return min - ((min - value) * factor);
        }
        if (value > max)
        {
            return max + ((value - max) * factor);
        }
        return value;
    }
}

internal static class PhotoInputConflictGuard
{
    internal static bool ShouldSuppressWheelAfterGesture(DateTime lastGestureUtc, int suppressWindowMs, DateTime nowUtc)
    {
        if (lastGestureUtc == PhotoInputConflictDefaults.UnsetTimestampUtc)
        {
            return false;
        }
        var window = Math.Max(PhotoInputConflictDefaults.SuppressWindowMinMs, suppressWindowMs);
        if (window == 0)
        {
            return false;
        }
        return (nowUtc - lastGestureUtc).TotalMilliseconds <= window;
    }
}
