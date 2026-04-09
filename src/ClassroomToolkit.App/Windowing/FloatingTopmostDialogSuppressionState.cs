using System.Threading;

namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostDialogSuppressionState
{
    private static int _suppressionDepth;

    internal static bool IsSuppressed => Volatile.Read(ref _suppressionDepth) > 0;

    internal static IDisposable Enter()
    {
        Interlocked.Increment(ref _suppressionDepth);
        return InteropAdapterScope.Create(() =>
        {
            var depth = Interlocked.Decrement(ref _suppressionDepth);
            if (depth >= 0)
            {
                return;
            }

            Interlocked.Exchange(ref _suppressionDepth, 0);
        });
    }
}
