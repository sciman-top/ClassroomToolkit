using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Presentation;
using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WhiteboardResumeSceneResolverTests
{
    [Fact]
    public void Resolve_ShouldReturnPhotoScene_WhenPhotoModeActive()
    {
        var result = WhiteboardResumeSceneResolver.Resolve(
            photoModeActive: true,
            photoIsPdf: true,
            fullscreenPresentationSource: PresentationForegroundSource.Unknown);

        result.Scene.Should().Be(UiSceneKind.PhotoFullscreen);
        result.PhotoSource.Should().Be(PhotoSourceKind.Pdf);
        result.PresentationSource.Should().Be(PresentationSourceKind.Unknown);
    }

    [Fact]
    public void Resolve_ShouldReturnPresentationScene_WhenPresentationFullscreen()
    {
        var result = WhiteboardResumeSceneResolver.Resolve(
            photoModeActive: false,
            photoIsPdf: false,
            fullscreenPresentationSource: PresentationForegroundSource.Wps);

        result.Scene.Should().Be(UiSceneKind.PresentationFullscreen);
        result.PhotoSource.Should().Be(PhotoSourceKind.Unknown);
        result.PresentationSource.Should().Be(PresentationSourceKind.Wps);
    }

    [Fact]
    public void Resolve_ShouldMapOfficePresentation_ToPowerPointSource()
    {
        var result = WhiteboardResumeSceneResolver.Resolve(
            photoModeActive: false,
            photoIsPdf: false,
            fullscreenPresentationSource: PresentationForegroundSource.Office);

        result.Scene.Should().Be(UiSceneKind.PresentationFullscreen);
        result.PresentationSource.Should().Be(PresentationSourceKind.PowerPoint);
    }

    [Fact]
    public void Resolve_ShouldReturnIdle_WhenNoPhotoAndNoPresentation()
    {
        var result = WhiteboardResumeSceneResolver.Resolve(
            photoModeActive: false,
            photoIsPdf: false,
            fullscreenPresentationSource: PresentationForegroundSource.Unknown);

        result.Scene.Should().Be(UiSceneKind.Idle);
        result.PhotoSource.Should().Be(PhotoSourceKind.Unknown);
        result.PresentationSource.Should().Be(PresentationSourceKind.Unknown);
    }
}
