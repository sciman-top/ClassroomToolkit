using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayPointerSourceGatePolicyTests
{
    [Theory]
    [InlineData(true, false, (int)OverlayPointerSourceGateDecision.Consume)]
    [InlineData(false, true, (int)OverlayPointerSourceGateDecision.Ignore)]
    [InlineData(false, false, (int)OverlayPointerSourceGateDecision.Continue)]
    public void Resolve_ShouldMatchExpected(
        bool photoLoading,
        bool ignoreFromPhotoControls,
        int expectedValue)
    {
        var expected = (OverlayPointerSourceGateDecision)expectedValue;
        var decision = OverlayPointerSourceGatePolicy.Resolve(
            photoLoading,
            ignoreFromPhotoControls);

        decision.Should().Be(expected);
    }
}
