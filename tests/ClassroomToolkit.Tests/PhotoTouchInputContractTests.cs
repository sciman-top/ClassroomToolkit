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
        source.Should().Contain("PhotoTouchInteractionPolicy.ShouldUseSingleTouchPan(");
        source.Should().Contain("BeginPhotoPan(");
        source.Should().Contain("PhotoPanPointerKind.Touch");
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
}
