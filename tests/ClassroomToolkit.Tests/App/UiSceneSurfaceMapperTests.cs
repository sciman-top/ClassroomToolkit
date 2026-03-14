using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class UiSceneSurfaceMapperTests
{
    [Theory]
    [InlineData(UiSceneKind.Idle, ZOrderSurface.None)]
    [InlineData(UiSceneKind.PresentationFullscreen, ZOrderSurface.PresentationFullscreen)]
    [InlineData(UiSceneKind.PhotoFullscreen, ZOrderSurface.PhotoFullscreen)]
    [InlineData(UiSceneKind.Whiteboard, ZOrderSurface.Whiteboard)]
    public void Map_ShouldReturnExpectedSurface(UiSceneKind scene, ZOrderSurface expected)
    {
        var actual = UiSceneSurfaceMapper.Map(scene);
        actual.Should().Be(expected);
    }
}
