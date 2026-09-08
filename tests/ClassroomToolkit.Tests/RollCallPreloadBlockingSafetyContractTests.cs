using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallPreloadBlockingSafetyContractTests
{
    [Fact]
    public void RollCallViewModelPreload_ShouldAvoidTaskResultText()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "ViewModels",
            "RollCallViewModel.Data.cs"));

        source.Should().Contain("TryReadCompletedSuccessfulPreloadResult(");
        source.Should().Contain("preloadTask.IsCompletedSuccessfully");
        source.Should().Contain("SHA256.HashData(stream)");
        source.Should().Contain("_preloadedLength");
        source.Should().Contain("_preloadedContentHash");
        source.Should().NotContain(".Result");
        source.Should().NotContain(".Wait(");
        source.Should().NotContain(".GetAwaiter().GetResult()");
    }
}
