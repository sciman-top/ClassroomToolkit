using ClassroomToolkit.App.Photos;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerDiagnosticsPolicyTests
{
    [Fact]
    public void FormatFavoriteFolderDialogFailureMessage_ShouldContainMessage()
    {
        var message = ImageManagerDiagnosticsPolicy.FormatFavoriteFolderDialogFailureMessage(
            "dialog unavailable");

        message.Should().Contain("[ImageManager] favorite-folder-dialog-failed");
        message.Should().Contain("msg=dialog unavailable");
    }

    [Fact]
    public void FormatUpNavigationFailureMessage_ShouldContainFolderAndMessage()
    {
        var message = ImageManagerDiagnosticsPolicy.FormatUpNavigationFailureMessage(
            @"C:\data\photos",
            "denied");

        message.Should().Contain("[ImageManager] up-navigation-failed");
        message.Should().Contain(@"folder=C:\data\photos");
        message.Should().Contain("msg=denied");
    }

    [Fact]
    public void FormatThumbnailDispatchFailureMessage_ShouldContainPathAndMessage()
    {
        var message = ImageManagerDiagnosticsPolicy.FormatThumbnailDispatchFailureMessage(
            @"C:\data\photos\a.jpg",
            "dispatcher down");

        message.Should().Contain("[ImageManager] thumbnail-dispatch-failed");
        message.Should().Contain(@"path=C:\data\photos\a.jpg");
        message.Should().Contain("msg=dispatcher down");
    }

    [Fact]
    public void FormatFolderExpandFailureMessage_ShouldContainPathAndMessage()
    {
        var message = ImageManagerDiagnosticsPolicy.FormatFolderExpandFailureMessage(
            @"C:\data\photos",
            "io");

        message.Should().Contain("[ImageManager] folder-expand-failed");
        message.Should().Contain(@"path=C:\data\photos");
        message.Should().Contain("msg=io");
    }
}
