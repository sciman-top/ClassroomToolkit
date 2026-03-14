using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoRightButtonDownExecutionPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, true)]
    public void Resolve_ShouldMatchExpected(
        bool shouldArmPending,
        bool shouldAllowPan,
        bool expectedArmPending,
        bool expectedTryBeginPan)
    {
        var plan = PhotoRightButtonDownExecutionPolicy.Resolve(
            shouldArmPending,
            shouldAllowPan);

        plan.ShouldArmPending.Should().Be(expectedArmPending);
        plan.ShouldTryBeginPan.Should().Be(expectedTryBeginPan);
    }
}
