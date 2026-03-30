using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractionActivityPolicyTests
{
    [Fact]
    public void IsActive_ShouldReturnFalse_WhenAllFlagsFalse()
    {
        var active = CrossPageInteractionActivityPolicy.IsActive(
            photoPanning: false,
            crossPageDragging: false,
            inkOperationActive: false);

        active.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ShouldReturnTrue_WhenAnyFlagIsTrue()
    {
        CrossPageInteractionActivityPolicy.IsActive(true, false, false).Should().BeTrue();
        CrossPageInteractionActivityPolicy.IsActive(false, true, false).Should().BeTrue();
        CrossPageInteractionActivityPolicy.IsActive(false, false, true).Should().BeTrue();
    }
}
