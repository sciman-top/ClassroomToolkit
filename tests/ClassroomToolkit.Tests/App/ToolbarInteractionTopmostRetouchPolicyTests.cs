using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public class ToolbarInteractionTopmostRetouchPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    public void Resolve_MatchesExpectedMatrix(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive,
        bool expected)
    {
        var decision = ToolbarInteractionTopmostRetouchPolicy.Resolve(
            overlayVisible,
            photoModeActive,
            whiteboardActive);

        decision.ShouldRetouch.Should().Be(expected);
    }

    [Fact]
    public void ShouldRetouch_ShouldMapResolveDecision()
    {
        var actual = ToolbarInteractionTopmostRetouchPolicy.ShouldRetouch(
            overlayVisible: true,
            photoModeActive: true,
            whiteboardActive: false);

        actual.Should().BeTrue();
    }
}
