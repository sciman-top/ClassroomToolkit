using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoNavigationDiagnosticsDisposeSafetyContractTests
{
    [Fact]
    public void ScopeDispose_ShouldGuardDisposeCallbackFailure()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("scope dispose callback failed");
        source.Should().Contain("catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Photos",
            "PhotoNavigationDiagnostics.cs");
    }
}
