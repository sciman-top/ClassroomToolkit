using System.Collections.Generic;
using System.Linq;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageRegionEraseOrderPolicy
{
    internal static IReadOnlyList<int> ResolveBatchOrder(
        IEnumerable<int> pages,
        int currentPage)
    {
        if (pages == null)
        {
            return currentPage > 0 ? new[] { currentPage } : [];
        }

        var ordered = pages
            .Where(p => p > 0)
            .Distinct()
            .OrderBy(p => p)
            .ToList();
        if (currentPage <= 0)
        {
            return ordered;
        }

        ordered.Remove(currentPage);
        ordered.Add(currentPage);
        return ordered;
    }
}
