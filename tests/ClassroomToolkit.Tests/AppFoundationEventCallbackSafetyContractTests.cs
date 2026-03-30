using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AppFoundationEventCallbackSafetyContractTests
{
    [Fact]
    public void RelayCommandRaiseCanExecuteChanged_ShouldIsolateSubscriberFailure()
    {
        var source = File.ReadAllText(GetSourcePath("Commands", "RelayCommand.cs"));

        source.Should().Contain("CanExecuteChanged?.GetInvocationList()");
        source.Should().Contain("RelayCommand: CanExecuteChanged callback failed");
    }

    [Fact]
    public void ViewModelPropertyChanged_ShouldIsolateSubscriberFailure()
    {
        var baseSource = File.ReadAllText(GetSourcePath("ViewModels", "ViewModelBase.cs"));
        var groupItemSource = File.ReadAllText(GetSourcePath("Models", "GroupButtonItem.cs"));

        baseSource.Should().Contain("PropertyChanged?.GetInvocationList()");
        baseSource.Should().Contain("ViewModelBase: PropertyChanged callback failed");
        groupItemSource.Should().Contain("PropertyChanged?.GetInvocationList()");
        groupItemSource.Should().Contain("GroupButtonItem: PropertyChanged callback failed");
    }

    [Fact]
    public void SessionEffectRunnerTransitionCallback_ShouldIsolateSubscriberFailure()
    {
        var source = File.ReadAllText(GetSourcePath("Session", "PaintOverlaySessionEffectRunner.cs"));

        source.Should().Contain("PaintOverlaySessionEffectRunner: transition callback failed");
        source.Should().Contain("catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))");
    }

    private static string GetSourcePath(string folder, string file)
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            folder,
            file);
    }
}
