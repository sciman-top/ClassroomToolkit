using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace ClassroomToolkit.Tests.Paint;

public sealed class PhotoInkCurrentPageClipPolicyTests
{
    [Fact]
    public void ResolveBounds_ShouldReturnPageBounds_WhenCrossPagePhotoInkTransformIsActive()
    {
        var rect = PhotoInkInteropPolicies.ResolveBoundsPhotoInkCurrentPageClip(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            usePhotoTransform: true,
            currentPageScreenRect: Rect.Empty,
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(new Rect(0, 0, 1280, 720));
    }

    [Fact]
    public void ResolveBounds_ShouldReturnScreenRect_WhenInkRendersInScreenSpace()
    {
        var rect = PhotoInkInteropPolicies.ResolveBoundsPhotoInkCurrentPageClip(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            usePhotoTransform: false,
            currentPageScreenRect: new Rect(40, 60, 800, 450),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(new Rect(40, 60, 800, 450));
    }

    [Fact]
    public void ResolveBounds_ShouldReturnEmpty_WhenCrossPageDisplayIsInactive()
    {
        var rect = PhotoInkInteropPolicies.ResolveBoundsPhotoInkCurrentPageClip(
            photoInkModeActive: true,
            crossPageDisplayActive: false,
            photoFullscreenActive: false,
            usePhotoTransform: false,
            currentPageScreenRect: new Rect(40, 60, 800, 450),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }

    [Fact]
    public void ResolveBounds_ShouldReturnEmpty_WhenPageBoundsAreInvalid()
    {
        var rect = PhotoInkInteropPolicies.ResolveBoundsPhotoInkCurrentPageClip(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            usePhotoTransform: true,
            currentPageScreenRect: Rect.Empty,
            pageWidthDip: 0,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }

    [Fact]
    public void ResolveBounds_ShouldReturnEmpty_WhenScreenRectIsInvalid()
    {
        var rect = PhotoInkInteropPolicies.ResolveBoundsPhotoInkCurrentPageClip(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            usePhotoTransform: false,
            currentPageScreenRect: Rect.Empty,
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }

    [Fact]
    public void ResolveBounds_ShouldReturnEmpty_WhenPhotoFullscreenIsActive()
    {
        var rect = PhotoInkInteropPolicies.ResolveBoundsPhotoInkCurrentPageClip(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: true,
            usePhotoTransform: false,
            currentPageScreenRect: new Rect(40, 60, 800, 450),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }
}

public sealed class PhotoInkPanCompensationGeometryPolicyTests
{
    [Fact]
    public void ShouldApplyCompensation_ShouldReturnTrue_WhenPhotoInkUsesPanCompensationAndOffsetExists()
    {
        var panCompensation = new TranslateTransform(12.5, -8.0);

        var shouldApply = PhotoInkInteropPolicies.ShouldApplyCompensation(
            photoInkModeActive: true,
            rasterRenderTransform: panCompensation,
            panCompensation);

        shouldApply.Should().BeTrue();
    }

    [Fact]
    public void ShouldApplyCompensation_ShouldReturnFalse_WhenCompensationOffsetIsZero()
    {
        var panCompensation = new TranslateTransform(0, 0);

        var shouldApply = PhotoInkInteropPolicies.ShouldApplyCompensation(
            photoInkModeActive: true,
            rasterRenderTransform: panCompensation,
            panCompensation);

        shouldApply.Should().BeFalse();
    }

    [Fact]
    public void AdjustToRasterSpace_ShouldTranslateGeometryByInverseCompensation()
    {
        var geometry = new RectangleGeometry(new Rect(20, 30, 40, 50));

        var adjusted = PhotoInkInteropPolicies.AdjustToRasterSpace(
            geometry,
            panCompensationX: 6,
            panCompensationY: -4);

        adjusted.Bounds.X.Should().BeApproximately(14, 0.001);
        adjusted.Bounds.Y.Should().BeApproximately(34, 0.001);
        adjusted.Bounds.Width.Should().BeApproximately(40, 0.001);
        adjusted.Bounds.Height.Should().BeApproximately(50, 0.001);
    }
}

public sealed class PhotoInkPanCompensationPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnZero_WhenPhotoInkModeInactive()
    {
        var delta = PhotoInkInteropPolicies.ResolvePhotoInkPanCompensation(
            photoInkModeActive: false,
            currentTranslateX: 120,
            currentTranslateY: -40,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: -10);

        delta.Should().Be(new Vector(0, 0));
    }

    [Fact]
    public void Resolve_ShouldReturnTranslateDelta_WhenPhotoInkModeActive()
    {
        var delta = PhotoInkInteropPolicies.ResolvePhotoInkPanCompensation(
            photoInkModeActive: true,
            currentTranslateX: 120,
            currentTranslateY: -40,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: -10);

        delta.Should().Be(new Vector(20, -30));
    }
}

public sealed class PhotoInkPanRedrawPolicyTests
{
    [Fact]
    public void ShouldRequest_ShouldReturnFalse_WhenPhotoInkModeInactive()
    {
        var shouldRequest = PhotoInkInteropPolicies.ShouldRequest(
            photoInkModeActive: false,
            currentTranslateX: 120,
            currentTranslateY: 210,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: 200);

        shouldRequest.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequest_ShouldReturnFalse_WhenMovementBelowThreshold()
    {
        var shouldRequest = PhotoInkInteropPolicies.ShouldRequest(
            photoInkModeActive: true,
            currentTranslateX: 107,
            currentTranslateY: 200,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: 200,
            thresholdDip: 8);

        shouldRequest.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequest_ShouldReturnTrue_WhenMovementReachesThreshold()
    {
        var shouldRequest = PhotoInkInteropPolicies.ShouldRequest(
            photoInkModeActive: true,
            currentTranslateX: 108,
            currentTranslateY: 200,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: 200,
            thresholdDip: 8);

        shouldRequest.Should().BeTrue();
    }

    [Fact]
    public void ShouldRequest_ShouldUseResponsiveDefaultThreshold_ForPhotoPan()
    {
        var shouldRequest = PhotoInkInteropPolicies.ShouldRequest(
            photoInkModeActive: true,
            currentTranslateX: 106,
            currentTranslateY: 200,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: 200);

        shouldRequest.Should().BeTrue();
    }

    [Fact]
    public void ShouldRequest_ShouldUseResponsiveDefaultThreshold_ForSmallerPhotoPanDelta()
    {
        var shouldRequest = PhotoInkInteropPolicies.ShouldRequest(
            photoInkModeActive: true,
            currentTranslateX: 103.2,
            currentTranslateY: 200,
            lastRedrawTranslateX: 100,
            lastRedrawTranslateY: 200);

        shouldRequest.Should().BeTrue();
    }
}

public sealed class PhotoInkPreviewClipPolicyTests
{
    [Fact]
    public void ResolveBounds_ShouldReturnScreenRect_WhenPhotoTransformIsActiveAndScreenRectIsValid()
    {
        var rect = PhotoInkInteropPolicies.ResolveBoundsPhotoInkPreviewClip(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            usePhotoTransform: true,
            currentPageScreenRect: new Rect(32, 128, 960, 540),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(new Rect(32, 128, 960, 540));
    }

    [Fact]
    public void ResolveBounds_ShouldFallbackToPageBounds_WhenScreenRectIsUnavailable()
    {
        var rect = PhotoInkInteropPolicies.ResolveBoundsPhotoInkPreviewClip(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            usePhotoTransform: true,
            currentPageScreenRect: Rect.Empty,
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(new Rect(0, 0, 1280, 720));
    }

    [Fact]
    public void ResolveBounds_ShouldReturnEmpty_WhenCrossPageDisplayIsInactive()
    {
        var rect = PhotoInkInteropPolicies.ResolveBoundsPhotoInkPreviewClip(
            photoInkModeActive: true,
            crossPageDisplayActive: false,
            photoFullscreenActive: false,
            usePhotoTransform: true,
            currentPageScreenRect: new Rect(32, 128, 960, 540),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }

    [Fact]
    public void ResolveBounds_ShouldReturnEmpty_WhenPhotoFullscreenIsActive()
    {
        var rect = PhotoInkInteropPolicies.ResolveBoundsPhotoInkPreviewClip(
            photoInkModeActive: true,
            crossPageDisplayActive: true,
            photoFullscreenActive: true,
            usePhotoTransform: true,
            currentPageScreenRect: new Rect(32, 128, 960, 540),
            pageWidthDip: 1280,
            pageHeightDip: 720);

        rect.Should().Be(Rect.Empty);
    }
}

public sealed class PhotoInkRenderPolicyTests
{
    [Fact]
    public void ShouldRequestImmediateRedraw_ShouldReturnFalse_WhenPhotoModeUsesPhotoTransform()
    {
        var photoTransform = new TransformGroup();
        var rasterTransform = photoTransform;

        var result = PhotoInkInteropPolicies.ShouldRequestImmediateRedraw(
            photoModeActive: true,
            rasterRenderTransform: rasterTransform,
            photoContentTransform: photoTransform,
            crossPageBrushContinuationActive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequestImmediateRedraw_ShouldReturnFalse_WhenNotPhotoMode()
    {
        var photoTransform = new TransformGroup();
        var rasterTransform = photoTransform;

        var result = PhotoInkInteropPolicies.ShouldRequestImmediateRedraw(
            photoModeActive: false,
            rasterRenderTransform: rasterTransform,
            photoContentTransform: photoTransform,
            crossPageBrushContinuationActive: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequestImmediateRedraw_ShouldReturnFalse_WhenRasterNotBoundToPhotoTransform()
    {
        var photoTransform = new TransformGroup();
        var rasterTransform = Transform.Identity;

        var result = PhotoInkInteropPolicies.ShouldRequestImmediateRedraw(
            photoModeActive: true,
            rasterRenderTransform: rasterTransform,
            photoContentTransform: photoTransform,
            crossPageBrushContinuationActive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequestImmediateRedraw_ShouldReturnTrue_WhenPhotoModeActiveAndCrossPageContinuationActive()
    {
        var photoTransform = new TransformGroup();
        var rasterTransform = Transform.Identity;

        var result = PhotoInkInteropPolicies.ShouldRequestImmediateRedraw(
            photoModeActive: true,
            rasterRenderTransform: rasterTransform,
            photoContentTransform: photoTransform,
            crossPageBrushContinuationActive: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRenderInteractiveInkInPhotoSpace_ShouldReturnFalse_WhenPhotoModeUsesPhotoTransform()
    {
        var photoTransform = new TransformGroup();
        var rasterTransform = photoTransform;

        var result = PhotoInkInteropPolicies.ShouldRenderInteractiveInkInPhotoSpace(
            photoModeActive: true,
            rasterRenderTransform: rasterTransform,
            photoContentTransform: photoTransform);

        result.Should().BeFalse();
    }
}

public sealed class PhotoInkViewportIntersectionPolicyTests
{
    [Fact]
    public void ShouldRender_WhenPhotoTransformMovesStrokeIntoViewport()
    {
        var strokeBounds = new Rect(0, 1500, 120, 80);
        var viewport = new Rect(0, 0, 1920, 1080);
        var matrix = Matrix.Identity;
        matrix.Translate(0, -1200);

        var result = PhotoInkInteropPolicies.ShouldRender(
            photoInkModeActive: true,
            usePhotoTransform: true,
            strokeBounds: strokeBounds,
            photoTransformMatrix: matrix,
            viewportBounds: viewport);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRender_WhenPhotoTransformKeepsStrokeOutsideViewport_ShouldReturnFalse()
    {
        var strokeBounds = new Rect(0, 2600, 120, 80);
        var viewport = new Rect(0, 0, 1920, 1080);
        var matrix = Matrix.Identity;
        matrix.Translate(0, -1200);

        var result = PhotoInkInteropPolicies.ShouldRender(
            photoInkModeActive: true,
            usePhotoTransform: true,
            strokeBounds: strokeBounds,
            photoTransformMatrix: matrix,
            viewportBounds: viewport);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRender_WhenNotUsingPhotoTransform_ShouldUseRawBounds()
    {
        var strokeBounds = new Rect(0, 1500, 120, 80);
        var viewport = new Rect(0, 0, 1920, 1080);
        var matrix = Matrix.Identity;
        matrix.Translate(0, -1200);

        var result = PhotoInkInteropPolicies.ShouldRender(
            photoInkModeActive: true,
            usePhotoTransform: false,
            strokeBounds: strokeBounds,
            photoTransformMatrix: matrix,
            viewportBounds: viewport);

        result.Should().BeFalse();
    }
}

public sealed class PhotoNavigationInkLoadTranslatePolicyTests
{
    [Fact]
    public void ResolveTranslateYBeforeLoad_ShouldUseTargetTranslate_WhenCrossPageInkPageChanged()
    {
        var translateY = PhotoInkInteropPolicies.ResolveTranslateYBeforeLoad(
            currentTranslateY: 120,
            targetTranslateY: -860,
            pageChanged: true,
            photoInkModeActive: true,
            crossPageDisplayActive: true);

        translateY.Should().Be(-860);
    }

    [Fact]
    public void ResolveTranslateYBeforeLoad_ShouldKeepCurrentTranslate_WhenNotCrossPageDisplay()
    {
        var translateY = PhotoInkInteropPolicies.ResolveTranslateYBeforeLoad(
            currentTranslateY: 120,
            targetTranslateY: -860,
            pageChanged: true,
            photoInkModeActive: true,
            crossPageDisplayActive: false);

        translateY.Should().Be(120);
    }

    [Fact]
    public void ResolveTranslateYBeforeLoad_ShouldKeepCurrentTranslate_WhenPageNotChanged()
    {
        var translateY = PhotoInkInteropPolicies.ResolveTranslateYBeforeLoad(
            currentTranslateY: 120,
            targetTranslateY: -860,
            pageChanged: false,
            photoInkModeActive: true,
            crossPageDisplayActive: true);

        translateY.Should().Be(120);
    }
}

public sealed class PhotoNavigationInkViewportSyncPolicyTests
{
    [Fact]
    public void ResolveAction_ShouldReturnUpdatePanCompensation_WhenPhotoInkModeActiveAndInteractiveSwitch()
    {
        var action = PhotoInkInteropPolicies.ResolveAction(
            photoInkModeActive: true,
            interactiveSwitch: true);

        action.Should().Be(PhotoNavigationInkViewportSyncAction.UpdatePanCompensation);
    }

    [Fact]
    public void ResolveAction_ShouldReturnResetPanCompensation_WhenPhotoInkModeActiveAndNonInteractiveSwitch()
    {
        var action = PhotoInkInteropPolicies.ResolveAction(
            photoInkModeActive: true,
            interactiveSwitch: false);

        action.Should().Be(PhotoNavigationInkViewportSyncAction.ResetPanCompensation);
    }

    [Fact]
    public void ResolveAction_ShouldReturnNone_WhenPhotoInkModeInactive()
    {
        var action = PhotoInkInteropPolicies.ResolveAction(
            photoInkModeActive: false,
            interactiveSwitch: true);

        action.Should().Be(PhotoNavigationInkViewportSyncAction.None);
    }
}
