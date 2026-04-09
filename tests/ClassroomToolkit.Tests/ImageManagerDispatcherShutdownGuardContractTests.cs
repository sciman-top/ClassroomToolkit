using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerDispatcherShutdownGuardContractTests
{
    [Fact]
    public void AsyncBatchLoops_ShouldGuardDispatcherShutdown_BeforeYieldDispatch()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow*.cs");

        source.Should().Contain("if (item.Dispatcher.HasShutdownStarted || item.Dispatcher.HasShutdownFinished)");
        source.Should().Contain("if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished || _isClosing)");
        source.Should().Contain("await item.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);");
        source.Should().Contain("await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);");
    }
}
