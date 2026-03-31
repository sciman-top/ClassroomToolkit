using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInkCurrentPageClipPolicyTests
{
    [Fact]
    public void ResolveBounds_ShouldReturnPageBounds_WhenCrossPagePhotoInkTransformIsActive()
    {
        var rect = PhotoInkCurrentPageClipPolicy.ResolveBounds(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            usePhotoTransform: true,
            currentPageScreenRect: Rect.Empty,
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(new Rect(0, 0, 1280, 720));
    }

    [Fact]
    public void ResolveBounds_ShouldReturnScreenRect_WhenInkRendersInScreenSpace()
    {
        var rect = PhotoInkCurrentPageClipPolicy.ResolveBounds(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            usePhotoTransform: false,
            currentPageScreenRect: new Rect(40, 60, 800, 450),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(new Rect(40, 60, 800, 450));
    }

    [Fact]
    public void ResolveBounds_ShouldReturnEmpty_WhenCrossPageDisplayIsInactive()
    {
        var rect = PhotoInkCurrentPageClipPolicy.ResolveBounds(
            photoInkModeActive: true,
            crossPageDisplayActive: false,
            photoFullscreenActive: false,
            usePhotoTransform: false,
            currentPageScreenRect: new Rect(40, 60, 800, 450),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }

    [Fact]
    public void ResolveBounds_ShouldReturnEmpty_WhenPageBoundsAreInvalid()
    {
        var rect = PhotoInkCurrentPageClipPolicy.ResolveBounds(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            usePhotoTransform: true,
            currentPageScreenRect: Rect.Empty,
            pageWidthDip: 0,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }

    [Fact]
    public void ResolveBounds_ShouldReturnEmpty_WhenScreenRectIsInvalid()
    {
        var rect = PhotoInkCurrentPageClipPolicy.ResolveBounds(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            usePhotoTransform: false,
            currentPageScreenRect: Rect.Empty,
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }

    [Fact]
    public void ResolveBounds_ShouldReturnEmpty_WhenPhotoFullscreenIsActive()
    {
        var rect = PhotoInkCurrentPageClipPolicy.ResolveBounds(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: true,
            usePhotoTransform: false,
            currentPageScreenRect: new Rect(40, 60, 800, 450),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }
}
