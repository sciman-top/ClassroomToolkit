using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInputNavigation
{
    internal static int ResolveTargetPage(
        double pointerY,
        int currentPage,
        int totalPages,
        double currentTop,
        double currentHeight,
        Func<int, double> getPageHeight)
    {
        if (totalPages <= 1 || currentPage <= 0 || currentPage > totalPages || currentHeight <= 0)
        {
            return Math.Clamp(currentPage, 1, Math.Max(1, totalPages));
        }

        var currentBottom = currentTop + currentHeight;
        if (pointerY >= currentTop && pointerY <= currentBottom)
        {
            return currentPage;
        }

        if (pointerY < currentTop)
        {
            var top = currentTop;
            for (int page = currentPage - 1; page >= 1; page--)
            {
                var height = Math.Max(0, getPageHeight(page));
                top -= height;
                if (pointerY >= top && pointerY <= top + height)
                {
                    return page;
                }
            }
            return 1;
        }

        var nextTop = currentBottom;
        for (int page = currentPage + 1; page <= totalPages; page++)
        {
            var height = Math.Max(0, getPageHeight(page));
            if (pointerY >= nextTop && pointerY <= nextTop + height)
            {
                return page;
            }
            nextTop += height;
        }

        return totalPages;
    }

    internal static double ComputePageOffset(
        int currentPage,
        int targetPage,
        Func<int, double> getPageHeight)
    {
        if (targetPage == currentPage)
        {
            return 0;
        }

        double offset = 0;
        if (targetPage > currentPage)
        {
            for (int page = currentPage; page < targetPage; page++)
            {
                offset += Math.Max(0, getPageHeight(page));
            }
            return offset;
        }

        for (int page = currentPage - 1; page >= targetPage; page--)
        {
            offset -= Math.Max(0, getPageHeight(page));
        }
        return offset;
    }
}
