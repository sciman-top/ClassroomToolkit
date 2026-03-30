using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionWidgetVisibilityEffectPolicyTests
{
    [Fact]
    public void ShouldRequestFloatingZOrder_ShouldReturnTrue_WhenAnyWidgetVisible()
    {
        var visibility = new UiSessionWidgetVisibility(
            RollCallVisible: false,
            LauncherVisible: true,
            ToolbarVisible: false);

        UiSessionWidgetVisibilityEffectPolicy.ShouldRequestFloatingZOrder(visibility).Should().BeTrue();
    }

    [Fact]
    public void ShouldRequestFloatingZOrder_ShouldReturnFalse_WhenAllWidgetsHidden()
    {
        var visibility = new UiSessionWidgetVisibility(
            RollCallVisible: false,
            LauncherVisible: false,
            ToolbarVisible: false);

        UiSessionWidgetVisibilityEffectPolicy.ShouldRequestFloatingZOrder(visibility).Should().BeFalse();
    }
}
