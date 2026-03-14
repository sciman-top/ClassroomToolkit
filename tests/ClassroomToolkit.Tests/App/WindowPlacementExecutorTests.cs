using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowPlacementExecutorTests
{
    [Fact]
    public void TryRefreshFrame_ShouldRetryAndSucceed_OnRecoverableFailure()
    {
        var adapter = new FakePlacementAdapter(
            (1, false, 5),
            (2, true, 0));

        using var _ = WindowPlacementExecutor.PushInteropAdapterForTest(adapter);
        var result = WindowPlacementExecutor.TryRefreshFrame(new IntPtr(1));

        result.Should().BeTrue();
        adapter.CallCount.Should().Be(2);
    }

    [Fact]
    public void TryRefreshFrame_ShouldNotRetry_OnInvalidHandleError()
    {
        var adapter = new FakePlacementAdapter((1, false, 1400));

        using var _ = WindowPlacementExecutor.PushInteropAdapterForTest(adapter);
        var result = WindowPlacementExecutor.TryRefreshFrame(new IntPtr(1));

        result.Should().BeFalse();
        adapter.CallCount.Should().Be(1);
    }

    [Fact]
    public void TryApplyBoundsNoActivateNoZOrder_ShouldIncludeShowWindowFlag_WhenRequested()
    {
        var adapter = new FakePlacementAdapter((1, true, 0));

        using var _ = WindowPlacementExecutor.PushInteropAdapterForTest(adapter);
        var result = WindowPlacementExecutor.TryApplyBoundsNoActivateNoZOrder(
            new IntPtr(1),
            x: 10,
            y: 20,
            width: 800,
            height: 600,
            showWindow: true);

        result.Should().BeTrue();
        (adapter.LastFlags & WindowPlacementBitMasks.SwpShowWindow).Should().NotBe(0);
    }

    private sealed class FakePlacementAdapter : IWindowPlacementInteropAdapter
    {
        private readonly (int Seq, bool Success, int Error)[] _steps;
        public int CallCount { get; private set; }
        public uint LastFlags { get; private set; }

        public FakePlacementAdapter(params (int Seq, bool Success, int Error)[] steps)
        {
            _steps = steps;
        }

        public bool TrySetWindowPos(
            IntPtr hwnd,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags,
            out int errorCode)
        {
            CallCount++;
            LastFlags = flags;

            var step = _steps[Math.Min(CallCount - 1, _steps.Length - 1)];
            errorCode = step.Error;
            return step.Success;
        }
    }
}
