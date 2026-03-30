using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanRefreshGatePolicyTests
{
    [Fact]
    public void ShouldRefresh_ShouldReturnFalse_WhenNoMovement()
    {
        PhotoPanRefreshGatePolicy.ShouldRefresh(
            beforeTranslateX: 120,
            beforeTranslateY: 300,
            afterTranslateX: 120,
            afterTranslateY: 300).Should().BeFalse();
    }

    [Fact]
    public void ShouldRefresh_ShouldReturnFalse_WhenMovementWithinEpsilon()
    {
        var epsilon = CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip;
        PhotoPanRefreshGatePolicy.ShouldRefresh(
            beforeTranslateX: 100,
            beforeTranslateY: 200,
            afterTranslateX: 100 + (epsilon * 0.5),
            afterTranslateY: 200,
            epsilonDip: epsilon).Should().BeFalse();
    }

    [Fact]
    public void ShouldRefresh_ShouldReturnTrue_WhenXMovementExceedsEpsilon()
    {
        var epsilon = CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip;
        PhotoPanRefreshGatePolicy.ShouldRefresh(
            beforeTranslateX: 100,
            beforeTranslateY: 200,
            afterTranslateX: 100 + (epsilon * 2),
            afterTranslateY: 200,
            epsilonDip: epsilon).Should().BeTrue();
    }

    [Fact]
    public void ShouldRefresh_ShouldReturnTrue_WhenYMovementExceedsEpsilon()
    {
        var epsilon = CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip;
        PhotoPanRefreshGatePolicy.ShouldRefresh(
            beforeTranslateX: 100,
            beforeTranslateY: 200,
            afterTranslateX: 100,
            afterTranslateY: 200 - (epsilon * 2),
            epsilonDip: epsilon).Should().BeTrue();
    }
}
