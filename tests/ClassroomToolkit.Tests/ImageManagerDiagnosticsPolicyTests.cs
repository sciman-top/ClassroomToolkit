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

    [Fact]
    public void FormatFileAttributeReadFailureMessage_ShouldContainPathExceptionAndMessage()
    {
        var message = ImageManagerDiagnosticsPolicy.FormatFileAttributeReadFailureMessage(
            @"C:\data\photos\a.jpg",
            "UnauthorizedAccessException",
            "denied");

        message.Should().Contain("[ImageManager] file-attribute-read-failed");
        message.Should().Contain(@"path=C:\data\photos\a.jpg");
        message.Should().Contain("ex=UnauthorizedAccessException");
        message.Should().Contain("msg=denied");
    }

    [Fact]
    public void FormatThumbnailLoadFailureMessage_ShouldContainPathSourceExceptionAndMessage()
    {
        var message = ImageManagerDiagnosticsPolicy.FormatThumbnailLoadFailureMessage(
            @"C:\data\photos\a.pdf",
            "pdf",
            "IOException",
            "broken");

        message.Should().Contain("[ImageManager] thumbnail-load-failed");
        message.Should().Contain(@"path=C:\data\photos\a.pdf");
        message.Should().Contain("source=pdf");
        message.Should().Contain("ex=IOException");
        message.Should().Contain("msg=broken");
    }

    [Fact]
    public void FormatPdfMetadataReadFailureMessage_ShouldContainPathExceptionAndMessage()
    {
        var message = ImageManagerDiagnosticsPolicy.FormatPdfMetadataReadFailureMessage(
            @"C:\data\photos\a.pdf",
            "InvalidDataException",
            "invalid");

        message.Should().Contain("[ImageManager] pdf-metadata-read-failed");
        message.Should().Contain(@"path=C:\data\photos\a.pdf");
        message.Should().Contain("ex=InvalidDataException");
        message.Should().Contain("msg=invalid");
    }

    [Fact]
    public void FormatModifiedTimeReadFailureMessage_ShouldContainPathExceptionAndMessage()
    {
        var message = ImageManagerDiagnosticsPolicy.FormatModifiedTimeReadFailureMessage(
            @"C:\data\photos\a.jpg",
            "IOException",
            "busy");

        message.Should().Contain("[ImageManager] modified-time-read-failed");
        message.Should().Contain(@"path=C:\data\photos\a.jpg");
        message.Should().Contain("ex=IOException");
        message.Should().Contain("msg=busy");
    }
}
