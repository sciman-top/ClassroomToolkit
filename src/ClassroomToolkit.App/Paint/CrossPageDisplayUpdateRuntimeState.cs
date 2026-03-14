using System;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageDisplayUpdateRuntimeState(
    bool Pending,
    int Token,
    DateTime PendingSinceUtc)
{
    internal static CrossPageDisplayUpdateRuntimeState Default => new(
        Pending: false,
        Token: 0,
        PendingSinceUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc);
}
