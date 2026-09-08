using ClassroomToolkit.App;
using AwesomeAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowLoadedToggleActionPolicyTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void Resolve_ShouldMatchExpected(
        bool launcherMinimized,
        int expected)
    {
        var action = MainWindowLoadedToggleActionPolicy.Resolve(launcherMinimized);

        ((int)action).Should().Be(expected);
    }
}
