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
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.Input.cs");
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClassroomToolkit.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
