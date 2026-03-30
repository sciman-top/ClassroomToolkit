using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LauncherBubbleRestoreCallbackSafetyContractTests
{
    [Fact]
    public void MouseUpRestoreRequested_ShouldBeWrappedByNonFatalGuard()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("TryExecuteNonFatal(() => RestoreRequested?.Invoke());");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "LauncherBubbleWindow.xaml.cs");
    }
}
