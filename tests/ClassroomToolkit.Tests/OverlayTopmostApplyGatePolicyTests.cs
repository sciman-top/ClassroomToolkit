using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayTopmostApplyGatePolicyTests
{
    [Theory]
    [InlineData(false, WindowState.Normal, false)]
    [InlineData(true, WindowState.Minimized, false)]
    [InlineData(true, WindowState.Normal, true)]
    [InlineData(true, WindowState.Maximized, true)]
    public void ShouldApply_ShouldMatchExpected(bool overlayVisible, WindowState windowState, bool expected)
    {
        OverlayTopmostApplyGatePolicy.ShouldApply(overlayVisible, windowState)
            .Should()
            .Be(expected);
    }
}
