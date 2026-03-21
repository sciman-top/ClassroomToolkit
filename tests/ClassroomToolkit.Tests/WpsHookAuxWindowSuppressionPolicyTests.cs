using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WpsHookAuxWindowSuppressionPolicyTests
{
    [Fact]
    public void ShouldSuppressNavigation_ShouldReturnTrue_WhenForegroundIsCurrentProcessAuxWindow()
    {
        var suppressed = WpsHookAuxWindowSuppressionPolicy.ShouldSuppressNavigation(
            foregroundOwnedByCurrentProcess: true,
            foregroundWindow: new IntPtr(0x1234),
            overlayWindow: new IntPtr(0x4321));

        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppressNavigation_ShouldReturnFalse_WhenForegroundIsOverlayWindow()
    {
        var overlay = new IntPtr(0x1234);
        var suppressed = WpsHookAuxWindowSuppressionPolicy.ShouldSuppressNavigation(
            foregroundOwnedByCurrentProcess: true,
            foregroundWindow: overlay,
            overlayWindow: overlay);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppressNavigation_ShouldReturnFalse_WhenForegroundNotOwnedByCurrentProcess()
    {
        var suppressed = WpsHookAuxWindowSuppressionPolicy.ShouldSuppressNavigation(
            foregroundOwnedByCurrentProcess: false,
            foregroundWindow: new IntPtr(0x1234),
            overlayWindow: new IntPtr(0x4321));

        suppressed.Should().BeFalse();
    }
}
