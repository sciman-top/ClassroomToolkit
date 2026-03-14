using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageRuntimeDefaults
{
    internal const int PostInputRefreshDelayMs = 420;
    internal const int NeighborPagesClearGraceMs = 180;
    internal const int DraggingUpdateMinIntervalMs = 24;
    internal const int UpdateMinIntervalMs = 24;
    internal static readonly DateTime UnsetTimestampUtc = DateTime.MinValue;
}
