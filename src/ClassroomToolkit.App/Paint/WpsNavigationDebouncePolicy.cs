using System;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct WpsNavigationDebounceState(
    (int Code, IntPtr Target, DateTime Timestamp)? LastEvent,
    DateTime BlockUntilUtc);

internal static class WpsNavigationDebouncePolicy
{
    internal static bool ShouldSuppress(
        int direction,
        IntPtr target,
        DateTime nowUtc,
        WpsNavigationDebounceState state,
        int debounceMs)
    {
        if (target == IntPtr.Zero)
        {
            return false;
        }
        if (state.BlockUntilUtc > nowUtc)
        {
            return true;
        }
        if (!state.LastEvent.HasValue)
        {
            return false;
        }

        var last = state.LastEvent.Value;
        if (last.Code != direction || last.Target != target)
        {
            return false;
        }

        return (nowUtc - last.Timestamp).TotalMilliseconds < debounceMs;
    }

    internal static WpsNavigationDebounceState Remember(
        int direction,
        IntPtr target,
        DateTime nowUtc,
        int debounceMs)
    {
        return new WpsNavigationDebounceState(
            LastEvent: (direction, target, nowUtc),
            BlockUntilUtc: nowUtc.AddMilliseconds(debounceMs));
    }
}
