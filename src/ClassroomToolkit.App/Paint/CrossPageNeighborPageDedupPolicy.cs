namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborPageDedupPolicy
{
    internal static List<(int PageIndex, double Top)> Resolve(
        List<(int PageIndex, double Top)> neighborPages)
    {
        if (neighborPages.Count <= 1)
        {
            return neighborPages;
        }

        var result = new List<(int PageIndex, double Top)>(neighborPages.Count);
        var seen = new HashSet<int>();
        foreach (var item in neighborPages)
        {
            if (!seen.Add(item.PageIndex))
            {
                continue;
            }
            result.Add(item);
        }
        return result;
    }
}

