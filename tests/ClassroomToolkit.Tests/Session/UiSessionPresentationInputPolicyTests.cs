using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionPresentationInputPolicyTests
{
    [Theory]
    [InlineData(UiNavigationMode.Disabled, false)]
    [InlineData(UiNavigationMode.MessageOnly, false)]
    [InlineData(UiNavigationMode.HookOnly, true)]
    [InlineData(UiNavigationMode.Hybrid, true)]
    public void AllowsPresentationInput_ShouldMatchContract(
        UiNavigationMode navigationMode,
        bool expected)
    {
        UiSessionPresentationInputPolicy.AllowsPresentationInput(navigationMode)
            .Should().Be(expected);
    }
}
