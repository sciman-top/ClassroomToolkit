using System;

namespace ClassroomToolkit.Infra.Storage;

internal static class RollStateVersionArbitrationPolicy
{
    internal static string? Resolve(
        string? authorityStateJson,
        long? authorityRevision,
        DateTime? authorityUpdatedAtUtc,
        string? cacheStateJson,
        long? cacheRevision,
        DateTime? cacheUpdatedAtUtc,
        Action<string>? log,
        string source)
    {
        var hasAuthorityState = !string.IsNullOrWhiteSpace(authorityStateJson);
        var hasCacheState = !string.IsNullOrWhiteSpace(cacheStateJson);

        if (!hasAuthorityState)
        {
            return hasCacheState ? cacheStateJson : authorityStateJson;
        }

        if (!hasCacheState)
        {
            return authorityStateJson;
        }

        if (authorityRevision.HasValue && cacheRevision.HasValue && authorityRevision.Value != cacheRevision.Value)
        {
            if (cacheRevision.Value > authorityRevision.Value)
            {
                log?.Invoke(
                    $"[{source}] prefer cache roll-state by revision authority={authorityRevision.Value} cache={cacheRevision.Value}");
                return cacheStateJson;
            }

            log?.Invoke(
                $"[{source}] prefer authority roll-state by revision authority={authorityRevision.Value} cache={cacheRevision.Value}");
            return authorityStateJson;
        }

        if (authorityUpdatedAtUtc.HasValue && cacheUpdatedAtUtc.HasValue)
        {
            if (cacheUpdatedAtUtc.Value > authorityUpdatedAtUtc.Value)
            {
                log?.Invoke(
                    $"[{source}] prefer cache roll-state by timestamp authority={authorityUpdatedAtUtc:O} cache={cacheUpdatedAtUtc:O}");
                return cacheStateJson;
            }

            log?.Invoke(
                $"[{source}] prefer authority roll-state by timestamp authority={authorityUpdatedAtUtc:O} cache={cacheUpdatedAtUtc:O}");
            return authorityStateJson;
        }

        if (!string.Equals(authorityStateJson, cacheStateJson, StringComparison.Ordinal))
        {
            log?.Invoke(
                $"[{source}] roll-state conflict without complete timestamp metadata; fallback to authority state.");
        }

        return authorityStateJson;
    }
}
