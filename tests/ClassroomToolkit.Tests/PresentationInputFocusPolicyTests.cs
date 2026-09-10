using AwesomeAssertions;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.Tests;

public sealed class PresentationInputFocusPolicyTests
{
    [Fact]
    public void IsAuthorizedForeground_ShouldRequireExactOverlayOrToolbarHandle()
    {
        PresentationInputFocusPolicy.IsAuthorizedForeground(
            new IntPtr(10),
            new IntPtr(10),
            new IntPtr(20)).Should().BeTrue();

        PresentationInputFocusPolicy.IsAuthorizedForeground(
            new IntPtr(20),
            new IntPtr(10),
            new IntPtr(20)).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorizedForeground_ShouldRejectOtherSameProcessWindow()
    {
        PresentationInputFocusPolicy.IsAuthorizedForeground(
            new IntPtr(30),
            new IntPtr(10),
            new IntPtr(20)).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorizedForeground_ShouldRejectZeroHandle()
    {
        PresentationInputFocusPolicy.IsAuthorizedForeground(
            IntPtr.Zero,
            new IntPtr(10),
            new IntPtr(20)).Should().BeFalse();
    }
}
