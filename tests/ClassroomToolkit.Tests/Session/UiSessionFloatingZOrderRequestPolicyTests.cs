using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionFloatingZOrderRequestPolicyTests
{
    [Fact]
    public void TryResolveForOverlayTopmost_ShouldReturnRequest_WhenTopmostRequired()
    {
        var resolved = UiSessionFloatingZOrderRequestPolicy.TryResolveForOverlayTopmost(
            topmostRequired: true,
            out var request);

        resolved.Should().BeTrue();
        request.Should().Be(new FloatingZOrderRequest(ForceEnforceZOrder: true));
    }

    [Fact]
    public void TryResolveForOverlayTopmost_ShouldReturnFalse_WhenTopmostNotRequired()
    {
        var resolved = UiSessionFloatingZOrderRequestPolicy.TryResolveForOverlayTopmost(
            topmostRequired: false,
            out var request);

        resolved.Should().BeFalse();
        request.Should().Be(default(FloatingZOrderRequest));
    }

    [Fact]
    public void TryResolveForWidgetVisibility_ShouldReturnRequest_WhenAnyWidgetVisible()
    {
        var resolved = UiSessionFloatingZOrderRequestPolicy.TryResolveForWidgetVisibility(
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
        var resolved = UiSessionFloatingZOrderRequestPolicy.TryResolveForWidgetVisibility(
            new UiSessionWidgetVisibility(
                RollCallVisible: false,
                LauncherVisible: false,
                ToolbarVisible: false),
            out var request);

        resolved.Should().BeFalse();
        request.Should().Be(default(FloatingZOrderRequest));
    }
}
