using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationFocusMonitorActivationPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, false, false, true, true)]
    public void ShouldMonitor_ShouldMatchExpected(
        bool overlayVisible,
        bool allowOffice,
        bool allowWps,
        bool photoFullscreenActive,
        bool expected)
    {
        PresentationFocusMonitorActivationPolicy.ShouldMonitor(
                overlayVisible,
                allowOffice,
                allowWps,
                photoFullscreenActive)
            .Should()
            .Be(expected);
    }
}
