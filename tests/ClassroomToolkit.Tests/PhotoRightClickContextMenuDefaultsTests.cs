using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoRightClickContextMenuDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PhotoRightClickContextMenuDefaults.MinThresholdDip.Should().Be(0.0);
        PhotoRightClickContextMenuDefaults.CancelMoveThresholdDip.Should().Be(6.0);
    }
}
