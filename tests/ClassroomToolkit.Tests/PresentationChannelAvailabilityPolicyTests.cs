using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationChannelAvailabilityPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void IsAnyChannelEnabled_ShouldMatchExpected(bool allowOffice, bool allowWps, bool expected)
    {
        PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(allowOffice, allowWps)
            .Should()
            .Be(expected);
    }
}
