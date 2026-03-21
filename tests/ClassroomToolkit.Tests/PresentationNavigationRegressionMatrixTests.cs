using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationNavigationRegressionMatrixTests
{
    [Theory]
    [InlineData(1, "keyboard", false, true, false, true, false, true, 1)]
    [InlineData(-1, "keyboard", false, true, false, true, false, true, -1)]
    [InlineData(1, "wheel", false, true, false, true, false, true, 1)]
    [InlineData(1, "wheel", true, true, false, true, false, false, 0)]
    [InlineData(1, "keyboard", false, false, false, true, false, false, 0)]
    [InlineData(1, "keyboard", false, true, true, false, false, false, 0)]
    [InlineData(1, "keyboard", false, true, false, true, true, false, 0)]
    [InlineData(9, "keyboard", false, true, false, true, false, true, 1)]
    [InlineData(-9, "keyboard", false, true, false, true, false, true, -1)]
    public void HookNavigationMatrix_ShouldMatchExpected(
        int direction,
        string source,
        bool suppressWheelFromRecentInkInput,
        bool targetValid,
        bool passthrough,
        bool interceptSource,
        bool suppressedAsDebounced,
        bool expectedDispatch,
        int expectedDirectionCode)
    {
        var parsed = PresentationNavigationIntentParser.TryParseHook(direction, source, out var intent);

        parsed.Should().BeTrue();
        var result = PresentationNavigationOrchestrator.ResolveHook(
            intent,
            suppressWheelFromRecentInkInput,
            targetValid,
            passthrough,
            interceptSource,
            suppressedAsDebounced);

        result.ShouldDispatch.Should().Be(expectedDispatch);
        result.DirectionCode.Should().Be(expectedDirectionCode);
    }
}
