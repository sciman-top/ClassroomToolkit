using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerWindowDispatchFallbackContractTests
{
    [Fact]
    public void DeferredWindowDispatches_ShouldFallbackInline_WhenSchedulingFailsOnUiThread()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow*.cs");

        source.Should().Contain("void ApplyDeferredSplitterUpdate()");
        source.Should().Contain("void ApplyRestoredBounds()");
        source.Should().Contain("var scheduled = false;");
        source.Should().Contain("if (!scheduled && Dispatcher.CheckAccess())");
    }
}
