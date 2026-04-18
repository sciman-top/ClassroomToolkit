using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RegionSelectionCompletionPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3.9, 20)]
    [InlineData(20, 3.9)]
    public void ResolvePointerRelease_ShouldKeepWaiting_WhenSelectionIsTooSmall(double width, double height)
    {
        var decision = RegionSelectionCompletionPolicy.ResolvePointerRelease(width, height);

        decision.Should().Be(RegionSelectionCompletionDecision.KeepWaiting);
    }

    [Fact]
    public void ResolvePointerRelease_ShouldAccept_WhenSelectionIsLargeEnough()
    {
        var decision = RegionSelectionCompletionPolicy.ResolvePointerRelease(4, 4);

        decision.Should().Be(RegionSelectionCompletionDecision.Accept);
    }
}
