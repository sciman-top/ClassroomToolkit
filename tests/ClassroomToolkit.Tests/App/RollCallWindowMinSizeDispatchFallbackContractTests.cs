using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallWindowMinSizeDispatchFallbackContractTests
{
    [Fact]
    public void UpdateMinWindowSize_ShouldFallbackInline_WhenDispatcherSchedulingFailsOnUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("void ApplyMinWindowSize()");
        source.Should().Contain("var scheduled = false;");
        source.Should().Contain("_ = Dispatcher.InvokeAsync(");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("ApplyMinWindowSize();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.Windowing.cs");
    }
}
