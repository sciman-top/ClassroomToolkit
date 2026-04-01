using System;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageNeighborInkRenderSurfacePlan(
    int PixelWidth,
    int PixelHeight,
    double HorizontalOffsetDip);

internal static class CrossPageNeighborInkRenderSurfacePolicy
{
    // Keep horizontal overflow bounded to avoid large RenderTargetBitmap allocations
    // when malformed stroke geometry reports extreme bounds.
    internal const int MaxHorizontalOverflowDipPerSidePx = 320;

    internal static CrossPageNeighborInkRenderSurfacePlan Resolve(
        int pagePixelWidth,
        int pagePixelHeight,
        double dpiX,
        double pageWidthDip,
        double minStrokeXDip,
        double maxStrokeXDip)
    {
        if (pagePixelWidth <= 0 || pagePixelHeight <= 0 || pageWidthDip <= 0)
        {
            return new CrossPageNeighborInkRenderSurfacePlan(pagePixelWidth, pagePixelHeight, 0);
        }

        if (double.IsNaN(minStrokeXDip)
            || double.IsNaN(maxStrokeXDip)
            || double.IsInfinity(minStrokeXDip)
            || double.IsInfinity(maxStrokeXDip)
            || maxStrokeXDip <= minStrokeXDip)
        {
            return new CrossPageNeighborInkRenderSurfacePlan(pagePixelWidth, pagePixelHeight, 0);
        }

        var safeDpiX = dpiX > 1 ? dpiX : 96.0;
        var leftOverflowDip = Math.Max(0, -minStrokeXDip);
        var rightOverflowDip = Math.Max(0, maxStrokeXDip - pageWidthDip);

        if (leftOverflowDip <= 0.01 && rightOverflowDip <= 0.01)
        {
            return new CrossPageNeighborInkRenderSurfacePlan(pagePixelWidth, pagePixelHeight, 0);
        }

        var maxOverflowDip = MaxHorizontalOverflowDipPerSidePx;
        leftOverflowDip = Math.Min(leftOverflowDip, maxOverflowDip);
        rightOverflowDip = Math.Min(rightOverflowDip, maxOverflowDip);

        var leftOverflowPx = (int)Math.Ceiling(leftOverflowDip * safeDpiX / 96.0);
        var rightOverflowPx = (int)Math.Ceiling(rightOverflowDip * safeDpiX / 96.0);

        if (leftOverflowPx <= 0 && rightOverflowPx <= 0)
        {
            return new CrossPageNeighborInkRenderSurfacePlan(pagePixelWidth, pagePixelHeight, 0);
        }

        var resolvedWidth = checked(pagePixelWidth + leftOverflowPx + rightOverflowPx);
        var resolvedOffsetDip = leftOverflowPx * 96.0 / safeDpiX;
        return new CrossPageNeighborInkRenderSurfacePlan(resolvedWidth, pagePixelHeight, resolvedOffsetDip);
    }
}
