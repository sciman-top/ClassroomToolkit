using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoHorizontalPanRangePolicyTests
{
    [Fact]
    public void Resolve_ShouldAllowEdgePanning_WhenPageNarrowerThanViewport()
    {
        var range = PhotoHorizontalPanRangePolicy.Resolve(
            viewportWidth: 1920,
            scaledWidth: 1200,
            includeSlack: false);

        range.MinX.Should().Be(0);
        range.MaxX.Should().Be(720);
    }

    [Fact]
    public void Resolve_ShouldUseSlack_WhenEnabled()
    {
        var range = PhotoHorizontalPanRangePolicy.Resolve(
            viewportWidth: 1000,
            scaledWidth: 600,
            includeSlack: true);

        range.MinX.Should().Be(-60);
        range.MaxX.Should().Be(460);
    }
}
