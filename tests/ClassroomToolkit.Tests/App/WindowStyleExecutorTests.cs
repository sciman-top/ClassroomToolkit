using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowStyleExecutorTests
{
    [Fact]
    public void TryUpdateStyleBits_ShouldReturnTrue_WhenNoChangeNeeded()
    {
        var adapter = new FakeStyleAdapter(
            getSteps: new[] { (Success: true, Style: 0b0010, Error: 0) },
            setSteps: Array.Empty<(bool Success, int Error)>());

        using var scope = WindowStyleExecutor.PushInteropAdapterForTest(adapter);
        var result = WindowStyleExecutor.TryUpdateStyleBits(
            new IntPtr(1),
            index: -20,
            setMask: 0b0010,
            clearMask: 0,
            out var style);

        result.Should().BeTrue();
        style.Should().Be(0b0010);
        adapter.SetCallCount.Should().Be(0);
    }

    [Fact]
    public void TryUpdateStyleBits_ShouldRetrySetAndSucceed_OnRecoverableError()
    {
        var adapter = new FakeStyleAdapter(
            getSteps: new[] { (Success: true, Style: 0b0001, Error: 0) },
            setSteps: new[] { (Success: false, Error: 5), (Success: true, Error: 0) });

        using var scope = WindowStyleExecutor.PushInteropAdapterForTest(adapter);
        var result = WindowStyleExecutor.TryUpdateStyleBits(
            new IntPtr(1),
            index: -20,
            setMask: 0b0010,
            clearMask: 0,
            out var style);

        result.Should().BeTrue();
        style.Should().Be(0b0011);
        adapter.SetCallCount.Should().Be(2);
    }

    [Fact]
    public void TryUpdateStyleBits_ShouldReturnFalse_WhenGetFailsWithInvalidHandle()
    {
        var adapter = new FakeStyleAdapter(
            getSteps: new[] { (Success: false, Style: 0, Error: 1400) },
            setSteps: Array.Empty<(bool Success, int Error)>());

        using var scope = WindowStyleExecutor.PushInteropAdapterForTest(adapter);
        var result = WindowStyleExecutor.TryUpdateStyleBits(
            new IntPtr(1),
            index: -20,
            setMask: 0b0010,
            clearMask: 0,
            out _);

        result.Should().BeFalse();
        adapter.SetCallCount.Should().Be(0);
    }

    [Fact]
    public void TryUpdateExtendedStyleBits_ShouldUseExtendedStyleIndex()
    {
        var adapter = new FakeStyleAdapter(
            getSteps: new[] { (Success: true, Style: 0b0000, Error: 0) },
            setSteps: new[] { (Success: true, Error: 0) });

        using var scope = WindowStyleExecutor.PushInteropAdapterForTest(adapter);
        var result = WindowStyleExecutor.TryUpdateExtendedStyleBits(
            new IntPtr(1),
            setMask: 0b0010,
            clearMask: 0,
            out var style);

        result.Should().BeTrue();
        style.Should().Be(0b0010);
        adapter.LastGetIndex.Should().Be(-20);
        adapter.LastSetIndex.Should().Be(-20);
    }

    private sealed class FakeStyleAdapter : IWindowStyleInteropAdapter
    {
        private readonly (bool Success, int Style, int Error)[] _getSteps;
        private readonly (bool Success, int Error)[] _setSteps;
        private int _getCursor;
        private int _setCursor;

        public int SetCallCount { get; private set; }
        public int LastGetIndex { get; private set; }
        public int LastSetIndex { get; private set; }

        public FakeStyleAdapter(
            (bool Success, int Style, int Error)[] getSteps,
            (bool Success, int Error)[] setSteps)
        {
            _getSteps = getSteps;
            _setSteps = setSteps;
        }

        public bool TryGetWindowLong(IntPtr hwnd, int index, out int style, out int errorCode)
        {
            LastGetIndex = index;
            var step = _getSteps[Math.Min(_getCursor, _getSteps.Length - 1)];
            _getCursor++;
            style = step.Style;
            errorCode = step.Error;
            return step.Success;
        }

        public bool TrySetWindowLong(IntPtr hwnd, int index, int value, out int errorCode)
        {
            SetCallCount++;
            LastSetIndex = index;
            var step = _setSteps[Math.Min(_setCursor, _setSteps.Length - 1)];
            _setCursor++;
            errorCode = step.Error;
            return step.Success;
        }
    }
}
