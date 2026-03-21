using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowToolbarRetouchDispatchContractTests
{
    [Fact]
    public void ToolbarRetouch_DirectRepair_ShouldRouteThroughExecutionCoordinator()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var executionOutcome = ToolbarInteractionDirectRepairExecutionCoordinator.Apply(");
        source.Should().Contain("() => _toolbarDirectRepairBackgroundQueued");
        source.Should().Contain("() => ToolbarInteractionDirectRepairDispatchStateUpdater.TryMarkQueued(ref _toolbarDirectRepairBackgroundQueued)");
        source.Should().Contain("() => ApplyToolbarDirectRepair(trigger, launcherWindow)");
    }

    [Fact]
    public void ToolbarRetouch_DirectRepair_ShouldUseBackgroundDispatcherAndExplicitFailureBranches()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("action => TryBeginInvoke(");
        source.Should().Contain("System.Windows.Threading.DispatcherPriority.Background");
        source.Should().Contain("ToolbarInteractionDirectRepairExecutionOutcome.BackgroundScheduleFailed");
        source.Should().Contain("ToolbarInteractionDirectRepairExecutionOutcome.BackgroundDispatchRejected");
        source.Should().Contain("ToolbarInteractionDirectRepairExecutionOutcome.BackgroundMarkQueuedFailed");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Paint.cs");
    }
}
