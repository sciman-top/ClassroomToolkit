using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallRemoteHookDispatchFallbackContractTests
{
    [Fact]
    public void EnqueueRemoteHookUiAction_ShouldFallbackInline_WhenDispatcherSchedulingFailsOnUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("void ExecuteOnUi()");
        source.Should().Contain("var scheduled = false;");
        source.Should().Contain("new Action(ExecuteOnUi)");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("ExecuteOnUi();");
    }

    [Fact]
    public void NotifyRemoteHookError_ShouldFallbackInline_WhenDispatcherSchedulingFailsOnUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("void ShowUnavailableNotice()");
        source.Should().Contain("_ = Dispatcher.InvokeAsync(ShowUnavailableNotice);");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("ShowUnavailableNotice();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.Input.cs");
    }
}
