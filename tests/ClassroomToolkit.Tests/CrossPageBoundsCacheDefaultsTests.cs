using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageBoundsCacheDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CrossPageBoundsCacheDefaults.InteractiveReuseMaxAgeMs.Should().Be(120);
        CrossPageBoundsCacheDefaults.KeyEpsilon.Should().Be(0.01);
    }
}
