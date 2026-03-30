using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowingExceptionFilterPolicyTests
{
    [Fact]
    public void IsNonFatal_ShouldReturnTrue_ForRecoverableException()
    {
        var result = WindowingExceptionFilterPolicy.IsNonFatal(new InvalidOperationException("recoverable"));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsNonFatal_ShouldReturnFalse_ForFatalException()
    {
        var result = WindowingExceptionFilterPolicy.IsNonFatal(new BadImageFormatException("fatal"));

        result.Should().BeFalse();
    }
}
