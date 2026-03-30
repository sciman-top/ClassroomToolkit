using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallRemoteHookLifecycleContractTests
{
    [Fact]
    public void RollCallWindowInput_ShouldRearmRemoteHookNotification_WhenHookStartsSuccessfully()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (result.Started)");
        source.Should().Contain("RemoteHookUnavailableNotificationPolicy.Reset(ref _remoteHookUnavailableNotifiedState);");
    }

    [Fact]
    public void RollCallWindowInput_ShouldNotifyRemoteHookError_OnlyForCurrentGeneration()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (isCurrent() && ShouldEnableRemotePresenterHook())");
        source.Should().Contain("NotifyRemoteHookError();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.Input.cs");
    }
}
