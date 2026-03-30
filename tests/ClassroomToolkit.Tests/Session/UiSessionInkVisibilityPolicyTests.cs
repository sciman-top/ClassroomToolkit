using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionInkVisibilityPolicyTests
{
    [Theory]
    [InlineData(UiSceneKind.Idle, UiToolMode.Draw, UiInkVisibility.VisibleEditable)]
    [InlineData(UiSceneKind.Idle, UiToolMode.Cursor, UiInkVisibility.Hidden)]
    [InlineData(UiSceneKind.PresentationFullscreen, UiToolMode.Draw, UiInkVisibility.VisibleEditable)]
    [InlineData(UiSceneKind.PresentationFullscreen, UiToolMode.Cursor, UiInkVisibility.VisibleReadOnly)]
    [InlineData(UiSceneKind.PhotoFullscreen, UiToolMode.Draw, UiInkVisibility.VisibleEditable)]
    [InlineData(UiSceneKind.PhotoFullscreen, UiToolMode.Cursor, UiInkVisibility.VisibleReadOnly)]
    [InlineData(UiSceneKind.Whiteboard, UiToolMode.Draw, UiInkVisibility.VisibleEditable)]
    [InlineData(UiSceneKind.Whiteboard, UiToolMode.Cursor, UiInkVisibility.VisibleReadOnly)]
    public void Resolve_ShouldFollowInkVisibilityContract(
        UiSceneKind scene,
        UiToolMode toolMode,
        UiInkVisibility expected)
    {
        var actual = UiSessionInkVisibilityPolicy.Resolve(scene, toolMode);
        actual.Should().Be(expected);
    }
}
