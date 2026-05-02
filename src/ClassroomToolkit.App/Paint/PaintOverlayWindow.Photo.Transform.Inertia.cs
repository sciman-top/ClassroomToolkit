using System;
using System.Diagnostics;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void ResetPhotoPanVelocitySamples(WpfPoint position)
    {
        var nowTicks = Stopwatch.GetTimestamp();
        _photoPanVelocitySamples.Clear();
        _photoPanVelocitySamples.Add(new PhotoPanVelocitySample(position, nowTicks));
    }

    private void UpdatePhotoPanVelocitySamples(WpfPoint position)
    {
        var nowTicks = Stopwatch.GetTimestamp();
        if (_photoPanVelocitySamples.Count <= 0)
        {
            ResetPhotoPanVelocitySamples(position);
            return;
        }

        var lastTimestampTicks = _photoPanVelocitySamples[^1].TimestampTicks;
        if (nowTicks <= lastTimestampTicks)
        {
            nowTicks = lastTimestampTicks + 1;
        }

        _photoPanVelocitySamples.Add(new PhotoPanVelocitySample(position, nowTicks));
        TrimPhotoPanVelocitySamples(nowTicks);
    }

    private void TrimPhotoPanVelocitySamples(long nowTicks)
    {
        var maxAgeTicks = (long)Math.Ceiling(
            PhotoPanInertiaDefaults.MouseVelocitySampleHistoryMaxAgeMs * Stopwatch.Frequency / 1000.0);
        while (_photoPanVelocitySamples.Count > 1
               && nowTicks - _photoPanVelocitySamples[0].TimestampTicks > maxAgeTicks)
        {
            _photoPanVelocitySamples.RemoveAt(0);
        }

        while (_photoPanVelocitySamples.Count > PhotoPanInertiaDefaults.MouseVelocitySampleCapacity)
        {
            _photoPanVelocitySamples.RemoveAt(0);
        }
    }

    private bool TryStartPhotoPanInertiaFromRelease()
    {
        var nowTicks = Stopwatch.GetTimestamp();
        var releaseTuning = PhotoPanReleaseTuningPolicy.Resolve(_photoPanActivePointerKind, _photoPanInertiaTuning);
        if (!PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
                _photoPanVelocitySamples,
                nowTicks,
                Stopwatch.Frequency,
                releaseTuning,
                out var velocityDipPerMs))
        {
            return false;
        }

        _photoPanInertiaVelocityDipPerMs = velocityDipPerMs;
        var nowUtc = GetCurrentUtcTimestamp();
        _photoPanInertiaLastTickUtc = nowUtc;
        _photoPanInertiaStartUtc = nowUtc;
        _photoPanInertiaLastRenderingTime = TimeSpan.MinValue;
        if (!_photoPanInertiaRenderingAttached)
        {
            CompositionTarget.Rendering += OnPhotoPanInertiaRendering;
            _photoPanInertiaRenderingAttached = true;
        }
        LogPhotoInputTelemetry(
            "pan-inertia-start",
            $"vx={_photoPanInertiaVelocityDipPerMs.X:0.###},vy={_photoPanInertiaVelocityDipPerMs.Y:0.###}");
        return true;
    }

    private void OnPhotoPanInertiaRendering(object? sender, EventArgs e)
    {
        if (!_photoModeActive || _photoPanning || _photoPanInertiaLastTickUtc == PhotoInputConflictDefaults.UnsetTimestampUtc)
        {
            StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
            return;
        }

        var nowUtc = GetCurrentUtcTimestamp();
        var releaseTuning = PhotoPanReleaseTuningPolicy.Resolve(_photoPanActivePointerKind, _photoPanInertiaTuning);
        if (_photoPanInertiaStartUtc != PhotoInputConflictDefaults.UnsetTimestampUtc)
        {
            var durationMs = (nowUtc - _photoPanInertiaStartUtc).TotalMilliseconds;
            if (PhotoPanInertiaMotionPolicy.ShouldStopByDuration(durationMs, releaseTuning))
            {
                StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
                return;
            }
        }

        var fallbackElapsedMs = (nowUtc - _photoPanInertiaLastTickUtc).TotalMilliseconds;
        double elapsedMs = fallbackElapsedMs;
        if (e is RenderingEventArgs renderingArgs)
        {
            if (_photoPanInertiaLastRenderingTime != TimeSpan.MinValue
                && renderingArgs.RenderingTime > _photoPanInertiaLastRenderingTime)
            {
                elapsedMs = (renderingArgs.RenderingTime - _photoPanInertiaLastRenderingTime).TotalMilliseconds;
            }
            _photoPanInertiaLastRenderingTime = renderingArgs.RenderingTime;
        }

        elapsedMs = PhotoPanInertiaMotionPolicy.ResolveFrameElapsedMilliseconds(elapsedMs);
        if (elapsedMs <= 0)
        {
            return;
        }
        _photoPanInertiaLastTickUtc = nowUtc;

        if (!PhotoPanInertiaMotionPolicy.TryResolveInertiaStep(
            _photoPanInertiaVelocityDipPerMs,
            elapsedMs,
            releaseTuning,
            out var translation,
            out var nextVelocityDipPerMs))
        {
            StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
            return;
        }

        EnsurePhotoTransformsWritable();
        var beforeX = _photoTranslate.X;
        var beforeY = _photoTranslate.Y;
        _photoTranslate.X += translation.X;
        _photoTranslate.Y += translation.Y;
        ApplyPhotoPanBounds(allowResistance: false);
        var moved = Math.Abs(_photoTranslate.X - beforeX) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip
            || Math.Abs(_photoTranslate.Y - beforeY) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip;
        if (!moved)
        {
            StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
            return;
        }

        UpdatePhotoInkPanCompensation();
        MarkPhotoInteractionForRenderQuality();
        var shouldRefresh = PhotoPanInteractiveRefreshPolicy.ShouldRefresh(
            _lastPhotoInteractiveRefreshTranslateX,
            _lastPhotoInteractiveRefreshTranslateY,
            _photoTranslate.X,
            _photoTranslate.Y);
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
        if (IsCrossPageDisplayActive())
        {
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.PhotoPan);
        }
        SchedulePhotoTransformSave(userAdjusted: true);

        _photoPanInertiaVelocityDipPerMs = nextVelocityDipPerMs;
        if (_photoPanInertiaVelocityDipPerMs.LengthSquared <= 0)
        {
            StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
        }
    }

    private void StopPhotoPanInertia(bool flushTransformSave, bool resetInkPanCompensation)
    {
        if (_photoPanInertiaRenderingAttached)
        {
            CompositionTarget.Rendering -= OnPhotoPanInertiaRendering;
            _photoPanInertiaRenderingAttached = false;
        }
        _photoPanInertiaVelocityDipPerMs = default;
        _photoPanInertiaLastTickUtc = PhotoInputConflictDefaults.UnsetTimestampUtc;
        _photoPanInertiaStartUtc = PhotoInputConflictDefaults.UnsetTimestampUtc;
        _photoPanInertiaLastRenderingTime = TimeSpan.MinValue;
        if (flushTransformSave)
        {
            FlushPhotoTransformSave();
        }
        if (resetInkPanCompensation)
        {
            ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        }
    }
}
