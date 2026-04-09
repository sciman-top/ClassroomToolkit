using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerCloseCallbackSafetyContractTests
{
    [Fact]
    public void BeginClose_ShouldIsolateLayoutCallbackFailure()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow*.cs");

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("LeftPanelLayoutChanged?.Invoke(_preferredLeftRatio, _preferredLeftPanelWidth)");
        source.Should().Contain("ImageManager: close layout callback failed");
    }
}
