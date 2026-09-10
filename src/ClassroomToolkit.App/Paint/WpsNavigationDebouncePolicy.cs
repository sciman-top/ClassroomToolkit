using System;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct WpsNavigationDebounceState(
    (int Code, IntPtr Target, DateTime Timestamp)? LastEvent);

internal static class WpsNavigationDebouncePolicy
{
    // 仅抑制同方向+同目标的重复导航（滚轮洪泛/多路径重复触发）；
    // 反方向是用户刻意的翻页纠正，不得被全域阻断窗口吞掉。
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
        DateTime nowUtc)
    {
        return new WpsNavigationDebounceState(
            LastEvent: (direction, target, nowUtc));
    }
}
