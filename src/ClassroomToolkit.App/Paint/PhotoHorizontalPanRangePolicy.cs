namespace ClassroomToolkit.App.Paint;

internal static class PhotoHorizontalPanRangePolicy
{
    internal static (double MinX, double MaxX) Resolve(
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
}
