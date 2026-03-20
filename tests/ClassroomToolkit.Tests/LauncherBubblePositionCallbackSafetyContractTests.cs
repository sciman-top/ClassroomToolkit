using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LauncherBubblePositionCallbackSafetyContractTests
{
    [Fact]
    public void PlaceNear_ShouldIsolatePositionChangedCallbackFailure()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("TryExecuteNonFatal(() => PositionChanged?.Invoke(new System.Windows.Point(Left, Top)));");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "LauncherBubbleWindow.xaml.cs");
    }
}
