using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayFocusResolverGatePolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ShouldResolvePresentationTarget_ShouldMatchExpected(
        bool presentationAllowed,
        bool navigationAllowsPresentationInput,
        bool expected)
    {
        OverlayFocusResolverGatePolicy.ShouldResolvePresentationTarget(
                presentationAllowed,
                navigationAllowsPresentationInput)
            .Should()
            .Be(expected);
    }
}
