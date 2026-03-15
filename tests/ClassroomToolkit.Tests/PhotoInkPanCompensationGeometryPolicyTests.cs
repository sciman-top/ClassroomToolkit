using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInkPanCompensationGeometryPolicyTests
{
    [Fact]
    public void ShouldApplyCompensation_ShouldReturnTrue_WhenPhotoInkUsesPanCompensationAndOffsetExists()
    {
        var panCompensation = new TranslateTransform(12.5, -8.0);

        var shouldApply = PhotoInkPanCompensationGeometryPolicy.ShouldApplyCompensation(
            photoInkModeActive: true,
            rasterRenderTransform: panCompensation,
            panCompensation);

        shouldApply.Should().BeTrue();
    }

    [Fact]
    public void ShouldApplyCompensation_ShouldReturnFalse_WhenCompensationOffsetIsZero()
    {
        var panCompensation = new TranslateTransform(0, 0);

        var shouldApply = PhotoInkPanCompensationGeometryPolicy.ShouldApplyCompensation(
            photoInkModeActive: true,
            rasterRenderTransform: panCompensation,
            panCompensation);

        shouldApply.Should().BeFalse();
    }

    [Fact]
    public void AdjustToRasterSpace_ShouldTranslateGeometryByInverseCompensation()
    {
        var geometry = new RectangleGeometry(new Rect(20, 30, 40, 50));

        var adjusted = PhotoInkPanCompensationGeometryPolicy.AdjustToRasterSpace(
            geometry,
            panCompensationX: 6,
            panCompensationY: -4);

        adjusted.Bounds.X.Should().BeApproximately(14, 0.001);
        adjusted.Bounds.Y.Should().BeApproximately(34, 0.001);
        adjusted.Bounds.Width.Should().BeApproximately(40, 0.001);
        adjusted.Bounds.Height.Should().BeApproximately(50, 0.001);
    }
}
