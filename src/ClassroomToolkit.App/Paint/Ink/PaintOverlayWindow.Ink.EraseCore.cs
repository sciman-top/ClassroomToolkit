using System;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void HideEraserPreview()
    {
        _ = _isErasing;
        // Eraser live preview is currently disabled.
    }

    private void ApplyEraserAt(WpfPoint position)
    {
        var radius = Math.Max(InkGeometryDefaults.MinEraserRadiusDip, _eraserSize * 0.5);
        var geometry = new EllipseGeometry(position, radius, radius);
        EraseGeometry(geometry);
    }

    private bool EraseRect(Rect region)
    {
        if (IsPhotoInkModeActive())
        {
            return EraseGeometry(new RectangleGeometry(region));
        }
        var eraseGeometry = new RectangleGeometry(region);
        var changed = ApplyInkErase(eraseGeometry);
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return changed || !_inkRecordEnabled;
        }
        var dpi = VisualTreeHelper.GetDpi(this);
        var rect = new Int32Rect(
            (int)Math.Floor(region.X * dpi.DpiScaleX),
            (int)Math.Floor(region.Y * dpi.DpiScaleY),
            (int)Math.Ceiling(region.Width * dpi.DpiScaleX),
            (int)Math.Ceiling(region.Height * dpi.DpiScaleY));
        rect = IntersectRects(rect, new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return changed || !_inkRecordEnabled;
        }
        var stride = rect.Width * 4;
        // 每次区域擦除手势一次：清零缓冲 grow-only 复用，不再按矩形尺寸瞬时分配。
        var requiredClearBytes = stride * rect.Height;
        if (_eraseRectClearBuffer == null || _eraseRectClearBuffer.Length < requiredClearBytes)
        {
            _eraseRectClearBuffer = new byte[requiredClearBytes];
        }
        _rasterSurface.WritePixels(rect, _eraseRectClearBuffer, stride, 0);
        _hasDrawing = true;
        return changed || !_inkRecordEnabled;
    }

    private void ClearRegionSelection()
    {
        if (_regionRect != null)
        {
            PreviewCanvas.Children.Remove(_regionRect);
            _regionRect = null;
        }
        _isRegionSelecting = false;
    }
}
