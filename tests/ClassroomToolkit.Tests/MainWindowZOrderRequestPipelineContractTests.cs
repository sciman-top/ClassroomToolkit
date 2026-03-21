using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowZOrderRequestPipelineContractTests
{
    [Fact]
    public void RequestApplyZOrderPolicy_ShouldUseAdmissionAndRequestStateUpdater()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var admission = ZOrderRequestAdmissionPolicy.Resolve(");
        source.Should().Contain("ZOrderRequestStateUpdater.Apply(");
        source.Should().Contain("if (!admission.ShouldQueue)");
    }

    [Fact]
    public void RequestApplyZOrderPolicy_ShouldRouteQueueingThroughFloatingDispatchUpdater()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("FloatingDispatchQueueStateUpdater.ApplyRequest(");
        source.Should().Contain("() => TryBeginInvoke(ExecuteQueuedApplyZOrderPolicy, DispatcherPriority.Background, \"ExecuteQueuedApplyZOrderPolicy\")");
    }

    [Fact]
    public void RequestApplyZOrderPolicy_ShouldRollbackRequestStateWhenQueueDispatchFails()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var queueDispatchFailed = false;");
        source.Should().Contain("ZOrderQueueDispatchFailureRollbackStateUpdater.Apply(");
        source.Should().Contain("if (queueDispatchFailed)");
    }

    [Fact]
    public void ExecuteQueuedApplyZOrderPolicy_ShouldApplyThroughExecuteQueuedUpdater()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var admission = FloatingDispatchExecuteAdmissionPolicy.Resolve(_floatingDispatchQueueState.ApplyQueued);");
        source.Should().Contain("FloatingDispatchQueueStateUpdater.ApplyExecuteQueued(");
        source.Should().Contain("ApplyZOrderPolicy,");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs");
    }
}
