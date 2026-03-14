using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class WpsRawFallbackTargetPolicyTests
{
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public void ShouldResolveWpsRawTarget_ShouldMatchExpected(
        bool presentationTargetValid,
        bool allowWps,
        bool expected)
    {
        WpsRawFallbackTargetPolicy.ShouldResolveWpsRawTarget(presentationTargetValid, allowWps)
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData(false, InputStrategy.Raw, false)]
    [InlineData(true, InputStrategy.Message, false)]
    [InlineData(true, InputStrategy.Raw, true)]
    public void IsValid_ShouldMatchExpected(bool wpsTargetValid, InputStrategy wpsSendMode, bool expected)
    {
        WpsRawFallbackTargetPolicy.IsValid(wpsTargetValid, wpsSendMode)
            .Should()
            .Be(expected);
    }
}
