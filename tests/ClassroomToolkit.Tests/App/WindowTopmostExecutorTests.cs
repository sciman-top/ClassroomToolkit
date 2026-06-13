using System;
using System.IO;
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

    [Fact]
    public void TryApplyHandleBehindNoActivate_ShouldUseInsertAfterHandle()
    {
        var adapter = new FakeTopmostAdapter((1, true, 0));

        using var _ = WindowTopmostExecutor.PushInteropAdapterForTest(adapter);
        var result = WindowTopmostExecutor.TryApplyHandleBehindNoActivate(new IntPtr(1), new IntPtr(2));

        result.Should().BeTrue();
        adapter.CallCount.Should().Be(1);
        adapter.LastInsertAfter.Should().Be(new IntPtr(2));
    }

    [Fact]
    public void PrepareNoActivateBehind_ShouldPrepareHiddenHandleWithoutTopmostFallback()
    {
        var source = File.ReadAllText(ClassroomToolkit.Tests.TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Windowing",
            "WindowTopmostExecutor.cs"));

        source.Should().Contain("internal static void PrepareNoActivateBehind(Window? window, Window? insertAfterWindow)");
        source.Should().Contain("ResolveWindowHandle(window, ensureHiddenHandle: !requireVisible)");
        source.Should().Contain("allowFallbackTopmost: false");
        source.Should().Contain("hwnd = helper.EnsureHandle();");
    }

    private sealed class FakeTopmostAdapter : IWindowTopmostInteropAdapter
    {
        private readonly (int Seq, bool Success, int Error)[] _steps;
        public int CallCount { get; private set; }
        public IntPtr LastInsertAfter { get; private set; }

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

        public bool TrySetWindowBehindNoActivate(IntPtr hwnd, IntPtr insertAfter, out int errorCode)
        {
            CallCount++;
            LastInsertAfter = insertAfter;
            var step = _steps[Math.Min(CallCount - 1, _steps.Length - 1)];
            errorCode = step.Error;
            return step.Success;
        }
    }
}
