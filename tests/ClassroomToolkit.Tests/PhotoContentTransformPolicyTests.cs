using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoContentTransformPolicyTests
{
    [Fact]
    public void ShouldApplyPhotoTransform_ShouldReturnFalse_WhenAllConditionsMet()
    {
        PhotoContentTransformPolicy.ShouldApplyPhotoTransform(
            enabledRequested: true,
            photoModeActive: true,
            boardActive: false,
            transformAvailable: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyPhotoTransform_ShouldReturnFalse_WhenBoardIsActive()
    {
        PhotoContentTransformPolicy.ShouldApplyPhotoTransform(
            enabledRequested: true,
            photoModeActive: true,
            boardActive: true,
            transformAvailable: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyPhotoTransform_ShouldReturnFalse_WhenTransformUnavailable()
    {
        PhotoContentTransformPolicy.ShouldApplyPhotoTransform(
            enabledRequested: true,
            photoModeActive: true,
            boardActive: false,
            transformAvailable: false).Should().BeFalse();
    }
}
