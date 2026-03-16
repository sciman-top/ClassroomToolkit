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
    public void KeyboardHook_ShouldUseAcceptEventsGate_InStopAndCallback()
    {
        var source = File.ReadAllText(GetInteropSourcePath("KeyboardHook.cs"));

        source.Should().Contain("private volatile bool _acceptEvents;");
        source.Should().Contain("_acceptEvents = true;");
        source.Should().Contain("_acceptEvents = false;");
        source.Should().Contain("if (!_acceptEvents || nCode < 0 || lParam == IntPtr.Zero)");
    }

    private static string GetInteropSourcePath(string fileName)
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.Interop",
            "Presentation",
            fileName);
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClassroomToolkit.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
