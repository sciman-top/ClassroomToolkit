using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoOverlayEventCallbackSafetyContractTests
{
    [Fact]
    public void PhotoClosedCallback_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("PhotoClosed?.Invoke(studentId)");
        source.Should().Contain("photo closed callback failed");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoOverlayWindow.xaml.cs");
    }
}
