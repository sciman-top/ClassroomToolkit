using ClassroomToolkit.App.Photos;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerActivationPolicyTests
{
    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, false)]
    public void ShouldOpenOnSingleClick_ShouldNeverOpenVisibleItems(
        bool isFolder,
        bool isPdf,
        bool isImage,
        bool expected)
    {
        ImageManagerActivationPolicy.ShouldOpenOnSingleClick(isFolder, isPdf, isImage)
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void ShouldOpenOnDoubleClick_ShouldAllowFolderAndPreviewableFiles(
        bool isFolder,
        bool isPdf,
        bool isImage,
        bool expected)
    {
        ImageManagerActivationPolicy.ShouldOpenOnDoubleClick(isFolder, isPdf, isImage)
            .Should()
            .Be(expected);
    }
}
