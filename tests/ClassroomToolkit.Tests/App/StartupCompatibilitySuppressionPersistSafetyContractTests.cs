using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class StartupCompatibilitySuppressionPersistSafetyContractTests
{
    [Fact]
    public void SuppressionPersist_ShouldBeGuardedByNonFatalExceptionFilter()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (dialog.SuppressCurrentIssues && settings != null)");
        source.Should().Contain("_services.GetService<AppSettingsService>()?.Save(settings);");
        source.Should().Contain("catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))");
        source.Should().Contain("_logException(ex, \"StartupCompatibilitySuppressionPersist\");");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Startup",
            "StartupOrchestrator.cs");
    }
}
