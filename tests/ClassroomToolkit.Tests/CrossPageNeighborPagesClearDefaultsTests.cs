using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborPagesClearDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CrossPageNeighborPagesClearDefaults.MinGraceMs.Should().Be(0);
    }
}
