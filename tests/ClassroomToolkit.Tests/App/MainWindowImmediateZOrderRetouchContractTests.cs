using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class MainWindowImmediateZOrderRetouchContractTests
{
    [Fact]
    public void MainWindow_ShouldExposeImmediateFloatingRetouchEntry()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("internal void RequestImmediateFloatingZOrderRetouch()");
        source.Should().Contain("RequestApplyZOrderPolicy(forceEnforceZOrder: true);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.ZOrder.cs");
    }
}

