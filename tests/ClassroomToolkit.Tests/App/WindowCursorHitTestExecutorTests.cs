using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public class WindowCursorHitTestExecutorTests
{
    [Fact]
    public void Resolve_ShouldReturnInvalidWindowHandle_WhenHandleIsZero()
    {
        var decision = WindowCursorHitTestExecutor.Resolve(IntPtr.Zero);
        decision.Succeeded.Should().BeFalse();
        decision.IsInside.Should().BeFalse();
        decision.Reason.Should().Be(WindowCursorHitTestExecutionReason.InvalidWindowHandle);
    }

    [Fact]
    public void TryIsCursorInsideWindow_ShouldReturnFalse_WhenHandleIsZero()
    {
        var ok = WindowCursorHitTestExecutor.TryIsCursorInsideWindow(IntPtr.Zero, out var inside);
        ok.Should().BeFalse();
        inside.Should().BeFalse();
    }

    [Fact]
    public void TryIsCursorInsideWindow_ShouldReturnFalse_WhenCursorFails()
    {
        using var _ = WindowCursorHitTestExecutor.PushInteropAdapterForTest(
            new FakeAdapter(cursorOk: false, rectOk: true));

        var ok = WindowCursorHitTestExecutor.TryIsCursorInsideWindow(new IntPtr(1), out var inside);
        ok.Should().BeFalse();
        inside.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldReturnWindowRectUnavailable_WhenRectFails()
    {
        using var _ = WindowCursorHitTestExecutor.PushInteropAdapterForTest(
            new FakeAdapter(cursorOk: true, rectOk: false));

        var decision = WindowCursorHitTestExecutor.Resolve(new IntPtr(3));

        decision.Succeeded.Should().BeFalse();
        decision.Reason.Should().Be(WindowCursorHitTestExecutionReason.WindowRectUnavailable);
    }

    [Fact]
    public void TryIsCursorInsideWindow_ShouldReturnTrueAndInside_WhenDataAvailable()
    {
        using var _ = WindowCursorHitTestExecutor.PushInteropAdapterForTest(
            new FakeAdapter(cursorOk: true, rectOk: true));

        var ok = WindowCursorHitTestExecutor.TryIsCursorInsideWindow(new IntPtr(2), out var inside);
        ok.Should().BeTrue();
        inside.Should().BeTrue();
    }

    private sealed class FakeAdapter : ICursorWindowGeometryInteropAdapter
    {
        private readonly bool _cursorOk;
        private readonly bool _rectOk;

        public FakeAdapter(bool cursorOk, bool rectOk)
        {
            _cursorOk = cursorOk;
            _rectOk = rectOk;
        }

        public bool TryGetCursorPos(out int x, out int y)
        {
            x = 10;
            y = 10;
            return _cursorOk;
        }

        public bool TryGetWindowRect(IntPtr hwnd, out int left, out int top, out int right, out int bottom)
        {
            left = 0;
            top = 0;
            right = 100;
            bottom = 100;
            return _rectOk;
        }
    }
}
