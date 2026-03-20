using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallSpeechDispatchFallbackContractTests
{
    [Fact]
    public void NotifySpeechError_ShouldFallbackInline_WhenDispatcherSchedulingFailsOnUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("void ShowSpeechUnavailableNotice()");
        source.Should().Contain("_ = Dispatcher.InvokeAsync(ShowSpeechUnavailableNotice);");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
        source.Should().Contain("ShowSpeechUnavailableNotice();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.State.cs");
    }
}
