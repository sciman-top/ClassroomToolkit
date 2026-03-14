using ClassroomToolkit.App.Diagnostics;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StartupDiagnosticsGatePolicyTests
{
    [Fact]
    public void ShouldRun_ShouldReturnFalse_WhenFlagIsOne()
    {
        StartupDiagnosticsGatePolicy.ShouldRun("1").Should().BeFalse();
    }

    [Fact]
    public void ShouldRun_ShouldReturnFalse_WhenFlagIsOneWithSpaces()
    {
        StartupDiagnosticsGatePolicy.ShouldRun(" 1 ").Should().BeFalse();
    }

    [Fact]
    public void ShouldRun_ShouldReturnTrue_WhenFlagIsNullOrOther()
    {
        StartupDiagnosticsGatePolicy.ShouldRun(null).Should().BeTrue();
        StartupDiagnosticsGatePolicy.ShouldRun(string.Empty).Should().BeTrue();
        StartupDiagnosticsGatePolicy.ShouldRun("0").Should().BeTrue();
        StartupDiagnosticsGatePolicy.ShouldRun("false").Should().BeTrue();
    }
}
