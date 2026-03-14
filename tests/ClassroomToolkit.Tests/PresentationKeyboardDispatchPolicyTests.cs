using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationKeyboardDispatchPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ShouldDispatch_ShouldMatchExpected(bool presentationAllowed, bool keyMapped, bool expected)
    {
        PresentationKeyboardDispatchPolicy.ShouldDispatch(presentationAllowed, keyMapped)
            .Should()
            .Be(expected);
    }
}
