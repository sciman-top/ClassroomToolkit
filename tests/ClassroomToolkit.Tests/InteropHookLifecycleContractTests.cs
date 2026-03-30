using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InteropHookLifecycleContractTests
{
    [Fact]
    public void WpsHook_Stop_ShouldDisableIntercept_BeforeDispatchGenerationBump()
    {
        var source = File.ReadAllText(GetInteropSourcePath("WpsSlideshowNavigationHook.cs"));
        var disableIndex = source.IndexOf("_interceptEnabled = false;", StringComparison.Ordinal);
        var generationIndex = source.IndexOf("Interlocked.Increment(ref _dispatchGeneration);", StringComparison.Ordinal);

        disableIndex.Should().BeGreaterThan(0);
        generationIndex.Should().BeGreaterThan(0);
        disableIndex.Should().BeLessThan(generationIndex);
    }

    [Fact]
    public void WpsHook_QueueNavigationRequest_ShouldGateByInterceptState()
    {
        var source = File.ReadAllText(GetInteropSourcePath("WpsSlideshowNavigationHook.cs"));

        source.Should().Contain("if (_disposed || !_interceptEnabled)");
        source.Should().Contain("if (_disposed || !_interceptEnabled || generation != Volatile.Read(ref _dispatchGeneration))");
    }

    [Fact]
    public void WpsHook_ShouldRejectRestartAfterDispose_AndClearSubscribers()
    {
        var source = File.ReadAllText(GetInteropSourcePath("WpsSlideshowNavigationHook.cs"));

        source.Should().Contain("if (_disposed)");
        source.Should().Contain("Stop();");
        source.Should().Contain("NavigationRequested = null;");
    }

    [Fact]
    public void KeyboardHook_ShouldUseAcceptEventsGate_InStopAndCallback()
    {
        var source = File.ReadAllText(GetInteropSourcePath("KeyboardHook.cs"));

        source.Should().Contain("private volatile bool _acceptEvents;");
        source.Should().Contain("private volatile bool _disposed;");
        source.Should().Contain("_acceptEvents = true;");
        source.Should().Contain("_acceptEvents = false;");
        source.Should().Contain("if (_disposed || !_acceptEvents || nCode < 0 || lParam == IntPtr.Zero)");
    }

    [Fact]
    public void KeyboardHook_Stop_ShouldClearSubscribersAndBindingTarget()
    {
        var source = File.ReadAllText(GetInteropSourcePath("KeyboardHook.cs"));

        source.Should().Contain("BindingTriggered = null;");
        source.Should().Contain("TargetBinding = null;");
        source.Should().Contain("if (_hookId == IntPtr.Zero)");
        source.Should().Contain("LastError = 0;");
        source.Should().Contain("if (!UnhookWindowsHookEx(_hookId))");
        source.Should().Contain("LastError = Marshal.GetLastWin32Error();");
        source.Should().Contain("[KeyboardHook] Unhook failed with error=");
    }

    [Fact]
    public void WpsHook_Stop_ShouldRecordUnhookFailures_ForKeyboardAndMouse()
    {
        var source = File.ReadAllText(GetInteropSourcePath("WpsSlideshowNavigationHook.cs"));

        source.Should().Contain("if (!UnhookWindowsHookEx(_keyboardHook))");
        source.Should().Contain("if (!UnhookWindowsHookEx(_mouseHook))");
        source.Should().Contain("[WpsNavHook] Keyboard unhook failed with error=");
        source.Should().Contain("[WpsNavHook] Mouse unhook failed with error=");
        source.Should().Contain("LastError = unhookFailed ? lastUnhookError : 0;");
    }

    private static string GetInteropSourcePath(string fileName)
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.Interop",
            "Presentation",
            fileName);
    }
}
