using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoTouchInputContractTests
{
    [Fact]
    public void PhotoTouchInput_ShouldRegisterTouchHandlers_AndIgnorePromotedStylusTouch()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow*.cs");

        source.Should().Contain("OverlayRoot.TouchDown += OnTouchDown;");
        source.Should().Contain("OverlayRoot.TouchMove += OnTouchMove;");
        source.Should().Contain("OverlayRoot.TouchUp += OnTouchUp;");
        source.Should().Contain("OverlayRoot.LostTouchCapture += OnOverlayLostTouchCapture;");
        source.Should().Contain("PhotoTouchInteractionPolicy.ShouldIgnorePromotedTouchStylus");
        source.Should().Contain("PhotoManipulationAdmissionPolicy.Resolve(");
        source.Should().Contain("ManipulationStarting");
        source.Should().Contain("ManipulationDelta");
    }

    [Fact]
    public void PhotoTouchInput_ShouldGatePhotoControlsBeforeTouchCaptureStateChanges()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Input.Touch.cs"));

        var gateIndex = source.IndexOf("if (!ShouldContinuePointerInput(e))", StringComparison.Ordinal);
        var touchStateIndex = source.IndexOf("_photoActiveTouchIds.Add", StringComparison.Ordinal);

        gateIndex.Should().BeGreaterThanOrEqualTo(0);
        touchStateIndex.Should().BeGreaterThan(gateIndex);
    }

    [Fact]
    public void PhotoTouchInput_ShouldLeaveTouchDownUnhandledForWpfManipulation()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Input.Touch.cs"));

        source.Should().NotContain("OverlayRoot.CaptureTouch(e.TouchDevice)");
        source.Should().NotContain("e.Handled = true");
        source.Should().Contain("ManipulationStarting/Delta");
    }

    [Fact]
    public void PhotoTouchInput_ShouldBlockPromotedTouchMouseBeforeInkOrPhotoRouting()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Input.Mouse.cs"));

        source.Should().Contain("ShouldContinueMouseInput(e)");
        source.Should().Contain("e.StylusDevice.TabletDevice.Type");
        source.Should().Contain("PhotoTouchInteractionPolicy.ShouldIgnorePromotedTouchStylus");
        source.Should().Contain("e.Handled = true;");
    }
}
