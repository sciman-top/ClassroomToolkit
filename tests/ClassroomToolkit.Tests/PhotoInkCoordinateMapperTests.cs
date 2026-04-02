using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInkCoordinateMapperTests
{
    [Fact]
    public void CreateForwardAndInverseMatrix_ShouldRoundTripPoint()
    {
        var point = new Point(123.45, 67.89);
        var forward = PhotoInkCoordinateMapper.CreateForwardMatrix(
            pageScaleX: 1.2,
            pageScaleY: 0.8,
            photoScaleX: 1.5,
            photoScaleY: 1.5,
            translateX: -40,
            translateY: 25);
        var inverse = PhotoInkCoordinateMapper.CreateInverseMatrix(
            pageScaleX: 1.2,
            pageScaleY: 0.8,
            photoScaleX: 1.5,
            photoScaleY: 1.5,
            translateX: -40,
            translateY: 25);

        var roundTrip = inverse.Transform(forward.Transform(point));
        roundTrip.X.Should().BeApproximately(point.X, 0.0001);
        roundTrip.Y.Should().BeApproximately(point.Y, 0.0001);
    }

    [Fact]
    public void ToScreenAndPhotoGeometry_ShouldPreserveBoundsAfterRoundTrip()
    {
        var source = new RectangleGeometry(new Rect(10, 20, 30, 40));

        var screen = PhotoInkCoordinateMapper.ToScreenGeometry(
            source,
            pageScaleX: 1.1,
            pageScaleY: 0.9,
            photoScaleX: 1.4,
            photoScaleY: 1.4,
            translateX: 15,
            translateY: -8);
        var flattenedScreen = screen.GetFlattenedPathGeometry();
        var photo = PhotoInkCoordinateMapper.ToPhotoGeometry(
            flattenedScreen,
            pageScaleX: 1.1,
            pageScaleY: 0.9,
            photoScaleX: 1.4,
            photoScaleY: 1.4,
            translateX: 15,
            translateY: -8);

        photo.Bounds.X.Should().BeApproximately(source.Bounds.X, 0.01);
        photo.Bounds.Y.Should().BeApproximately(source.Bounds.Y, 0.01);
        photo.Bounds.Width.Should().BeApproximately(source.Bounds.Width, 0.01);
        photo.Bounds.Height.Should().BeApproximately(source.Bounds.Height, 0.01);
    }

    [Fact]
    public void CreateInverseMatrix_ShouldReturnIdentity_WhenScaleNearZero()
    {
        var inverse = PhotoInkCoordinateMapper.CreateInverseMatrix(
            pageScaleX: 0,
            pageScaleY: 1,
            photoScaleX: 1,
            photoScaleY: 1,
            translateX: 10,
            translateY: 20);

        inverse.Should().Be(Matrix.Identity);
    }
}
