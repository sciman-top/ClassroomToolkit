using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PresentationFullscreenWindowAdmissionPolicyTests
{
    [Fact]
    public void ShouldTreatAsPresentationFullscreen_ShouldAllowOfficeFullscreenFallback_WhenClassNotSlideshow()
    {
        var allowed = PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: true,
            isFullscreen: true,
            classifiesAsSlideshow: false,
            classifiesAsOffice: true);

        allowed.Should().BeTrue();
    }

    [Fact]
    public void ShouldTreatAsPresentationFullscreen_ShouldRejectWpsFullscreenWithoutSlideshowClass()
    {
        var allowed = PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: true,
            isFullscreen: true,
            classifiesAsSlideshow: false,
            classifiesAsOffice: false);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void ShouldTreatAsPresentationFullscreen_ShouldRejectNonFullscreenEvenWhenOffice()
    {
        var allowed = PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: true,
            isFullscreen: false,
            classifiesAsSlideshow: false,
            classifiesAsOffice: true);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void ShouldTreatAsPresentationFullscreen_ShouldRejectInvalidOrMissingTargetInfo()
    {
        var invalid = PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: false,
            targetHasInfo: true,
            isFullscreen: true,
            classifiesAsSlideshow: true,
            classifiesAsOffice: true);
        var missingInfo = PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: false,
            isFullscreen: true,
            classifiesAsSlideshow: true,
            classifiesAsOffice: true);

        invalid.Should().BeFalse();
        missingInfo.Should().BeFalse();
    }
}
