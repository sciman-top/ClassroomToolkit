using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationSlideshowDetectionPolicyTests
{
    [Fact]
    public void IsSlideshow_ShouldReturnFalse_WhenTargetInvalid()
    {
        var classifier = new PresentationClassifier();

        var result = PresentationSlideshowDetectionPolicy.IsSlideshow(
            PresentationTarget.Empty,
            classifier,
            _ => true);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsSlideshow_ShouldReturnTrue_WhenClassifierMatches()
    {
        var classifier = new PresentationClassifier();
        var target = new PresentationTarget(
            new IntPtr(1001),
            new PresentationWindowInfo(1, "powerpnt.exe", new[] { "screenclass" }));

        var result = PresentationSlideshowDetectionPolicy.IsSlideshow(
            target,
            classifier,
            _ => false);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsSlideshow_ShouldFallbackToFullscreen_WhenClassifierMisses()
    {
        var classifier = new PresentationClassifier();
        var target = new PresentationTarget(
            new IntPtr(2002),
            new PresentationWindowInfo(1, "wpspresentation.exe", new[] { "randomclass" }));

        var result = PresentationSlideshowDetectionPolicy.IsSlideshow(
            target,
            classifier,
            _ => true);

        result.Should().BeTrue();
    }
}
