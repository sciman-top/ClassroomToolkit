using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkRedrawClipPolicyTests
{
    [Fact]
    public void ShouldUsePartialClear_ShouldReturnTrue_WhenClipMatchesLastFrame()
    {
        var clip = new Int32Rect(10, 20, 300, 200);

        var result = InkRedrawClipPolicy.ShouldUsePartialClear(
            clipAvailable: true,
            clipPixelRect: clip,
            lastClipPixelRect: clip);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldUsePartialClear_ShouldReturnFalse_WhenClipChanged()
    {
        var result = InkRedrawClipPolicy.ShouldUsePartialClear(
            clipAvailable: true,
            clipPixelRect: new Int32Rect(10, 20, 300, 200),
            lastClipPixelRect: new Int32Rect(10, 20, 301, 200));

        result.Should().BeFalse();
    }

    [Fact]
    public void TryResolvePixelClip_ShouldClampToSurfaceBounds()
    {
        var ok = InkRedrawClipPolicy.TryResolvePixelClip(
            clipBoundsDip: new Rect(-10, -20, 250, 180),
            surfacePixelWidth: 200,
            surfacePixelHeight: 120,
            surfaceDpiX: 96,
            surfaceDpiY: 96,
            out var rect);

        ok.Should().BeTrue();
        rect.Should().Be(new Int32Rect(0, 0, 200, 120));
    }
}
