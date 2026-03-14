using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageRequestAdmissionReasonPolicyTests
{
    [Theory]
    [InlineData(1, "crosspage-inactive")]
    [InlineData(2, "photo-loading")]
    [InlineData(3, "background-not-ready")]
    [InlineData(4, "overlay-not-visible")]
    [InlineData(5, "overlay-minimized")]
    [InlineData(6, "viewport-unavailable")]
    [InlineData(0, "admitted")]
    public void ResolveDiagnosticTag_ShouldReturnExpectedTag(
        int reasonValue,
        string expectedTag)
    {
        var reason = (CrossPageRequestAdmissionReason)reasonValue;

        CrossPageRequestAdmissionReasonPolicy
            .ResolveDiagnosticTag(reason)
            .Should()
            .Be(expectedTag);
    }
}
