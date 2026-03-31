using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInkPreviewClipPolicyTests
{
    [Fact]
    public void ResolveBounds_ShouldReturnScreenRect_WhenPhotoTransformIsActiveAndScreenRectIsValid()
    {
        var rect = PhotoInkPreviewClipPolicy.ResolveBounds(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            usePhotoTransform: true,
            currentPageScreenRect: new Rect(32, 128, 960, 540),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(new Rect(32, 128, 960, 540));
    }

    [Fact]
    public void ResolveBounds_ShouldFallbackToPageBounds_WhenScreenRectIsUnavailable()
    {
        var rect = PhotoInkPreviewClipPolicy.ResolveBounds(
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
    public void ResolveBounds_ShouldReturnEmpty_WhenCrossPageDisplayIsInactive()
    {
        var rect = PhotoInkPreviewClipPolicy.ResolveBounds(
            photoInkModeActive: true,
            crossPageDisplayActive: false,
            photoFullscreenActive: false,
            usePhotoTransform: true,
            currentPageScreenRect: new Rect(32, 128, 960, 540),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }

    [Fact]
    public void ResolveBounds_ShouldReturnEmpty_WhenPhotoFullscreenIsActive()
    {
        var rect = PhotoInkPreviewClipPolicy.ResolveBounds(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: true,
            usePhotoTransform: true,
            currentPageScreenRect: new Rect(32, 128, 960, 540),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }
}
