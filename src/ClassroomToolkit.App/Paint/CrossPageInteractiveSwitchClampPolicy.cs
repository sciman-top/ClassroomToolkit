using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractiveSwitchClampPolicy
{
    internal static double ClampTranslateY(
        double candidateTranslateY,
        int targetPage,
        int totalPages,
        double viewportHeight,
        Func<int, double> getPageHeight,
        double fallbackPageHeight)
    {
        if (targetPage <= 0 || totalPages <= 0 || viewportHeight <= 0)
        {
            return candidateTranslateY;
        }

        var boundedTargetPage = Math.Clamp(
            targetPage,
            CrossPageInteractiveSwitchClampDefaults.MinPageIndex,
            totalPages);
        var safeFallbackHeight = Math.Max(
            CrossPageInteractiveSwitchClampDefaults.MinFallbackPageHeight,
            fallbackPageHeight);
        var targetHeight = ResolveHeight(boundedTargetPage, getPageHeight, safeFallbackHeight);

        double totalHeightAbove = 0;
        for (int page = CrossPageInteractiveSwitchClampDefaults.MinPageIndex; page < boundedTargetPage; page++)
        {
            totalHeightAbove += ResolveHeight(page, getPageHeight, safeFallbackHeight);
        }

        double totalHeightBelow = 0;
        for (int page = boundedTargetPage + 1; page <= totalPages; page++)
        {
            totalHeightBelow += ResolveHeight(page, getPageHeight, safeFallbackHeight);
        }

        var maxY = totalHeightAbove;
        var minY = -(targetHeight + totalHeightBelow - viewportHeight);
        if (minY > maxY)
        {
            var center = (minY + maxY) * 0.5;
            minY = center;
            maxY = center;
        }

        return Math.Clamp(candidateTranslateY, minY, maxY);
    }

    private static double ResolveHeight(
        int pageIndex,
        Func<int, double> getPageHeight,
        double fallbackPageHeight)
    {
        var height = Math.Max(CrossPageInteractiveSwitchClampDefaults.MinResolvedPageHeight, getPageHeight(pageIndex));
        return height > CrossPageInteractiveSwitchClampDefaults.MinResolvedPageHeight
            ? height
            : fallbackPageHeight;
    }
}
