using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintWindowCreationPolicyTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void ShouldEnsureWindows_ShouldMatchExpected(
        bool hasOverlayWindow,
        bool hasToolbarWindow,
        bool expected)
    {
        PaintWindowCreationPolicy.ShouldEnsureWindows(hasOverlayWindow, hasToolbarWindow)
            .Should()
            .Be(expected);
    }
}
