using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Presentation;
using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class SessionSceneSourceMapperTests
{
    [Fact]
    public void MapPresentationSource_ShouldMapKnownTypes()
    {
        SessionSceneSourceMapper.MapPresentationSource(PresentationForegroundSource.Wps)
            .Should().Be(PresentationSourceKind.Wps);
        SessionSceneSourceMapper.MapPresentationSource(PresentationForegroundSource.Office)
            .Should().Be(PresentationSourceKind.PowerPoint);
        SessionSceneSourceMapper.MapPresentationSource(PresentationForegroundSource.Unknown)
            .Should().Be(PresentationSourceKind.Unknown);
    }

    [Fact]
    public void MapPhotoSource_ShouldMapPdfAndImage()
    {
        SessionSceneSourceMapper.MapPhotoSource(isPdf: true).Should().Be(PhotoSourceKind.Pdf);
        SessionSceneSourceMapper.MapPhotoSource(isPdf: false).Should().Be(PhotoSourceKind.Image);
    }
}
