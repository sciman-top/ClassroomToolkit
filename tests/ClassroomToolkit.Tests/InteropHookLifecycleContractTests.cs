using FluentAssertions;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Tests;

[Trait("Gate", "CoreContract")]
public sealed class InteropHookLifecycleContractTests
{
    [Fact]
    public void WpsHook_Stop_ShouldInvalidateAlreadyQueuedNavigation()
    {
        Action? pending = null;
        using var hook = new WpsSlideshowNavigationHook((_, action, _) => pending = action);
        var dispatchCount = 0;
        hook.NavigationRequested += (_, _) => dispatchCount++;
        hook.SetInterceptEnabled(true);

        hook.QueueNavigationRequest(1, "test");
        pending.Should().NotBeNull();
        hook.Stop();
        pending!();

        dispatchCount.Should().Be(0);
    }

    [Fact]
    public void WpsHook_QueueNavigationRequest_ShouldRequireInterceptEnabled()
    {
        Action? pending = null;
        using var hook = new WpsSlideshowNavigationHook((_, action, _) => pending = action);

        hook.QueueNavigationRequest(1, "disabled");
        pending.Should().BeNull();

        hook.SetInterceptEnabled(true);
        hook.QueueNavigationRequest(1, "enabled");
        pending.Should().NotBeNull();
    }

    [Fact]
    public async Task WpsHook_ShouldRejectRestartAndInvalidateQueuedNavigation_AfterDispose()
    {
        Action? pending = null;
        var hook = new WpsSlideshowNavigationHook((_, action, _) => pending = action);
        var dispatchCount = 0;
        hook.NavigationRequested += (_, _) => dispatchCount++;
        hook.SetInterceptEnabled(true);
        hook.QueueNavigationRequest(-1, "test");

        hook.Dispose();
        pending.Should().NotBeNull();
        pending!();

        dispatchCount.Should().Be(0);
        (await hook.StartAsync()).Should().BeFalse();
    }

    [Fact]
    public void KeyboardHook_ShouldUseAcceptEventsGate_InStopAndCallback()
    {
        var source = ReadInteropSources("KeyboardHook*.cs");

        source.Should().Contain("private volatile bool _acceptEvents;");
        source.Should().Contain("private volatile bool _disposed;");
        source.Should().Contain("_acceptEvents = true;");
        source.Should().Contain("_acceptEvents = false;");
        source.Should().Contain("if (_disposed || !_acceptEvents || nCode < 0 || lParam == IntPtr.Zero)");
    }

    [Fact]
    public void KeyboardHook_Stop_ShouldClearSubscribersAndBindingTarget()
    {
        var source = ReadInteropSources("KeyboardHook*.cs");

        source.Should().Contain("BindingTriggered = null;");
        source.Should().Contain("TargetBinding = null;");
        source.Should().Contain("if (_hookId == IntPtr.Zero)");
        source.Should().Contain("LastError = 0;");
        source.Should().Contain("if (!UnhookWindowsHookEx(_hookId))");
        source.Should().Contain("LastError = Marshal.GetLastWin32Error();");
        source.Should().Contain("[KeyboardHook] Unhook failed with error=");
    }

    [Fact]
    public void KeyboardHook_ShouldLogStartFailure()
    {
        var source = ReadInteropSources("KeyboardHook*.cs");

        source.Should().Contain("[KeyboardHook] Start failed with error=");
    }

    [Fact]
    public void WpsHook_Stop_ShouldRecordUnhookFailures_ForKeyboardAndMouse()
    {
        var source = ReadInteropSources("WpsSlideshowNavigationHook*.cs");

        source.Should().Contain("if (!UnhookWindowsHookEx(_keyboardHook))");
        source.Should().Contain("if (!UnhookWindowsHookEx(_mouseHook))");
        source.Should().Contain("[WpsNavHook] Keyboard unhook failed with error=");
        source.Should().Contain("[WpsNavHook] Mouse unhook failed with error=");
        source.Should().Contain("LastError = unhookFailed ? lastUnhookError : 0;");
    }

    [Fact]
    public void WpsHook_ShouldLogStartFailure()
    {
        var source = ReadInteropSources("WpsSlideshowNavigationHook*.cs");

        source.Should().Contain("[WpsNavHook] Start failed with error=");
    }

    [Fact]
    public void WpsHook_StartFailure_ShouldPreserveStartLastError_AfterCleanup()
    {
        var source = ReadInteropSources("WpsSlideshowNavigationHook*.cs");

        // LastError 必须在 SetWindowsHookEx 失败的同步点捕获（per-thread），await 后读取会跨线程失真。
        source.Should().Contain("var lastInstallError = 0;");
        source.Should().Contain("lastInstallError = Marshal.GetLastWin32Error();");
        source.Should().Contain("LastError = lastInstallError;");
        source.Should().Contain("Stop();");
    }

    private static string ReadInteropSources(string pattern)
    {
        return ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.Interop",
            "Presentation",
            pattern);
    }
}
