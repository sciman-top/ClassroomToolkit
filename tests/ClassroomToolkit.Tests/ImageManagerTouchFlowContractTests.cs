using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerTouchFlowContractTests
{
    [Fact]
    public void ImageManager_ShouldExposeVisibleSelectionMode_AndKeepSingleTapOpen()
    {
        var xaml = File.ReadAllText(GetXamlPath());
        var source = File.ReadAllText(GetSourcePath());

        xaml.Should().Contain("x:Name=\"EnterSelectionModeButton\"");
        xaml.Should().Contain("Click=\"OnEnterSelectionModeClick\"");
        xaml.Should().Contain("x:Name=\"DeleteFilesButton\"");
        source.Should().Contain("private void OnEnterSelectionModeClick");
        source.Should().Contain("EnterMultiSelectMode(");
        source.Should().Contain("ImageManagerActivationPolicy.ShouldOpenOnSingleClick");
        source.Should().Contain("if (item.IsFolder)");
        source.Should().Contain("OpenFolder(item.Path);");
    }

    private static string GetXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Photos",
        "ImageManagerWindow.xaml");

    private static string GetSourcePath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Photos",
        "ImageManagerWindow.Navigation.cs");
}
