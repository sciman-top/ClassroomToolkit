using System.Windows;
using System.Windows.Input;
using System.Linq;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void OnManipulationStarting(object? sender, ManipulationStartingEventArgs e)
    {
        if (!TryAdmitPhotoManipulation(e, CaptureInputInteractionState()))
        {
            _photoManipulating = false;
            return;
        }
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
        _photoManipulating = true;
        e.ManipulationContainer = OverlayRoot;
        e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
    }

    private void OnManipulationInertiaStarting(object? sender, ManipulationInertiaStartingEventArgs e)
    {
        var interactionState = CaptureInputInteractionState();
        if (!TryAdmitPhotoManipulation(e, interactionState))
        {
            return;
        }
        _photoManipulating = true;
        e.TranslationBehavior.DesiredDeceleration = PhotoManipulationInertiaPolicy.ResolveTranslationDeceleration(
            interactionState.CrossPageDisplayActive,
            _photoPanInertiaTuning);
        e.Handled = true;
    }

    private void OnManipulationDelta(object? sender, ManipulationDeltaEventArgs e)
    {
        var interactionState = CaptureInputInteractionState();
        if (!TryAdmitPhotoManipulation(e, interactionState))
        {
            _photoManipulating = false;
            return;
        }
        _photoManipulating = true;
        MarkPhotoInteractionForRenderQuality();
        // Re-check at delta time to prevent gesture pan from racing with active ink operations.
        if (IsInkOperationActive())
        {
            e.Handled = true;
            return;
        }
        EnsurePhotoTransformsWritable();
        MarkPhotoGestureInput();
        var scale = e.DeltaManipulation.Scale;
        var factor = (scale.X + scale.Y) / 2.0;
        ApplyPhotoZoomInput(PhotoZoomInputSource.Gesture, factor, e.ManipulationOrigin);
        LogPhotoInputTelemetry("gesture-zoom", $"factor={factor:0.####}");
        var translation = e.DeltaManipulation.Translation;
        var deltaExecutionPlan = PhotoManipulationDeltaExecutionPolicy.Resolve(
            translation,
            PhotoZoomInputDefaults.ManipulationTranslationEpsilonDip,
            interactionState.CrossPageDisplayActive);
        if (deltaExecutionPlan.ShouldApplyTranslation)
        {
            _photoTranslate.X += translation.X;
            _photoTranslate.Y += translation.Y;
            ApplyPhotoPanBounds(allowResistance: true);
            UpdatePhotoInkPanCompensation();
            var shouldRefresh = PhotoPanInteractiveRefreshPolicy.ShouldRefresh(
                _lastPhotoInteractiveRefreshTranslateX,
                _lastPhotoInteractiveRefreshTranslateY,
                _photoTranslate.X,
                _photoTranslate.Y);
            SchedulePhotoTransformSave(userAdjusted: true);
            if (shouldRefresh)
            {
                SyncPhotoInteractiveRefreshAnchor();
                UpdateNeighborTransformsForPan();
                if (PhotoInkPanRedrawPolicy.ShouldRequest(
                        IsPhotoInkModeActive(),
                        _photoTranslate.X,
                        _photoTranslate.Y,
                        _lastInkRedrawPhotoTranslateX,
                        _lastInkRedrawPhotoTranslateY))
                {
                    RequestPhotoTransformInkRedraw();
                }
            }
            if (deltaExecutionPlan.ShouldLogPanTelemetry)
            {
                LogPhotoInputTelemetry("gesture-pan", $"dx={translation.X:0.##},dy={translation.Y:0.##}");
            }
        }
        if (deltaExecutionPlan.ShouldRequestCrossPageUpdate
            && !IsPhotoZoomInteractionActive())
        {
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.ManipulationDelta);
        }
    }

    private void OnManipulationCompleted(object? sender, ManipulationCompletedEventArgs e)
    {
        _photoManipulating = false;
        if (!TryAdmitPhotoManipulation(e, CaptureInputInteractionState()))
        {
            return;
        }

        ApplyPhotoPanBounds(allowResistance: false);
        if (IsCrossPageDisplayActive())
        {
            if (IsPhotoZoomInteractionActive())
            {
                MarkPhotoInteractionForRenderQuality();
            }
            else
            {
                RequestCrossPageDisplayUpdate(CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.ManipulationDelta));
            }
        }
        SchedulePhotoTransformSave(userAdjusted: true);
    }

    private bool TryAdmitPhotoManipulation(
        InputEventArgs e,
        InputInteractionState interactionState)
    {
        var handlingPlan = PhotoManipulationAdmissionPolicy.Resolve(
            interactionState.PhotoModeActive,
            interactionState.BoardActive,
            _mode,
            IsInkOperationActive(),
            _photoPanning,
            ResolveManipulationTouchCount(e));
        if (handlingPlan.ShouldMarkHandled)
        {
            e.Handled = true;
        }

        return handlingPlan.ShouldHandle;
    }

    private static int ResolveManipulationTouchCount(InputEventArgs e)
    {
        return e switch
        {
            ManipulationStartingEventArgs starting => CountManipulators(starting.Manipulators),
            ManipulationDeltaEventArgs delta => CountManipulators(delta.Manipulators),
            // Inertia/completion may have zero live manipulators after the gesture is admitted.
            ManipulationInertiaStartingEventArgs inertia => Math.Max(2, CountManipulators(inertia.Manipulators)),
            ManipulationCompletedEventArgs completed => Math.Max(2, CountManipulators(completed.Manipulators)),
            _ => 0
        };
    }

    private static int CountManipulators(IEnumerable<IManipulator>? manipulators)
    {
        return manipulators?.Count() ?? 0;
    }
}
