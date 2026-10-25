using System;
using System.Windows.Media;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void MarkPhotoInteractionForRenderQuality()
    {
        if (!_photoModeActive)
        {
            return;
        }

        ApplyPhotoRenderQualityMode(useLowQualityScaling: true);
        _photoRenderQualityRestoreTimer.Stop();
        _photoRenderQualityRestoreTimer.Start();
    }

    private void OnPhotoRenderQualityRestoreTimerTick(object? sender, EventArgs e)
    {
        _photoRenderQualityRestoreTimer.Stop();
        if (ShouldIgnoreLifecycleTick())
        {
            return;
        }

        if (!_photoModeActive)
        {
            ApplyPhotoRenderQualityMode(useLowQualityScaling: false);
            return;
        }

        if (IsCrossPageInteractionActive())
        {
            MarkPhotoInteractionForRenderQuality();
            return;
        }

        if (IsCrossPageDisplayActive())
        {
            ApplyCrossPageBoundaryLimits(includeSlack: false);
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.ApplyScale));
        }

        ApplyPhotoRenderQualityMode(useLowQualityScaling: false);
    }

    private void ApplyPhotoRenderQualityMode(bool useLowQualityScaling, bool forceApply = false)
    {
        if (!forceApply && _photoUseLowQualityScaling == useLowQualityScaling)
        {
            return;
        }

        _photoUseLowQualityScaling = useLowQualityScaling;
        var scalingMode = useLowQualityScaling
            ? BitmapScalingMode.LowQuality
            : BitmapScalingMode.HighQuality;
        ApplyPhotoRenderOptions(PhotoBackground, scalingMode, enableBitmapCache: true);
        ApplyPhotoRenderOptions(RasterImage, scalingMode, enableBitmapCache: false);
        for (var i = 0; i < _neighborPageImages.Count; i++)
        {
            ApplyPhotoRenderOptions(_neighborPageImages[i], scalingMode, enableBitmapCache: true);
        }

        for (var i = 0; i < _neighborInkImages.Count; i++)
        {
            ApplyPhotoRenderOptions(_neighborInkImages[i], scalingMode, enableBitmapCache: true);
        }
    }

    private void ReapplyPhotoRenderQualityModeForDynamicSurfaces()
    {
        ApplyPhotoRenderQualityMode(
            useLowQualityScaling: _photoUseLowQualityScaling,
            forceApply: true);
    }

    private static void ApplyPhotoRenderOptions(
        WpfImage image,
        BitmapScalingMode scalingMode,
        bool enableBitmapCache)
    {
        RenderOptions.SetBitmapScalingMode(image, scalingMode);
        if (!enableBitmapCache || image.CacheMode is BitmapCache)
        {
            return;
        }

        image.CacheMode = new BitmapCache();
    }
}
