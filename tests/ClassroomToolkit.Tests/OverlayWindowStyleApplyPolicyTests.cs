using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayWindowStyleApplyPolicyTests
{
    [Fact]
    public void ShouldApply_ShouldReturnTrue_WhenNoPreviousState()
    {
        var shouldApply = OverlayWindowStyleApplyPolicy.ShouldApply(
            inputPassthroughEnabled: true,
            focusBlocked: false,
            lastInputPassthroughEnabled: null,
            lastFocusBlocked: null);

        shouldApply.Should().BeTrue();
    }

    [Fact]
    public void ShouldApply_ShouldReturnFalse_WhenStateUnchanged()
    {
        var shouldApply = OverlayWindowStyleApplyPolicy.ShouldApply(
            inputPassthroughEnabled: true,
            focusBlocked: false,
            lastInputPassthroughEnabled: true,
            lastFocusBlocked: false);

        shouldApply.Should().BeFalse();
    }

    [Fact]
    public void ShouldApply_ShouldReturnTrue_WhenAnyFlagChanged()
    {
        var shouldApply = OverlayWindowStyleApplyPolicy.ShouldApply(
            inputPassthroughEnabled: false,
            focusBlocked: false,
            lastInputPassthroughEnabled: true,
            lastFocusBlocked: false);

        shouldApply.Should().BeTrue();
    }
}
