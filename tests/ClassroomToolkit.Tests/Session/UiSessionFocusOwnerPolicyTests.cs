using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionFocusOwnerPolicyTests
{
    [Theory]
    [InlineData(UiSceneKind.Idle, UiFocusOwner.None)]
    [InlineData(UiSceneKind.PresentationFullscreen, UiFocusOwner.Presentation)]
    [InlineData(UiSceneKind.PhotoFullscreen, UiFocusOwner.Photo)]
    [InlineData(UiSceneKind.Whiteboard, UiFocusOwner.Whiteboard)]
    public void Resolve_ShouldFollowFocusOwnerContract(UiSceneKind scene, UiFocusOwner expected)
    {
        var actual = UiSessionFocusOwnerPolicy.Resolve(scene);
        actual.Should().Be(expected);
    }
}
