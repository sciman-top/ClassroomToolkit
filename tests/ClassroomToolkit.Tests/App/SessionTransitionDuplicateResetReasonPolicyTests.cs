using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionDuplicateResetReasonPolicyTests
{
    [Fact]
    public void ResolveTag_ShouldReturnExpectedTag()
    {
        SessionTransitionDuplicateResetReasonPolicy.ResolveTag(SessionTransitionDuplicateResetReason.OverlayNotRewired)
            .Should().Be("overlay-not-rewired");
        SessionTransitionDuplicateResetReasonPolicy.ResolveTag(SessionTransitionDuplicateResetReason.NoAppliedTransition)
            .Should().Be("no-applied-transition");
        SessionTransitionDuplicateResetReasonPolicy.ResolveTag(SessionTransitionDuplicateResetReason.ResetRequired)
            .Should().Be("reset-required");
        SessionTransitionDuplicateResetReasonPolicy.ResolveTag(SessionTransitionDuplicateResetReason.None)
            .Should().Be("none");
    }
}
