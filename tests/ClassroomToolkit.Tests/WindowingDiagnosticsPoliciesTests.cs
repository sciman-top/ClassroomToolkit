using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowingExceptionFilterPolicyTests
{
    [Fact]
    public void IsNonFatal_ShouldReturnTrue_ForRecoverableException()
    {
        var result = WindowingDiagnosticsPolicies.IsNonFatal(new InvalidOperationException("recoverable"));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsNonFatal_ShouldReturnFalse_ForFatalException()
    {
        var result = WindowingDiagnosticsPolicies.IsNonFatal(new BadImageFormatException("fatal"));

        result.Should().BeFalse();
    }
}
