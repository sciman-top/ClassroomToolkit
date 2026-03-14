using System.Windows;
using ClassroomToolkit.App.Photos;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerRestoreBoundsPolicyTests
{
    [Fact]
    public void Resolve_ShouldClampOversizedRestoreBoundsToWorkAreaCaps()
    {
        var plan = ImageManagerRestoreBoundsPolicy.Resolve(
            restoredWidth: 3840,
            restoredHeight: 2160,
            defaultWidth: 1100,
            defaultHeight: 700,
            minWidth: 960,
            minHeight: 620,
            workArea: new Rect(0, 0, 1920, 1080));

        plan.Width.Should().Be(1100);
        plan.Height.Should().Be(700);
        plan.Left.Should().BeApproximately((1920 - 1100) / 2.0, 0.2);
        plan.Top.Should().BeApproximately((1080 - 700) / 2.0, 0.2);
    }

    [Fact]
    public void Resolve_ShouldKeepRestoredSize_WhenWithinSafeRange()
    {
        var plan = ImageManagerRestoreBoundsPolicy.Resolve(
            restoredWidth: 1280,
            restoredHeight: 820,
            defaultWidth: 1100,
            defaultHeight: 700,
            minWidth: 960,
            minHeight: 620,
            workArea: new Rect(0, 0, 1920, 1080));

        plan.Width.Should().Be(1100);
        plan.Height.Should().Be(700);
    }

    [Fact]
    public void Resolve_ShouldKeepSmallerRestoredSize_WhenAboveMinimum()
    {
        var plan = ImageManagerRestoreBoundsPolicy.Resolve(
            restoredWidth: 1024,
            restoredHeight: 680,
            defaultWidth: 1100,
            defaultHeight: 700,
            minWidth: 960,
            minHeight: 620,
            workArea: new Rect(0, 0, 1920, 1080));

        plan.Width.Should().Be(1024);
        plan.Height.Should().Be(680);
    }
}
