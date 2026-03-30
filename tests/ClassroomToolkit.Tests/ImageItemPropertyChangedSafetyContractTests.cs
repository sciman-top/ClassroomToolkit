using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageItemPropertyChangedSafetyContractTests
{
    [Fact]
    public void PropertyChangedNotification_ShouldBeGuardedBySafeActionExecutor()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName))");
        source.Should().Contain("ImageItem: property changed callback failed");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "ImageItem.cs");
    }
}
