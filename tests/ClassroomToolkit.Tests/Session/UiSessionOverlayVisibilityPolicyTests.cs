using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionOverlayVisibilityPolicyTests
{
    [Theory]
    [InlineData(UiSceneKind.Idle, false)]
    [InlineData(UiSceneKind.PresentationFullscreen, true)]
    [InlineData(UiSceneKind.PhotoFullscreen, true)]
    [InlineData(UiSceneKind.Whiteboard, true)]
    public void IsOverlayTopmostRequired_ShouldMatchContract(UiSceneKind scene, bool expected)
    {
        UiSessionOverlayVisibilityPolicy.IsOverlayTopmostRequired(scene).Should().Be(expected);
    }

    [Theory]
    [InlineData(UiSceneKind.Idle, false)]
    [InlineData(UiSceneKind.PresentationFullscreen, true)]
    [InlineData(UiSceneKind.PhotoFullscreen, true)]
    [InlineData(UiSceneKind.Whiteboard, true)]
    public void AreFloatingWidgetsVisible_ShouldMatchContract(UiSceneKind scene, bool expected)
    {
        UiSessionOverlayVisibilityPolicy.AreFloatingWidgetsVisible(scene).Should().Be(expected);
    }
}
