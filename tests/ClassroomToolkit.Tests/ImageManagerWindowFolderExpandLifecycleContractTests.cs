using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerWindowFolderExpandLifecycleContractTests
{
    [Fact]
    public void OnFolderExpanded_ShouldDelegateToTaskWorker()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("private static void OnFolderExpanded(object sender, RoutedEventArgs e)");
        source.Should().Contain("_ = OnFolderExpandedAsync(sender);");
    }

    [Fact]
    public void OnFolderExpandedAsync_ShouldGuardShutdownRelatedExceptions()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("catch (OperationCanceledException)");
        source.Should().Contain("catch (ObjectDisposedException)");
        source.Should().Contain("FormatFolderExpandFailureMessage(");
    }

    private static string GetSourcePath()
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageManagerWindow.xaml.cs");
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClassroomToolkit.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
