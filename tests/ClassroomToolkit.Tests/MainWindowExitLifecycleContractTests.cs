using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowExitLifecycleContractTests
{
    [Fact]
    public void RequestExit_ShouldCloseImageManagerWindow_WhenPresent()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("hasImageManagerWindow: _imageManagerWindow != null");
        source.Should().Contain("if (exitPlan.ShouldCloseImageManagerWindow && _imageManagerWindow != null)");
        source.Should().Contain("close-image-manager-window");
        source.Should().Contain("imageManagerWindow.Close");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs");
    }
}
