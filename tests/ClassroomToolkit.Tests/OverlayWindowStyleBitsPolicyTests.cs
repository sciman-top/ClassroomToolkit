using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayWindowStyleBitsPolicyTests
{
    [Fact]
    public void Resolve_ShouldSetTransparentAndNoActivate_WhenBothEnabled()
    {
        var mask = OverlayWindowStyleBitsPolicy.Resolve(
            inputPassthroughEnabled: true,
            focusBlocked: true);

        mask.SetMask.Should().Be(NativeMethods.WsExTransparent | NativeMethods.WsExNoActivate);
        mask.ClearMask.Should().Be(0);
    }

    [Fact]
    public void Resolve_ShouldClearBoth_WhenBothDisabled()
    {
        var mask = OverlayWindowStyleBitsPolicy.Resolve(
            inputPassthroughEnabled: false,
            focusBlocked: false);

        mask.SetMask.Should().Be(0);
        mask.ClearMask.Should().Be(NativeMethods.WsExTransparent | NativeMethods.WsExNoActivate);
    }
}
