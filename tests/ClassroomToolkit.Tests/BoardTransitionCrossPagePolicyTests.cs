using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class BoardTransitionCrossPagePolicyTests
{
    [Fact]
    public void ShouldHandleCrossPageArtifacts_ShouldReturnTrue_WhenPhotoModeAndCrossPageEnabled()
    {
        BoardTransitionCrossPagePolicy.ShouldHandleCrossPageArtifacts(
            photoModeActive: true,
            crossPageDisplayEnabled: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldHandleCrossPageArtifacts_ShouldReturnFalse_WhenPhotoModeDisabled()
    {
        BoardTransitionCrossPagePolicy.ShouldHandleCrossPageArtifacts(
            photoModeActive: false,
            crossPageDisplayEnabled: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldHandleCrossPageArtifacts_ShouldReturnFalse_WhenCrossPageDisabled()
    {
        BoardTransitionCrossPagePolicy.ShouldHandleCrossPageArtifacts(
            photoModeActive: true,
            crossPageDisplayEnabled: false).Should().BeFalse();
    }
}
