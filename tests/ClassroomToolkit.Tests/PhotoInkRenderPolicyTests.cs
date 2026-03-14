using System.Windows.Media;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInkRenderPolicyTests
{
    [Fact]
    public void ShouldRequestImmediateRedraw_ShouldReturnFalse_WhenPhotoModeUsesPhotoTransform()
    {
        var photoTransform = new TransformGroup();
        var rasterTransform = photoTransform;

        var result = PhotoInkRenderPolicy.ShouldRequestImmediateRedraw(
            photoModeActive: true,
            rasterRenderTransform: rasterTransform,
            photoContentTransform: photoTransform);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequestImmediateRedraw_ShouldReturnFalse_WhenNotPhotoMode()
    {
        var photoTransform = new TransformGroup();
        var rasterTransform = photoTransform;

        var result = PhotoInkRenderPolicy.ShouldRequestImmediateRedraw(
            photoModeActive: false,
            rasterRenderTransform: rasterTransform,
            photoContentTransform: photoTransform);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequestImmediateRedraw_ShouldReturnFalse_WhenRasterNotBoundToPhotoTransform()
    {
        var photoTransform = new TransformGroup();
        var rasterTransform = Transform.Identity;

        var result = PhotoInkRenderPolicy.ShouldRequestImmediateRedraw(
            photoModeActive: true,
            rasterRenderTransform: rasterTransform,
            photoContentTransform: photoTransform);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRenderInteractiveInkInPhotoSpace_ShouldReturnFalse_WhenPhotoModeUsesPhotoTransform()
    {
        var photoTransform = new TransformGroup();
        var rasterTransform = photoTransform;

        var result = PhotoInkRenderPolicy.ShouldRenderInteractiveInkInPhotoSpace(
            photoModeActive: true,
            rasterRenderTransform: rasterTransform,
            photoContentTransform: photoTransform);

        result.Should().BeFalse();
    }
}
