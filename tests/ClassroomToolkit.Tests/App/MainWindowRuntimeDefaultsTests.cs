using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class MainWindowRuntimeDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        MainWindowRuntimeDefaults.OverlayActivationRetouchMinIntervalMs.Should().Be(100);
        MainWindowRuntimeDefaults.ExplicitForegroundRetouchMinIntervalMs.Should().Be(120);
        MainWindowRuntimeDefaults.StartupDiagnosticsDialogDelayMs.Should().Be(1800);
        MainWindowRuntimeDefaults.LauncherMinutesToSeconds.Should().Be(60);
        MainWindowRuntimeDefaults.NumericComparisonEpsilon.Should().Be(0.0001);
    }
}
