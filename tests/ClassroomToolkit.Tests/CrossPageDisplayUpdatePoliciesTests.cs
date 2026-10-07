using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayClearPolicyTests
{
    [Fact]
    public void ShouldClearNeighborPages_ShouldReturnTrue_WhenTotalPagesNotGreaterThanOne()
    {
        CrossPageDisplayUpdatePolicies.ShouldClearNeighborPages(
            totalPages: 1,
            hasCurrentBitmap: true,
            currentPageHeight: 100).Should().BeTrue();
    }

    [Fact]
    public void ShouldClearNeighborPages_ShouldReturnTrue_WhenCurrentBitmapMissing()
    {
        CrossPageDisplayUpdatePolicies.ShouldClearNeighborPages(
            totalPages: 5,
            hasCurrentBitmap: false,
            currentPageHeight: 100).Should().BeTrue();
    }

    [Fact]
    public void ShouldClearNeighborPages_ShouldReturnTrue_WhenCurrentPageHeightInvalid()
    {
        CrossPageDisplayUpdatePolicies.ShouldClearNeighborPages(
            totalPages: 5,
            hasCurrentBitmap: true,
            currentPageHeight: 0).Should().BeTrue();
    }

    [Fact]
    public void ShouldClearNeighborPages_ShouldReturnFalse_WhenInputsAreValid()
    {
        CrossPageDisplayUpdatePolicies.ShouldClearNeighborPages(
            totalPages: 5,
            hasCurrentBitmap: true,
            currentPageHeight: 320).Should().BeFalse();
    }
}

public sealed class CrossPagePdfVisiblePrefetchUpdatePolicyTests
{
    [Fact]
    public void ShouldRefreshCrossPageDisplay_ShouldReturnTrue_WhenPdfAndCrossPagePhotoTransformActive()
    {
        CrossPageDisplayUpdatePolicies.ShouldRefreshCrossPageDisplay(
            photoModeActive: true,
            photoDocumentIsPdf: true,
            boardActive: false,
            crossPageDisplayEnabled: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldRefreshCrossPageDisplay_ShouldReturnFalse_WhenNotPdf()
    {
        CrossPageDisplayUpdatePolicies.ShouldRefreshCrossPageDisplay(
            photoModeActive: true,
            photoDocumentIsPdf: false,
            boardActive: false,
            crossPageDisplayEnabled: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldRefreshCrossPageDisplay_ShouldReturnFalse_WhenBoardActive()
    {
        CrossPageDisplayUpdatePolicies.ShouldRefreshCrossPageDisplay(
            photoModeActive: true,
            photoDocumentIsPdf: true,
            boardActive: true,
            crossPageDisplayEnabled: true).Should().BeFalse();
    }
}

public sealed class CrossPageRequestAdmissionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReject_WhenCrossPageInactive()
    {
        var decision = CrossPageDisplayUpdatePolicies.ResolveCrossPageRequestAdmission(
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
        var decision = CrossPageDisplayUpdatePolicies.ResolveCrossPageRequestAdmission(
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
        var decision = CrossPageDisplayUpdatePolicies.ResolveCrossPageRequestAdmission(
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
        var decision = CrossPageDisplayUpdatePolicies.ResolveCrossPageRequestAdmission(
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
        var decision = CrossPageDisplayUpdatePolicies.ResolveCrossPageRequestAdmission(
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
        var decision = CrossPageDisplayUpdatePolicies.ResolveCrossPageRequestAdmission(
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
        var decision = CrossPageDisplayUpdatePolicies.ResolveCrossPageRequestAdmission(
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

public sealed class CrossPageUpdateReplayPolicyTests
{
    [Theory]
    [InlineData("Interaction", true)]
    [InlineData("VisualSync", true)]
    [InlineData("BackgroundRefresh", false)]
    public void ShouldQueueReplay_ShouldMatchSourceKind(
        string kindName,
        bool expected)
    {
        var kind = Enum.Parse<CrossPageUpdateSourceKind>(kindName);
        CrossPageDisplayUpdatePolicies.ShouldQueueReplay(kind).Should().Be(expected);
    }

    [Fact]
    public void ShouldFlushReplay_ShouldReturnTrue_OnlyWhenRuntimeIsFlushable()
    {
        CrossPageDisplayUpdatePolicies.ShouldFlushReplay(
                replayPending: true,
                crossPageUpdatePending: false,
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                interactionActive: false)
            .Should()
            .BeTrue();

        CrossPageDisplayUpdatePolicies.ShouldFlushReplay(
                replayPending: false,
                crossPageUpdatePending: false,
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                interactionActive: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldFlushReplay_ShouldReturnFalse_WhenInteractionIsActive()
    {
        CrossPageDisplayUpdatePolicies.ShouldFlushReplay(
                replayPending: true,
                crossPageUpdatePending: false,
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                interactionActive: true)
            .Should()
            .BeFalse();
    }

    [Theory]
    [InlineData(CrossPageUpdateSources.InkVisualSyncReplay, true)]
    [InlineData(CrossPageUpdateSources.InteractionReplay, true)]
    [InlineData(CrossPageUpdateSources.PhotoPan, false)]
    public void IsReplayBaseSource_ShouldMatchExpected(string source, bool expected)
    {
        CrossPageDisplayUpdatePolicies.IsReplayBaseSource(source).Should().Be(expected);
    }
}
