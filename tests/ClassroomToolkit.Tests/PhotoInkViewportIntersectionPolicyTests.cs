using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInkViewportIntersectionPolicyTests
{
    [Fact]
    public void ShouldRender_WhenPhotoTransformMovesStrokeIntoViewport()
    {
        var strokeBounds = new Rect(0, 1500, 120, 80);
        var viewport = new Rect(0, 0, 1920, 1080);
        var matrix = Matrix.Identity;
        matrix.Translate(0, -1200);

        var result = PhotoInkViewportIntersectionPolicy.ShouldRender(
            photoInkModeActive: true,
            usePhotoTransform: true,
            strokeBounds: strokeBounds,
            photoTransformMatrix: matrix,
            viewportBounds: viewport);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRender_WhenPhotoTransformKeepsStrokeOutsideViewport_ShouldReturnFalse()
    {
        var strokeBounds = new Rect(0, 2600, 120, 80);
        var viewport = new Rect(0, 0, 1920, 1080);
        var matrix = Matrix.Identity;
        matrix.Translate(0, -1200);

        var result = PhotoInkViewportIntersectionPolicy.ShouldRender(
            photoInkModeActive: true,
            usePhotoTransform: true,
            strokeBounds: strokeBounds,
            photoTransformMatrix: matrix,
            viewportBounds: viewport);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRender_WhenNotUsingPhotoTransform_ShouldUseRawBounds()
    {
        var strokeBounds = new Rect(0, 1500, 120, 80);
        var viewport = new Rect(0, 0, 1920, 1080);
        var matrix = Matrix.Identity;
        matrix.Translate(0, -1200);

        var result = PhotoInkViewportIntersectionPolicy.ShouldRender(
            photoInkModeActive: true,
            usePhotoTransform: false,
            strokeBounds: strokeBounds,
            photoTransformMatrix: matrix,
            viewportBounds: viewport);

        result.Should().BeFalse();
    }
}
