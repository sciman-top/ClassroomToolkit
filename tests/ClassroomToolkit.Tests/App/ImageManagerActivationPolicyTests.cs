using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerActivationPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnTrue_WhenImageManagerIsFront_AndNoUtilityWindowIsActive()
    {
        var decision = ImageManagerActivationPolicy.Resolve(
            imageManagerTopmost: true,
            imageManagerActive: false,
            toolbarActive: false,
            rollCallActive: false,
            launcherActive: false);

        decision.ShouldActivate.Should().BeTrue();
        decision.Reason.Should().Be(ImageManagerActivationReason.None);
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_WhenAlreadyActive()
    {
        var decision = ImageManagerActivationPolicy.Resolve(
            imageManagerTopmost: true,
            imageManagerActive: true,
            toolbarActive: false,
            rollCallActive: false,
            launcherActive: false);

        decision.ShouldActivate.Should().BeFalse();
        decision.Reason.Should().Be(ImageManagerActivationReason.AlreadyActive);
    }

    [Theory]
    [InlineData(true, false, false, (int)ImageManagerActivationReason.BlockedByToolbar)]
    [InlineData(false, true, false, (int)ImageManagerActivationReason.BlockedByRollCall)]
    [InlineData(false, false, true, (int)ImageManagerActivationReason.BlockedByLauncher)]
    public void Resolve_ShouldReturnFalse_WhenAnyUtilityWindowIsActive(
        bool toolbarActive,
        bool rollCallActive,
        bool launcherActive,
        int expectedReason)
    {
        var decision = ImageManagerActivationPolicy.Resolve(
            imageManagerTopmost: true,
            imageManagerActive: false,
            toolbarActive: toolbarActive,
            rollCallActive: rollCallActive,
            launcherActive: launcherActive);

        decision.ShouldActivate.Should().BeFalse();
        decision.Reason.Should().Be((ImageManagerActivationReason)expectedReason);
    }

    [Fact]
    public void ShouldActivate_ShouldMapResolveDecision()
    {
        ImageManagerActivationPolicy.ShouldActivate(
            imageManagerTopmost: true,
            imageManagerActive: false,
            toolbarActive: false,
            rollCallActive: false,
            launcherActive: false).Should().BeTrue();
    }
}
