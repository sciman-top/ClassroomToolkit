using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkPredictionDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        InkPredictionDefaults.HorizonMinMs.Should().Be(4);
        InkPredictionDefaults.HorizonMaxMs.Should().Be(16);
        InkPredictionDefaults.MaxDistanceDip.Should().Be(10.0);
        InkPredictionDefaults.PrimaryAlphaMin.Should().Be(24);
        InkPredictionDefaults.PrimaryAlphaMax.Should().Be(136);
        InkPredictionDefaults.SecondaryAlphaMin.Should().Be(18);
        InkPredictionDefaults.SecondaryAlphaMax.Should().Be(110);
        InkPredictionDefaults.TipAlphaMin.Should().Be(14);
        InkPredictionDefaults.TipAlphaMax.Should().Be(92);
    }
}
