using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionNavigationPolicyTests
{
    [Theory]
    [InlineData(UiSceneKind.Idle, UiToolMode.Draw, UiNavigationMode.Disabled)]
    [InlineData(UiSceneKind.Idle, UiToolMode.Cursor, UiNavigationMode.Disabled)]
    [InlineData(UiSceneKind.PresentationFullscreen, UiToolMode.Draw, UiNavigationMode.HookOnly)]
    [InlineData(UiSceneKind.PresentationFullscreen, UiToolMode.Cursor, UiNavigationMode.Hybrid)]
    [InlineData(UiSceneKind.PhotoFullscreen, UiToolMode.Draw, UiNavigationMode.Disabled)]
    [InlineData(UiSceneKind.PhotoFullscreen, UiToolMode.Cursor, UiNavigationMode.MessageOnly)]
    [InlineData(UiSceneKind.Whiteboard, UiToolMode.Draw, UiNavigationMode.Disabled)]
    [InlineData(UiSceneKind.Whiteboard, UiToolMode.Cursor, UiNavigationMode.Disabled)]
    public void Resolve_ShouldFollowNavigationContract(
        UiSceneKind scene,
        UiToolMode toolMode,
        UiNavigationMode expected)
    {
        var actual = UiSessionNavigationPolicy.Resolve(scene, toolMode);
        actual.Should().Be(expected);
    }
}
