using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BorderFixHelperDeferredDispatchFallbackContractTests
{
    [Fact]
    public void OnWindowLoaded_ShouldKeepFallbackRepair_WhenDeferredDispatchFails()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("void ApplyDeferredBorderFix()");
        source.Should().Contain("BorderFixHelper 延迟调度失败");
        source.Should().Contain("if (window.Dispatcher.CheckAccess())");
        source.Should().Contain("FixAllBorders(window);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Helpers",
            "BorderFixHelper.cs");
    }
}
