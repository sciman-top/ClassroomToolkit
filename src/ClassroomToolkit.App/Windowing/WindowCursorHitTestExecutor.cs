using System;

namespace ClassroomToolkit.App.Windowing;

internal enum WindowCursorHitTestExecutionReason
{
    None = 0,
    InvalidWindowHandle = 1,
    CursorUnavailable = 2,
    WindowRectUnavailable = 3,
    HitTestCompleted = 4
}

internal readonly record struct WindowCursorHitTestExecutionDecision(
    bool Succeeded,
    bool IsInside,
    WindowCursorHitTestExecutionReason Reason);

internal static class WindowCursorHitTestExecutor
{
    private static ICursorWindowGeometryInteropAdapter _interopAdapter = new NativeCursorWindowGeometryInteropAdapter();

    internal static IDisposable PushInteropAdapterForTest(ICursorWindowGeometryInteropAdapter adapter)
    {
        var previous = _interopAdapter;
        _interopAdapter = adapter;
        return InteropAdapterScope.Create(() => _interopAdapter = previous);
    }

    internal static bool TryIsCursorInsideWindow(IntPtr hwnd, out bool inside)
    {
        var decision = Resolve(hwnd);
        inside = decision.IsInside;
        return decision.Succeeded;
    }

    internal static WindowCursorHitTestExecutionDecision Resolve(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return new WindowCursorHitTestExecutionDecision(
                Succeeded: false,
                IsInside: false,
                Reason: WindowCursorHitTestExecutionReason.InvalidWindowHandle);
        }

        if (!_interopAdapter.TryGetCursorPos(out var x, out var y))
        {
            return new WindowCursorHitTestExecutionDecision(
                Succeeded: false,
                IsInside: false,
                Reason: WindowCursorHitTestExecutionReason.CursorUnavailable);
        }

        if (!_interopAdapter.TryGetWindowRect(hwnd, out var left, out var top, out var right, out var bottom))
        {
            return new WindowCursorHitTestExecutionDecision(
                Succeeded: false,
                IsInside: false,
                Reason: WindowCursorHitTestExecutionReason.WindowRectUnavailable);
        }

        var hitTestDecision = WindowCursorHitTestPolicy.Resolve(x, y, left, top, right, bottom);
        return new WindowCursorHitTestExecutionDecision(
            Succeeded: true,
            IsInside: hitTestDecision.IsInside,
            Reason: WindowCursorHitTestExecutionReason.HitTestCompleted);
    }
}
