using ClassroomToolkit.Interop.Utilities;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InteropExceptionFilterPolicyTests
{
    [Fact]
    public void IsNonFatal_ShouldReturnTrue_ForRecoverableException()
    {
        var result = InteropExceptionFilterPolicy.IsNonFatal(new InvalidOperationException("recoverable"));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsNonFatal_ShouldReturnFalse_ForFatalException()
    {
        var result = InteropExceptionFilterPolicy.IsNonFatal(new BadImageFormatException("fatal"));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsNonFatal_ShouldReturnFalse_ForAccessViolationException()
    {
        var result = InteropExceptionFilterPolicy.IsNonFatal(new AccessViolationException("access-violation"));

        result.Should().BeFalse();
    }
}
