using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInteractionModePolicyTests
{
    [Fact]
    public void IsPhotoNavigationEnabled_ShouldMatchPhotoModeAndBoardState()
    {
        PhotoInteractionModePolicy.IsPhotoNavigationEnabled(
            photoModeActive: true,
            boardActive: false).Should().BeTrue();
        PhotoInteractionModePolicy.IsPhotoNavigationEnabled(
            photoModeActive: true,
            boardActive: true).Should().BeFalse();
        PhotoInteractionModePolicy.IsPhotoNavigationEnabled(
            photoModeActive: false,
            boardActive: false).Should().BeFalse();
    }

    [Fact]
    public void IsPhotoTransformEnabled_ShouldMatchPhotoModeAndBoardState()
    {
        PhotoInteractionModePolicy.IsPhotoTransformEnabled(
            photoModeActive: true,
            boardActive: false).Should().BeTrue();
        PhotoInteractionModePolicy.IsPhotoTransformEnabled(
            photoModeActive: true,
            boardActive: true).Should().BeFalse();
        PhotoInteractionModePolicy.IsPhotoTransformEnabled(
            photoModeActive: false,
            boardActive: false).Should().BeFalse();
    }

    [Fact]
    public void IsPhotoOrBoardActive_ShouldReturnTrue_WhenEitherIsActive()
    {
        PhotoInteractionModePolicy.IsPhotoOrBoardActive(
            photoModeActive: true,
            boardActive: false).Should().BeTrue();
        PhotoInteractionModePolicy.IsPhotoOrBoardActive(
            photoModeActive: false,
            boardActive: true).Should().BeTrue();
        PhotoInteractionModePolicy.IsPhotoOrBoardActive(
            photoModeActive: false,
            boardActive: false).Should().BeFalse();
    }

    [Fact]
    public void IsCrossPageDisplayActive_ShouldRequireCrossPageAndPhotoTransformEnabled()
    {
        PhotoInteractionModePolicy.IsCrossPageDisplayActive(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true).Should().BeTrue();

        PhotoInteractionModePolicy.IsCrossPageDisplayActive(
            photoModeActive: true,
            boardActive: true,
            crossPageDisplayEnabled: true).Should().BeFalse();

        PhotoInteractionModePolicy.IsCrossPageDisplayActive(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: false).Should().BeFalse();
    }
}
