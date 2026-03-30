using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputSwitchAdmissionPolicyTests
{
    [Theory]
    [InlineData(false, true, false, true, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, true, true, true, true)]
    public void ShouldProceed_ShouldMatchExpected(
        bool canSwitchByGate,
        bool hasBitmap,
        bool hasCurrentRect,
        bool shouldSwitchByPointer,
        bool expected)
    {
        var result = CrossPageInputSwitchAdmissionPolicy.ShouldProceed(
            canSwitchByGate,
            hasBitmap,
            hasCurrentRect,
            shouldSwitchByPointer);

        result.Should().Be(expected);
    }
}
