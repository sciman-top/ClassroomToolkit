using System.Reflection;
using ClassroomToolkit.Services.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationDiagnosticsProbeErrorMessageTests
{
    [Fact]
    public void ResolveProbeErrorMessage_ShouldPreferAggregateInnerExceptionMessage()
    {
        var method = GetResolveErrorMethod();
        var ex = new AggregateException("outer-message", new InvalidOperationException("inner-message"));

        var result = method.Invoke(null, [ex]) as string;

        result.Should().Be("inner-message");
    }

    [Fact]
    public void ResolveProbeErrorMessage_ShouldFallbackToOriginalMessage_ForNonAggregate()
    {
        var method = GetResolveErrorMethod();
        var ex = new InvalidOperationException("plain-message");

        var result = method.Invoke(null, [ex]) as string;

        result.Should().Be("plain-message");
    }

    private static MethodInfo GetResolveErrorMethod()
    {
        var method = typeof(PresentationDiagnosticsProbe).GetMethod(
            "ResolveProbeErrorMessage",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        return method!;
    }
}
