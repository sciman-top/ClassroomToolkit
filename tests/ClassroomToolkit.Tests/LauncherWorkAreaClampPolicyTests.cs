using System.Windows;
using ClassroomToolkit.App;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LauncherWorkAreaClampPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnOriginalPosition_WhenAlreadyInsideWorkArea()
    {
        var workArea = new Rect(0, 0, 1920, 1080);

        var resolved = LauncherWorkAreaClampPolicy.Resolve(
            left: 120,
            top: 80,
            width: 640,
            height: 480,
            workArea: workArea);

        resolved.X.Should().Be(120);
        resolved.Y.Should().Be(80);
    }

    [Fact]
    public void Resolve_ShouldClampToLeftTopBounds_WhenOutOfBounds()
    {
        var workArea = new Rect(10, 20, 1000, 700);

        var resolved = LauncherWorkAreaClampPolicy.Resolve(
            left: -120,
            top: -90,
            width: 300,
            height: 200,
            workArea: workArea);

        resolved.X.Should().Be(10);
        resolved.Y.Should().Be(20);
    }

    [Fact]
    public void Resolve_ShouldClampToRightBottomBounds_WhenOverflow()
    {
        var workArea = new Rect(0, 0, 800, 600);

        var resolved = LauncherWorkAreaClampPolicy.Resolve(
            left: 760,
            top: 590,
            width: 200,
            height: 120,
            workArea: workArea);

        resolved.X.Should().Be(600);
        resolved.Y.Should().Be(480);
    }
}
