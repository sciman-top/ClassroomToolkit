using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AppStartupCompatibilityOverridesWiringContractTests
{
    [Fact]
    public void AppStartup_ShouldPassClassifierOverridesToStartupCompatibilityProbe()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("classifierOverridesJson = startupSettings.PresentationClassifierOverridesJson;");
        source.Should().Contain("StartupCompatibilityProbe.Collect(settingsPath, classifierOverridesJson);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "App.xaml.cs");
    }
}
