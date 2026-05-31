using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationReservedNavigationKeyPolicyTests
{
    [Fact]
    public void ResolveRollCallGroupSwitchKeys_ShouldReserveEnter_WhenGroupSwitchUsesEnter()
    {
        var keys = PresentationReservedNavigationKeyPolicy.ResolveRollCallGroupSwitchKeys(
            enabled: true,
            configuredKey: "enter");

        keys.Should().ContainSingle().Which.Should().Be(VirtualKey.Enter);
    }

    [Fact]
    public void ResolveRollCallGroupSwitchKeys_ShouldReturnEmpty_WhenGroupSwitchDisabled()
    {
        var keys = PresentationReservedNavigationKeyPolicy.ResolveRollCallGroupSwitchKeys(
            enabled: false,
            configuredKey: "enter");

        keys.Should().BeEmpty();
    }

    [Fact]
    public void ResolveRollCallGroupSwitchKeys_ShouldFallbackToEnter_WhenConfiguredKeyBlank()
    {
        var keys = PresentationReservedNavigationKeyPolicy.ResolveRollCallGroupSwitchKeys(
            enabled: true,
            configuredKey: " ");

        keys.Should().ContainSingle().Which.Should().Be(VirtualKey.Enter);
    }
}
