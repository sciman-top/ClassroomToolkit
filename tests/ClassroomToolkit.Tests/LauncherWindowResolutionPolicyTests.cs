using ClassroomToolkit.App;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class LauncherWindowResolutionPolicyTests
{
    [Theory]
    [InlineData(1, true, true)]
    [InlineData(1, false, false)]
    [InlineData(0, true, false)]
    public void ShouldUseBubbleWindow_ShouldMatchExpected(
        int resolvedKind,
        bool bubbleWindowExists,
        bool expected)
    {
        var result = LauncherWindowResolutionPolicy.ShouldUseBubbleWindow(
            (LauncherWindowKind)resolvedKind,
            bubbleWindowExists);

        result.Should().Be(expected);
    }
}
