using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayActivationRetouchReasonPolicyTests
{
    [Fact]
    public void ResolveTag_ShouldReturnExpectedTag()
    {
        OverlayActivationRetouchReasonPolicy.ResolveTag(OverlayActivationRetouchReason.NoApplyRequest)
            .Should().Be("no-apply-request");
        OverlayActivationRetouchReasonPolicy.ResolveTag(OverlayActivationRetouchReason.Throttled)
            .Should().Be("throttled");
        OverlayActivationRetouchReasonPolicy.ResolveTag(OverlayActivationRetouchReason.Forced)
            .Should().Be("forced");
        OverlayActivationRetouchReasonPolicy.ResolveTag(OverlayActivationRetouchReason.None)
            .Should().Be("apply");
    }
}
