using System;
using System.Threading;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowDragOperationState
{
    private static int _activeDragCount;

    internal static bool IsActive => Volatile.Read(ref _activeDragCount) > 0;

    internal static IDisposable Begin()
    {
        Interlocked.Increment(ref _activeDragCount);
        return InteropAdapterScope.Create(End);
    }

    private static void End()
    {
        var next = Interlocked.Decrement(ref _activeDragCount);
        if (next < 0)
        {
            Interlocked.Exchange(ref _activeDragCount, 0);
        }
    }
}
