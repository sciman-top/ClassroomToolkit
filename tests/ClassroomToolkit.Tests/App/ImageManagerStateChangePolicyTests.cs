using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerStateChangePolicyTests
{
    [Fact]
    public void Resolve_ContextOverload_ShouldRecoverOverlay_WhenBothMinimized()
    {
        var context = new ImageManagerStateChangeContext(
            ImageManagerExists: true,
            ImageManagerWindowState: System.Windows.WindowState.Minimized,
            OverlayVisible: true,
            OverlayWindowState: System.Windows.WindowState.Minimized);

        var decision = ImageManagerStateChangePolicy.Resolve(context);

        decision.NormalizeOverlayWindowState.Should().BeTrue();
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldRecoverOverlay_WhenImageManagerMinimizesWhileOverlayMinimized()
    {
        var decision = ImageManagerStateChangePolicy.Resolve(
            imageManagerExists: true,
            imageManagerMinimized: true,
            overlayVisible: true,
            overlayMinimized: true);

        decision.NormalizeOverlayWindowState.Should().BeTrue();
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldDoNothing_WhenOverlayNotMinimized()
    {
        var decision = ImageManagerStateChangePolicy.Resolve(
            imageManagerExists: true,
            imageManagerMinimized: true,
            overlayVisible: true,
            overlayMinimized: false);

        decision.NormalizeOverlayWindowState.Should().BeFalse();
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}
