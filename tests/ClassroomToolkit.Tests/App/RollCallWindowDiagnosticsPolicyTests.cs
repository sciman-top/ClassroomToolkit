using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallWindowDiagnosticsPolicyTests
{
    [Fact]
    public void FormatInitializationFailureMessage_ShouldContainExceptionAndMessage()
    {
        var message = RollCallWindowDiagnosticsPolicy.FormatInitializationFailureMessage(
            "InvalidOperationException",
            "init failed");

        message.Should().Contain("[RollCallWindow] initialization-failed");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=init failed");
    }

    [Fact]
    public void FormatDragMoveFailureMessage_ShouldContainExceptionAndMessage()
    {
        var message = RollCallWindowDiagnosticsPolicy.FormatDragMoveFailureMessage(
            "InvalidOperationException",
            "drag failed");

        message.Should().Contain("[RollCallWindow] drag-move-failed");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=drag failed");
    }

    [Fact]
    public void FormatPhotoOverlayCloseFailureMessage_ShouldContainOperationAndException()
    {
        var message = RollCallWindowDiagnosticsPolicy.FormatPhotoOverlayCloseFailureMessage(
            "close-window",
            "InvalidOperationException",
            "close failed");

        message.Should().Contain("[RollCallWindow] photo-overlay-close-failed");
        message.Should().Contain("op=close-window");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=close failed");
    }

    [Fact]
    public void FormatPhotoOverlayCloseFailureMessage_ShouldSupportHideOverlayOperation()
    {
        var message = RollCallWindowDiagnosticsPolicy.FormatPhotoOverlayCloseFailureMessage(
            "hide-overlay",
            "ObjectDisposedException",
            "disposed");

        message.Should().Contain("op=hide-overlay");
        message.Should().Contain("ex=ObjectDisposedException");
        message.Should().Contain("msg=disposed");
    }

    [Fact]
    public void FormatWindowLifecycleFailureMessage_ShouldContainOperationAndException()
    {
        var message = RollCallWindowDiagnosticsPolicy.FormatWindowLifecycleFailureMessage(
            "request-close",
            "InvalidOperationException",
            "close failed");

        message.Should().Contain("[RollCallWindow] window-lifecycle-failed");
        message.Should().Contain("op=request-close");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=close failed");
    }

    [Fact]
    public void FormatDialogShowFailureMessage_ShouldContainDialogAndException()
    {
        var message = RollCallWindowDiagnosticsPolicy.FormatDialogShowFailureMessage(
            "StudentListDialog",
            "InvalidOperationException",
            "show failed");

        message.Should().Contain("[RollCallWindow] dialog-show-failed");
        message.Should().Contain("dialog=StudentListDialog");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=show failed");
    }

    [Fact]
    public void FormatGroupOverlayFailureMessage_ShouldContainOperationAndException()
    {
        var message = RollCallWindowDiagnosticsPolicy.FormatGroupOverlayFailureMessage(
            "show-group",
            "InvalidOperationException",
            "group failed");

        message.Should().Contain("[RollCallWindow] group-overlay-failed");
        message.Should().Contain("op=show-group");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=group failed");
    }

    [Fact]
    public void FormatConfirmationFailureMessage_ShouldContainOperationAndException()
    {
        var message = RollCallWindowDiagnosticsPolicy.FormatConfirmationFailureMessage(
            "reset-rollcall-group",
            "InvalidOperationException",
            "confirm failed");

        message.Should().Contain("[RollCallWindow] confirm-show-failed");
        message.Should().Contain("op=reset-rollcall-group");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=confirm failed");
    }
}
