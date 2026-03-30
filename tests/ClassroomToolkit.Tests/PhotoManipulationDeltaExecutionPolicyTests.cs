using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoManipulationDeltaExecutionPolicyTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0.05, 0.05, false)]
    [InlineData(0.2, 0.0, true)]
    [InlineData(0.0, -0.2, true)]
    public void Resolve_ShouldDetermineTranslationExecution(
        double dx,
        double dy,
        bool expectedShouldApplyTranslation)
    {
        var plan = PhotoManipulationDeltaExecutionPolicy.Resolve(
            new Vector(dx, dy),
            translationEpsilonDip: 0.1,
            crossPageDisplayActive: false);

        plan.ShouldApplyTranslation.Should().Be(expectedShouldApplyTranslation);
        plan.ShouldLogPanTelemetry.Should().Be(expectedShouldApplyTranslation);
        plan.ShouldRequestCrossPageUpdate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRequestCrossPageUpdate_WhenCrossPageDisplayActive()
    {
        var plan = PhotoManipulationDeltaExecutionPolicy.Resolve(
            new Vector(0, 0),
            translationEpsilonDip: 0.1,
            crossPageDisplayActive: true);

        plan.ShouldRequestCrossPageUpdate.Should().BeTrue();
    }
}
