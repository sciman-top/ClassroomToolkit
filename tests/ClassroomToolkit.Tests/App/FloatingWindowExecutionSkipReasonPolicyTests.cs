using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingWindowExecutionSkipReasonPolicyTests
{
    [Fact]
    public void ResolveTag_ShouldReturnExpectedTag()
    {
        FloatingWindowExecutionSkipReasonPolicy.ResolveTag(FloatingWindowExecutionSkipReason.EnforceZOrder)
            .Should().Be("enforce-zorder");
        FloatingWindowExecutionSkipReasonPolicy.ResolveTag(FloatingWindowExecutionSkipReason.ActivationIntent)
            .Should().Be("activation-intent");
        FloatingWindowExecutionSkipReasonPolicy.ResolveTag(FloatingWindowExecutionSkipReason.OwnerBindingIntent)
            .Should().Be("owner-binding-intent");
        FloatingWindowExecutionSkipReasonPolicy.ResolveTag(FloatingWindowExecutionSkipReason.NoExecutionIntent)
            .Should().Be("no-execution-intent");
        FloatingWindowExecutionSkipReasonPolicy.ResolveTag(FloatingWindowExecutionSkipReason.None)
            .Should().Be("execute");
    }
}
