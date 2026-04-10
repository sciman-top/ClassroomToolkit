using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowStartupWarmupDispatchContractTests
{
    [Fact]
    public void OnLoaded_ShouldQueueWarmups_WithDeferredDispatcherPriorities()
    {
        var source = MainWindowContractSourceReader.ReadCombinedSource();

        source.Should().Contain("ScheduleStartupWarmups();");
        source.Should().Contain("operation: \"warmup-rollcall-data\"");
        source.Should().Contain("priority: DispatcherPriority.ContextIdle");
        source.Should().Contain("operation: \"schedule-ink-cleanup\"");
        source.Should().Contain("priority: DispatcherPriority.Background");
        source.Should().Contain("var scheduled = TryBeginInvoke(");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
    }
}
