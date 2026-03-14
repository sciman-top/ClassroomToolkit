using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageRequestAdmissionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReject_WhenCrossPageInactive()
    {
        var decision = CrossPageRequestAdmissionPolicy.Resolve(
            crossPageDisplayActive: false,
            photoLoading: false,
            hasPhotoBackgroundSource: true,
            overlayVisible: true,
            overlayMinimized: false,
            hasUsableViewport: true);

        decision.ShouldAdmit.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageRequestAdmissionReason.CrossPageInactive);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenPhotoLoading()
    {
        var decision = CrossPageRequestAdmissionPolicy.Resolve(
            crossPageDisplayActive: true,
            photoLoading: true,
            hasPhotoBackgroundSource: true,
            overlayVisible: true,
            overlayMinimized: false,
            hasUsableViewport: true);

        decision.ShouldAdmit.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageRequestAdmissionReason.PhotoLoading);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenBackgroundNotReady()
    {
        var decision = CrossPageRequestAdmissionPolicy.Resolve(
            crossPageDisplayActive: true,
            photoLoading: false,
            hasPhotoBackgroundSource: false,
            overlayVisible: true,
            overlayMinimized: false,
            hasUsableViewport: true);

        decision.ShouldAdmit.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageRequestAdmissionReason.BackgroundNotReady);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenOverlayNotVisible()
    {
        var decision = CrossPageRequestAdmissionPolicy.Resolve(
            crossPageDisplayActive: true,
            photoLoading: false,
            hasPhotoBackgroundSource: true,
            overlayVisible: false,
            overlayMinimized: false,
            hasUsableViewport: true);

        decision.ShouldAdmit.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageRequestAdmissionReason.OverlayNotVisible);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenOverlayMinimized()
    {
        var decision = CrossPageRequestAdmissionPolicy.Resolve(
            crossPageDisplayActive: true,
            photoLoading: false,
            hasPhotoBackgroundSource: true,
            overlayVisible: true,
            overlayMinimized: true,
            hasUsableViewport: true);

        decision.ShouldAdmit.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageRequestAdmissionReason.OverlayMinimized);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenViewportUnavailable()
    {
        var decision = CrossPageRequestAdmissionPolicy.Resolve(
            crossPageDisplayActive: true,
            photoLoading: false,
            hasPhotoBackgroundSource: true,
            overlayVisible: true,
            overlayMinimized: false,
            hasUsableViewport: false);

        decision.ShouldAdmit.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageRequestAdmissionReason.ViewportUnavailable);
    }

    [Fact]
    public void Resolve_ShouldAdmit_WhenReady()
    {
        var decision = CrossPageRequestAdmissionPolicy.Resolve(
            crossPageDisplayActive: true,
            photoLoading: false,
            hasPhotoBackgroundSource: true,
            overlayVisible: true,
            overlayMinimized: false,
            hasUsableViewport: true);

        decision.ShouldAdmit.Should().BeTrue();
        decision.Reason.Should().Be(CrossPageRequestAdmissionReason.None);
    }
}
