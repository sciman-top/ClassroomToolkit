using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoOverlayDiagnosticsStartupContractTests
{
    [Fact]
    public void AppStartup_ShouldInitializePhotoOverlayDiagnosticsSession()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("PhotoOverlayDiagnostics.InitializeSession(");
        source.Should().Contain("Path.Combine(AppDataDirectory, \"logs\")");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "App.xaml.cs");
    }
}
