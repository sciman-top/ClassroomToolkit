using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoBackgroundVisibilityPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnVisible_WhenPhotoModeOn_BoardOff_AndSourceExists()
    {
        var visibility = PhotoWindowPolicies.ResolvePhotoBackgroundVisibility(
            photoModeActive: true,
            boardActive: false,
            hasBackgroundSource: true);

        visibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Resolve_ShouldReturnCollapsed_WhenBoardIsActive()
    {
        var visibility = PhotoWindowPolicies.ResolvePhotoBackgroundVisibility(
            photoModeActive: true,
            boardActive: true,
            hasBackgroundSource: true);

        visibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Resolve_ShouldReturnCollapsed_WhenSourceMissing()
    {
        var visibility = PhotoWindowPolicies.ResolvePhotoBackgroundVisibility(
            photoModeActive: true,
            boardActive: false,
            hasBackgroundSource: false);

        visibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Resolve_ShouldReturnCollapsed_WhenPhotoModeDisabled()
    {
        var visibility = PhotoWindowPolicies.ResolvePhotoBackgroundVisibility(
            photoModeActive: false,
            boardActive: false,
            hasBackgroundSource: true);

        visibility.Should().Be(Visibility.Collapsed);
    }
}

public sealed class PhotoContentTransformPolicyTests
{
    [Fact]
    public void ShouldApplyPhotoTransform_ShouldReturnFalse_WhenAllConditionsMet()
    {
        PhotoWindowPolicies.ShouldApplyPhotoTransform(
            enabledRequested: true,
            photoModeActive: true,
            boardActive: false,
            transformAvailable: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyPhotoTransform_ShouldReturnFalse_WhenBoardIsActive()
    {
        PhotoWindowPolicies.ShouldApplyPhotoTransform(
            enabledRequested: true,
            photoModeActive: true,
            boardActive: true,
            transformAvailable: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyPhotoTransform_ShouldReturnFalse_WhenTransformUnavailable()
    {
        PhotoWindowPolicies.ShouldApplyPhotoTransform(
            enabledRequested: true,
            photoModeActive: true,
            boardActive: false,
            transformAvailable: false).Should().BeFalse();
    }
}

public sealed class PhotoCrossPageSequencePolicyTests
{
    [Fact]
    public void Normalize_ShouldKeepOnlyImages_AndMapCurrentIndexByPath()
    {
        var sequence = new[]
        {
            @"E:\a.pdf",
            @"E:\b.png",
            @"E:\c.jpg"
        };

        var (normalized, index) = PhotoWindowPolicies.Normalize(sequence, currentIndex: 1);

        normalized.Should().Equal(@"E:\b.png", @"E:\c.jpg");
        index.Should().Be(0);
    }

    [Fact]
    public void Normalize_ShouldReturnEmpty_WhenNoImageInSequence()
    {
        var sequence = new[]
        {
            @"E:\a.pdf",
            @"E:\b.pdf"
        };

        var (normalized, index) = PhotoWindowPolicies.Normalize(sequence, currentIndex: 0);

        normalized.Should().BeEmpty();
        index.Should().Be(-1);
    }
}

public sealed class PhotoInteractionModePolicyTests
{
    [Fact]
    public void IsPhotoNavigationEnabled_ShouldMatchPhotoModeAndBoardState()
    {
        PhotoWindowPolicies.IsPhotoNavigationEnabled(
            photoModeActive: true,
            boardActive: false).Should().BeTrue();
        PhotoWindowPolicies.IsPhotoNavigationEnabled(
            photoModeActive: true,
            boardActive: true).Should().BeFalse();
        PhotoWindowPolicies.IsPhotoNavigationEnabled(
            photoModeActive: false,
            boardActive: false).Should().BeFalse();
    }

    [Fact]
    public void IsPhotoTransformEnabled_ShouldMatchPhotoModeAndBoardState()
    {
        PhotoWindowPolicies.IsPhotoTransformEnabled(
            photoModeActive: true,
            boardActive: false).Should().BeTrue();
        PhotoWindowPolicies.IsPhotoTransformEnabled(
            photoModeActive: true,
            boardActive: true).Should().BeFalse();
        PhotoWindowPolicies.IsPhotoTransformEnabled(
            photoModeActive: false,
            boardActive: false).Should().BeFalse();
    }

    [Fact]
    public void IsPhotoOrBoardActive_ShouldReturnTrue_WhenEitherIsActive()
    {
        PhotoWindowPolicies.IsPhotoOrBoardActive(
            photoModeActive: true,
            boardActive: false).Should().BeTrue();
        PhotoWindowPolicies.IsPhotoOrBoardActive(
            photoModeActive: false,
            boardActive: true).Should().BeTrue();
        PhotoWindowPolicies.IsPhotoOrBoardActive(
            photoModeActive: false,
            boardActive: false).Should().BeFalse();
    }

    [Fact]
    public void IsCrossPageDisplayActive_ShouldRequireCrossPageAndPhotoTransformEnabled()
    {
        PhotoWindowPolicies.IsCrossPageDisplayActive(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true).Should().BeTrue();

        PhotoWindowPolicies.IsCrossPageDisplayActive(
            photoModeActive: true,
            boardActive: true,
            crossPageDisplayEnabled: true).Should().BeFalse();

        PhotoWindowPolicies.IsCrossPageDisplayActive(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: false).Should().BeFalse();
    }
}

public sealed class PhotoRightButtonDownExecutionPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, true)]
    public void Resolve_ShouldMatchExpected(
        bool shouldArmPending,
        bool shouldAllowPan,
        bool expectedArmPending,
        bool expectedTryBeginPan)
    {
        var plan = PhotoWindowPolicies.ResolvePhotoRightButtonDownExecution(
            shouldArmPending,
            shouldAllowPan);

        plan.ShouldArmPending.Should().Be(expectedArmPending);
        plan.ShouldTryBeginPan.Should().Be(expectedTryBeginPan);
    }
}

public sealed class PhotoRightButtonUpExecutionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnPassThrough_WhenContextMenuShouldNotShow()
    {
        var plan = PhotoWindowPolicies.ResolvePhotoRightButtonUpExecution(shouldShowContextMenuOnUp: false);

        plan.Should().Be(new PhotoRightButtonUpExecutionPlan(
            Action: PhotoRightButtonUpAction.PassThrough,
            ShouldMarkHandled: false,
            ShouldClearPending: false));
    }

    [Fact]
    public void Resolve_ShouldReturnShowContextMenuPlan_WhenContextMenuShouldShow()
    {
        var plan = PhotoWindowPolicies.ResolvePhotoRightButtonUpExecution(shouldShowContextMenuOnUp: true);

        plan.Should().Be(new PhotoRightButtonUpExecutionPlan(
            Action: PhotoRightButtonUpAction.ShowContextMenu,
            ShouldMarkHandled: true,
            ShouldClearPending: true));
    }
}

public sealed class PhotoRightClickContextMenuPolicyTests
{
    [Fact]
    public void ShouldArmPending_ShouldReturnTrue_WhenPhotoFullscreenCursor()
    {
        PhotoWindowPolicies.ShouldArmPending(
                photoModeActive: true,
                photoFullscreen: true,
                mode: PaintToolMode.Cursor)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldArmPending_ShouldReturnFalse_WhenModeOrStateMismatch()
    {
        PhotoWindowPolicies.ShouldArmPending(
                photoModeActive: false,
                photoFullscreen: true,
                mode: PaintToolMode.Cursor)
            .Should()
            .BeFalse();

        PhotoWindowPolicies.ShouldArmPending(
                photoModeActive: true,
                photoFullscreen: false,
                mode: PaintToolMode.Cursor)
            .Should()
            .BeFalse();

        PhotoWindowPolicies.ShouldArmPending(
                photoModeActive: true,
                photoFullscreen: true,
                mode: PaintToolMode.Brush)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldCancelPendingByMove_ShouldUseThresholdDip()
    {
        var threshold = PhotoRightClickContextMenuDefaults.CancelMoveThresholdDip;
        PhotoWindowPolicies.ShouldCancelPendingByMove(new Vector(threshold - 1, 0))
            .Should()
            .BeFalse();

        PhotoWindowPolicies.ShouldCancelPendingByMove(new Vector(threshold + 1, 0))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldShowContextMenuOnUp_ShouldRequirePendingAndArmState()
    {
        PhotoWindowPolicies.ShouldShowContextMenuOnUp(
                rightClickPending: true,
                photoModeActive: true,
                photoFullscreen: true,
                mode: PaintToolMode.Cursor)
            .Should()
            .BeTrue();

        PhotoWindowPolicies.ShouldShowContextMenuOnUp(
                rightClickPending: false,
                photoModeActive: true,
                photoFullscreen: true,
                mode: PaintToolMode.Cursor)
            .Should()
            .BeFalse();
    }
}

public sealed class PhotoTitleBarDragZOrderPolicyTests
{
    [Fact]
    public void Resolve_ShouldAllowDragAndRequestSingleNonForcedRetouch_WhenLeftButtonDragInWindowedPhotoMode()
    {
        var plan = PhotoWindowPolicies.ResolvePhotoTitleBarDragZOrder(
            photoModeActive: true,
            photoFullscreen: false,
            changedButton: MouseButton.Left);

        plan.CanDrag.Should().BeTrue();
        plan.RequestZOrderBeforeDrag.Should().BeFalse();
        plan.RequestZOrderAfterDrag.Should().BeTrue();
        plan.ForceAfterDrag.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, MouseButton.Left)]
    [InlineData(true, true, MouseButton.Left)]
    [InlineData(true, false, MouseButton.Right)]
    public void Resolve_ShouldBlockDrag_WhenPreconditionsNotMet(
        bool photoModeActive,
        bool photoFullscreen,
        MouseButton changedButton)
    {
        var plan = PhotoWindowPolicies.ResolvePhotoTitleBarDragZOrder(
            photoModeActive,
            photoFullscreen,
            changedButton);

        plan.CanDrag.Should().BeFalse();
        plan.RequestZOrderBeforeDrag.Should().BeFalse();
        plan.RequestZOrderAfterDrag.Should().BeFalse();
        plan.ForceAfterDrag.Should().BeFalse();
    }
}


public sealed class PhotoTouchInteractionPolicyTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void ShouldUseManipulation_ShouldRequireAtLeastOneTouch(
        int activeTouchCount,
        bool expected)
    {
        PhotoWindowPolicies.ShouldUseManipulation(activeTouchCount).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void ShouldUseManipulationZoom_ShouldRequireTwoTouches(
        int activeTouchCount,
        bool expected)
    {
        PhotoWindowPolicies.ShouldUseManipulationZoom(activeTouchCount).Should().Be(expected);
    }

    [Theory]
    [InlineData(TabletDeviceType.Touch, true)]
    [InlineData(TabletDeviceType.Stylus, false)]
    public void ShouldIgnorePromotedTouchStylus_ShouldMatchExpected(
        TabletDeviceType tabletDeviceType,
        bool expected)
    {
        PhotoWindowPolicies.ShouldIgnorePromotedTouchStylus(tabletDeviceType).Should().Be(expected);
    }
}

public sealed class PhotoWindowModeZOrderRetouchPolicyTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldRequest_ShouldRequireActivePhotoModeAndFullscreenStateChange(
        bool photoModeActive,
        bool fullscreenChanged,
        bool expected)
    {
        PhotoWindowPolicies.ShouldRequest(photoModeActive, fullscreenChanged).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldForceEnforce_ShouldFollowCurrentFullscreen(bool fullscreen, bool expected)
    {
        PhotoWindowPolicies.ShouldForceEnforce(fullscreen).Should().Be(expected);
    }
}

public sealed class PhotoWindowStateRestorePolicyTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldArmFullscreenRestore_ShouldFollowCurrentPhotoFullscreen(bool photoFullscreen, bool expected)
    {
        PhotoWindowPolicies.ShouldArmFullscreenRestore(photoFullscreen).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, WindowState.Normal, true)]
    [InlineData(true, WindowState.Maximized, true)]
    [InlineData(true, WindowState.Minimized, false)]
    [InlineData(false, WindowState.Normal, false)]
    [InlineData(false, WindowState.Maximized, false)]
    [InlineData(false, WindowState.Minimized, false)]
    public void ShouldRestoreFullscreen_ShouldRequirePendingFlagAndNonMinimizedWindow(
        bool pendingRestore,
        WindowState windowState,
        bool expected)
    {
        PhotoWindowPolicies.ShouldRestoreFullscreen(pendingRestore, windowState).Should().Be(expected);
    }
}
