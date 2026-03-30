using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusBatchTimingDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        StylusBatchTimingDefaults.FallbackHzWhenEmpty.Should().Be(240);
        StylusBatchTimingDefaults.MinPerSampleHz.Should().Be(480);
        StylusBatchTimingDefaults.MaxPerSampleHz.Should().Be(45);
        StylusBatchTimingDefaults.FallbackSpanHz.Should().Be(120);
    }
}
