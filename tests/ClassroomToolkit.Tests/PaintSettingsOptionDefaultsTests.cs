using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PaintSettingsOptionDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PaintSettingsOptionDefaults.InkExportMaxParallelDefault.Should().Be(2);
        PaintSettingsOptionDefaults.PhotoNeighborPrefetchRadiusDefault.Should().Be(4);
    }
}
