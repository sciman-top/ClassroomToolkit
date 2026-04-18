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
            classifiesAsOffice: true,
            classifiesAsDedicatedWpsRuntime: false);

        allowed.Should().BeTrue();
    }

    [Fact]
    public void ShouldTreatAsPresentationFullscreen_ShouldAllowDedicatedWpsRuntimeWithoutSlideshowClass()
    {
        var allowed = PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: true,
            isFullscreen: true,
            classifiesAsSlideshow: false,
            classifiesAsOffice: false,
            classifiesAsDedicatedWpsRuntime: true);

        allowed.Should().BeTrue();
    }

    [Fact]
    public void ShouldTreatAsPresentationFullscreen_ShouldRejectWpsEditorFullscreenWithoutSlideshowClass()
    {
        var allowed = PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: true,
            isFullscreen: true,
            classifiesAsSlideshow: false,
            classifiesAsOffice: false,
            classifiesAsDedicatedWpsRuntime: false);

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
            classifiesAsOffice: true,
            classifiesAsDedicatedWpsRuntime: true);

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
            classifiesAsOffice: true,
            classifiesAsDedicatedWpsRuntime: true);
        var missingInfo = PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: false,
            isFullscreen: true,
            classifiesAsSlideshow: true,
            classifiesAsOffice: true,
            classifiesAsDedicatedWpsRuntime: true);

        invalid.Should().BeFalse();
        missingInfo.Should().BeFalse();
    }
}
