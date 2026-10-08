using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using Xunit;

namespace ClassroomToolkit.Tests.Paint;

public sealed class InputInteractionStatePolicyTests
{
    [Fact]
    public void Resolve_ShouldExposePhotoAndBoardState()
    {
        var state = PointerToolExecutionPolicies.ResolveInputInteractionState(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true);

        state.PhotoModeActive.Should().BeTrue();
        state.BoardActive.Should().BeFalse();
        state.CrossPageDisplayEnabled.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldProjectPhotoOrBoardActive()
    {
        var state = PointerToolExecutionPolicies.ResolveInputInteractionState(
            photoModeActive: false,
            boardActive: true,
            crossPageDisplayEnabled: true);

        state.PhotoOrBoardActive.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldProjectPhotoNavigationEnabled()
    {
        var enabled = PointerToolExecutionPolicies.ResolveInputInteractionState(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true);
        var disabled = PointerToolExecutionPolicies.ResolveInputInteractionState(
            photoModeActive: true,
            boardActive: true,
            crossPageDisplayEnabled: true);

        enabled.PhotoNavigationEnabled.Should().BeTrue();
        disabled.PhotoNavigationEnabled.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldProjectCrossPageInputDisplayActive()
    {
        var active = PointerToolExecutionPolicies.ResolveInputInteractionState(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true);
        var inactive = PointerToolExecutionPolicies.ResolveInputInteractionState(
            photoModeActive: true,
            boardActive: true,
            crossPageDisplayEnabled: true);

        active.CrossPageDisplayActive.Should().BeTrue();
        inactive.CrossPageDisplayActive.Should().BeFalse();
        active.CrossPageInputDisplayActive.Should().BeTrue();
        inactive.CrossPageInputDisplayActive.Should().BeFalse();
        active.CrossPageInputDisplayActive.Should().Be(active.CrossPageDisplayActive);
        inactive.CrossPageInputDisplayActive.Should().Be(inactive.CrossPageDisplayActive);
    }
}

public sealed class PointerCaptureCleanupPolicyTests
{
    [Theory]
    [InlineData("mouse-capture-lost", false, true, true)]
    [InlineData("stylus-capture-lost", true, false, true)]
    [InlineData("mouse-capture-lost", false, false, false)]
    [InlineData("stylus-capture-lost", false, false, false)]
    [InlineData("overlay-deactivated", true, true, false)]
    [InlineData("overlay-closed", true, true, false)]
    public void ShouldDeferCleanup_ShouldWaitOnlyForPartialPointerCaptureLoss(
        string reason,
        bool mouseCaptured,
        bool stylusCaptured,
        bool expected)
    {
        PointerToolExecutionPolicies.ShouldDeferCleanup(reason, mouseCaptured, stylusCaptured)
            .Should().Be(expected);
    }
}

public sealed class PointerDownToolExecutionPolicyTests
{
    [Theory]
    [InlineData((int)PaintToolMode.Brush, (int)PointerDownToolAction.BeginBrushStroke, true)]
    [InlineData((int)PaintToolMode.Eraser, (int)PointerDownToolAction.BeginEraser, true)]
    [InlineData((int)PaintToolMode.RegionErase, (int)PointerDownToolAction.BeginRegionSelection, true)]
    [InlineData((int)PaintToolMode.Shape, (int)PointerDownToolAction.BeginShape, true)]
    [InlineData((int)PaintToolMode.Cursor, (int)PointerDownToolAction.None, false)]
    public void Resolve_ShouldReturnExpectedPlan(int mode, int expectedAction, bool shouldCapturePointer)
    {
        var plan = PointerToolExecutionPolicies.ResolvePointerDownToolExecution((PaintToolMode)mode);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldCapturePointer.Should().Be(shouldCapturePointer);
    }
}

public sealed class PointerMoveToolExecutionPolicyTests
{
    [Theory]
    [InlineData((int)PaintToolMode.Brush, (int)PointerMoveToolAction.UpdateBrushStroke)]
    [InlineData((int)PaintToolMode.Eraser, (int)PointerMoveToolAction.UpdateEraser)]
    [InlineData((int)PaintToolMode.RegionErase, (int)PointerMoveToolAction.UpdateRegionSelection)]
    [InlineData((int)PaintToolMode.Shape, (int)PointerMoveToolAction.UpdateShapePreview)]
    [InlineData((int)PaintToolMode.Cursor, (int)PointerMoveToolAction.None)]
    public void Resolve_ShouldReturnExpectedAction(int mode, int expectedAction)
    {
        var action = PointerToolExecutionPolicies.ResolvePointerMoveToolExecution((PaintToolMode)mode);

        ((int)action).Should().Be(expectedAction);
    }
}

public sealed class PointerUpToolExecutionPolicyTests
{
    [Theory]
    [InlineData((int)PaintToolMode.Brush, true, (int)PointerUpToolAction.EndBrushStroke, true)]
    [InlineData((int)PaintToolMode.Brush, false, (int)PointerUpToolAction.EndBrushStroke, false)]
    [InlineData((int)PaintToolMode.Eraser, true, (int)PointerUpToolAction.EndEraser, false)]
    [InlineData((int)PaintToolMode.RegionErase, false, (int)PointerUpToolAction.EndRegionSelection, false)]
    [InlineData((int)PaintToolMode.Shape, false, (int)PointerUpToolAction.EndShape, false)]
    [InlineData((int)PaintToolMode.Cursor, true, (int)PointerUpToolAction.None, false)]
    public void Resolve_ShouldReturnExpectedPlan(
        int mode,
        bool pendingAdaptiveRendererRefresh,
        int expectedAction,
        bool expectedRefresh)
    {
        var plan = PointerToolExecutionPolicies.ResolvePointerUpToolExecution(
            (PaintToolMode)mode,
            pendingAdaptiveRendererRefresh);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldRefreshAdaptiveRenderer.Should().Be(expectedRefresh);
    }
}
