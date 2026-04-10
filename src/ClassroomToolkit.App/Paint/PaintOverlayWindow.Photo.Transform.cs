using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Windowing;
using IoPath = System.IO.Path;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool TryStepPhotoViewport(int direction)
    {
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
        var viewportHeight = ResolvePhotoViewportHeight();
        if (viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            return false;
        }
        EnsurePhotoTransformsWritable();
        var step = PhotoViewportStepPolicy.ResolveStep(viewportHeight);
        var originalY = _photoTranslate.Y;
        _photoTranslate.Y -= direction * step;

        if (IsCrossPageDisplayActive())
        {
            ApplyCrossPageBoundaryLimits(includeSlack: false);
            SyncCurrentPageToViewportCenter();
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.StepViewport);
        }
        else
        {
            ClampSinglePageTranslateY(viewportHeight);
        }

        var moved = Math.Abs(_photoTranslate.Y - originalY) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip;
        if (!moved)
        {
            return false;
        }
        SchedulePhotoTransformSave(userAdjusted: true);
        UpdatePhotoInkClip();
        RequestPhotoTransformInkRedraw();
        return true;
    }

    private void RequestPhotoTransformInkRedraw()
    {
        if (!IsPhotoInkModeActive())
        {
            return;
        }

        if (TryEnforceRuntimeEmptyGuardForCurrentPage())
        {
            return;
        }

        MarkInkTransformVersionDirty();
        RequestInkRedraw();
    }

    private void SyncPhotoInteractiveRefreshAnchor()
    {
        _lastPhotoInteractiveRefreshTranslateX = _photoTranslate.X;
        _lastPhotoInteractiveRefreshTranslateY = _photoTranslate.Y;
    }

    private void UpdatePhotoInkPanCompensation()
    {
        var delta = PhotoInkPanCompensationPolicy.Resolve(
            IsPhotoInkModeActive(),
            _photoTranslate.X,
            _photoTranslate.Y,
            _lastInkRedrawPhotoTranslateX,
            _lastInkRedrawPhotoTranslateY);
        _photoInkPanCompensation.X = delta.X;
        _photoInkPanCompensation.Y = delta.Y;
        UpdatePhotoInkClip();
    }

    private void ResetPhotoInkPanCompensation(bool syncToCurrentPhotoTranslate)
    {
        _photoInkPanCompensation.X = 0;
        _photoInkPanCompensation.Y = 0;
        if (syncToCurrentPhotoTranslate)
        {
            _lastInkRedrawPhotoTranslateX = _photoTranslate.X;
            _lastInkRedrawPhotoTranslateY = _photoTranslate.Y;
            SyncPhotoInteractiveRefreshAnchor();
        }
        UpdatePhotoInkClip();
    }

}
