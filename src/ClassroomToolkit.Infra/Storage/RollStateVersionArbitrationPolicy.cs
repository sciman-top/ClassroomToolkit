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
        var logSource = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
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
                TryLog(
                    log,
                    $"[{logSource}] prefer cache roll-state by revision authority={authorityRevision.Value} cache={cacheRevision.Value}");
                return cacheStateJson;
            }

            TryLog(
                log,
                $"[{logSource}] prefer authority roll-state by revision authority={authorityRevision.Value} cache={cacheRevision.Value}");
            return authorityStateJson;
        }

        if (authorityUpdatedAtUtc.HasValue && cacheUpdatedAtUtc.HasValue)
        {
            if (cacheUpdatedAtUtc.Value > authorityUpdatedAtUtc.Value)
            {
                TryLog(
                    log,
                    $"[{logSource}] prefer cache roll-state by timestamp authority={authorityUpdatedAtUtc:O} cache={cacheUpdatedAtUtc:O}");
                return cacheStateJson;
            }

            TryLog(
                log,
                $"[{logSource}] prefer authority roll-state by timestamp authority={authorityUpdatedAtUtc:O} cache={cacheUpdatedAtUtc:O}");
            return authorityStateJson;
        }

        if (!string.Equals(authorityStateJson, cacheStateJson, StringComparison.Ordinal))
        {
            TryLog(
                log,
                $"[{logSource}] roll-state conflict without complete timestamp metadata; fallback to authority state.");
        }

        return authorityStateJson;
    }

    private static void TryLog(Action<string>? log, string message)
    {
        if (log == null)
        {
            return;
        }

        try
        {
            log(message);
        }
        catch (Exception ex) when (InfraExceptionFilterPolicy.IsNonFatal(ex))
        {
        }
    }
}
