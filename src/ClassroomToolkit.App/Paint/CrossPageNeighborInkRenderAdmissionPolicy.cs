namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborInkRenderAdmissionPolicy
{
    internal static bool ShouldRejectStaleCacheKey(string cacheKey, string expectedCacheKey)
    {
        if (string.IsNullOrWhiteSpace(expectedCacheKey))
        {
            return true;
        }

        return !string.Equals(cacheKey, expectedCacheKey, System.StringComparison.Ordinal);
    }
}
