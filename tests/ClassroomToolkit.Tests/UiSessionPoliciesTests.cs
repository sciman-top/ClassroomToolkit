using AwesomeAssertions;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionFloatingZOrderRequestPolicyTests
{
    [Fact]
    public void TryResolveForOverlayTopmost_ShouldReturnRequest_WhenTopmostRequired()
    {
        var resolved = UiSessionPolicies.TryResolveForOverlayTopmost(
            topmostRequired: true,
            out var request);

        resolved.Should().BeTrue();
        request.Should().Be(new FloatingZOrderRequest(ForceEnforceZOrder: true));
    }

    [Fact]
    public void TryResolveForOverlayTopmost_ShouldReturnFalse_WhenTopmostNotRequired()
    {
        var resolved = UiSessionPolicies.TryResolveForOverlayTopmost(
            topmostRequired: false,
            out var request);

        resolved.Should().BeFalse();
        request.Should().Be(default(FloatingZOrderRequest));
    }

    [Fact]
    public void TryResolveForWidgetVisibility_ShouldReturnRequest_WhenAnyWidgetVisible()
    {
        var resolved = UiSessionPolicies.TryResolveForWidgetVisibility(
            new UiSessionWidgetVisibility(
                RollCallVisible: false,
                LauncherVisible: true,
                ToolbarVisible: false),
            out var request);

        resolved.Should().BeTrue();
        request.Should().Be(new FloatingZOrderRequest(ForceEnforceZOrder: true));
    }

    [Fact]
    public void TryResolveForWidgetVisibility_ShouldReturnFalse_WhenAllWidgetsHidden()
    {
        var resolved = UiSessionPolicies.TryResolveForWidgetVisibility(
            new UiSessionWidgetVisibility(
                RollCallVisible: false,
                LauncherVisible: false,
                ToolbarVisible: false),
            out var request);

        resolved.Should().BeFalse();
        request.Should().Be(default(FloatingZOrderRequest));
    }
}

public sealed class UiSessionFocusOwnerPolicyTests
{
    [Theory]
    [InlineData(UiSceneKind.Idle, UiFocusOwner.None)]
    [InlineData(UiSceneKind.PresentationFullscreen, UiFocusOwner.Presentation)]
    [InlineData(UiSceneKind.PhotoFullscreen, UiFocusOwner.Photo)]
    [InlineData(UiSceneKind.Whiteboard, UiFocusOwner.Whiteboard)]
    public void Resolve_ShouldFollowFocusOwnerContract(UiSceneKind scene, UiFocusOwner expected)
    {
        var actual = UiSessionPolicies.ResolveFocusOwner(scene);
        actual.Should().Be(expected);
    }
}

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
        var actual = UiSessionPolicies.ResolveInkVisibility(scene, toolMode);
        actual.Should().Be(expected);
    }
}

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
        var actual = UiSessionPolicies.ResolveNavigation(scene, toolMode);
        actual.Should().Be(expected);
    }
}

public sealed class UiSessionOverlayVisibilityPolicyTests
{
    [Theory]
    [InlineData(UiSceneKind.Idle, false)]
    [InlineData(UiSceneKind.PresentationFullscreen, true)]
    [InlineData(UiSceneKind.PhotoFullscreen, true)]
    [InlineData(UiSceneKind.Whiteboard, true)]
    public void IsOverlayTopmostRequired_ShouldMatchContract(UiSceneKind scene, bool expected)
    {
        UiSessionPolicies.IsOverlayTopmostRequired(scene).Should().Be(expected);
    }

    [Theory]
    [InlineData(UiSceneKind.Idle, false)]
    [InlineData(UiSceneKind.PresentationFullscreen, true)]
    [InlineData(UiSceneKind.PhotoFullscreen, true)]
    [InlineData(UiSceneKind.Whiteboard, true)]
    public void AreFloatingWidgetsVisible_ShouldMatchContract(UiSceneKind scene, bool expected)
    {
        UiSessionPolicies.AreFloatingWidgetsVisible(scene).Should().Be(expected);
    }
}

public sealed class UiSessionPresentationInputPolicyTests
{
    [Theory]
    [InlineData(UiNavigationMode.Disabled, false)]
    [InlineData(UiNavigationMode.MessageOnly, false)]
    [InlineData(UiNavigationMode.HookOnly, true)]
    [InlineData(UiNavigationMode.Hybrid, true)]
    public void AllowsPresentationInput_ShouldMatchContract(
        UiNavigationMode navigationMode,
        bool expected)
    {
        UiSessionPolicies.AllowsPresentationInput(navigationMode)
            .Should().Be(expected);
    }
}

public sealed class UiSessionWidgetVisibilityEffectPolicyTests
{
    [Fact]
    public void ShouldRequestFloatingZOrder_ShouldReturnTrue_WhenAnyWidgetVisible()
    {
        var visibility = new UiSessionWidgetVisibility(
            RollCallVisible: false,
            LauncherVisible: true,
            ToolbarVisible: false);

        UiSessionPolicies.ShouldRequestFloatingZOrder(visibility).Should().BeTrue();
    }

    [Fact]
    public void ShouldRequestFloatingZOrder_ShouldReturnFalse_WhenAllWidgetsHidden()
    {
        var visibility = new UiSessionWidgetVisibility(
            RollCallVisible: false,
            LauncherVisible: false,
            ToolbarVisible: false);

        UiSessionPolicies.ShouldRequestFloatingZOrder(visibility).Should().BeFalse();
    }
}
