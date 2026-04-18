using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

[Collection(SharedWindowDragStateCollection.Name)]
public sealed class WindowTopmostExecutorTests
{
    [Fact]
    public void TryApplyHandleNoActivate_ShouldRetryAndSucceed_OnRecoverableFailure()
    {
        var adapter = new FakeTopmostAdapter(
            (1, false, 5),
            (2, true, 0));

        using var _ = WindowTopmostExecutor.PushInteropAdapterForTest(adapter);
        var result = WindowTopmostExecutor.TryApplyHandleNoActivate(new IntPtr(1), enabled: true);

        result.Should().BeTrue();
        adapter.CallCount.Should().Be(2);
    }

    [Fact]
    public void TryApplyHandleNoActivate_ShouldNotRetry_OnInvalidHandleError()
    {
        var adapter = new FakeTopmostAdapter((1, false, 1400));

        using var _ = WindowTopmostExecutor.PushInteropAdapterForTest(adapter);
        var result = WindowTopmostExecutor.TryApplyHandleNoActivate(new IntPtr(1), enabled: true);

        result.Should().BeFalse();
        adapter.CallCount.Should().Be(1);
    }

    [Fact]
    public void TryApplyHandleNoActivate_ShouldReturnFalse_WhenHandleIsZero()
    {
        var adapter = new FakeTopmostAdapter((1, true, 0));

        using var _ = WindowTopmostExecutor.PushInteropAdapterForTest(adapter);
        var result = WindowTopmostExecutor.TryApplyHandleNoActivate(IntPtr.Zero, enabled: true);

        result.Should().BeFalse();
        adapter.CallCount.Should().Be(0);
    }

    [Fact]
    public void TryApplyHandleNoActivate_ShouldSkip_WhenDragOperationIsActive()
    {
        var adapter = new FakeTopmostAdapter((1, true, 0));

        using var _ = WindowTopmostExecutor.PushInteropAdapterForTest(adapter);
        using var dragScope = WindowDragOperationState.Begin();
        var result = WindowTopmostExecutor.TryApplyHandleNoActivate(new IntPtr(1), enabled: true);

        result.Should().BeFalse();
        adapter.CallCount.Should().Be(0);
    }

    private sealed class FakeTopmostAdapter : IWindowTopmostInteropAdapter
    {
        private readonly (int Seq, bool Success, int Error)[] _steps;
        public int CallCount { get; private set; }

        public FakeTopmostAdapter(params (int Seq, bool Success, int Error)[] steps)
        {
            _steps = steps;
        }

        public bool TrySetTopmostNoActivate(IntPtr hwnd, bool enabled, out int errorCode)
        {
            CallCount++;
            var step = _steps[Math.Min(CallCount - 1, _steps.Length - 1)];
            errorCode = step.Error;
            return step.Success;
        }
    }
}
