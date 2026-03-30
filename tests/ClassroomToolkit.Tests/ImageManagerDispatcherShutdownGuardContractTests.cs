using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerDispatcherShutdownGuardContractTests
{
    [Fact]
    public void AsyncBatchLoops_ShouldGuardDispatcherShutdown_BeforeYieldDispatch()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (item.Dispatcher.HasShutdownStarted || item.Dispatcher.HasShutdownFinished)");
        source.Should().Contain("if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished || _isClosing)");
        source.Should().Contain("await item.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);");
        source.Should().Contain("await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow.xaml.cs");
    }
}
