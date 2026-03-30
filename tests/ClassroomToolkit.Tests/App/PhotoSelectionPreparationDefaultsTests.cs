using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoSelectionPreparationDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        PhotoSelectionPreparationDefaults.PresentationForegroundSuppressionMs.Should().Be(800);
    }
}
