using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveSwitchClampDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CrossPageInteractiveSwitchClampDefaults.MinPageIndex.Should().Be(1);
        CrossPageInteractiveSwitchClampDefaults.MinFallbackPageHeight.Should().Be(1.0);
        CrossPageInteractiveSwitchClampDefaults.MinResolvedPageHeight.Should().Be(0.0);
    }
}
