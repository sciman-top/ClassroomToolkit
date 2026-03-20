using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class WpsHookKeyboardInjectionPolicyTests
{
    [Theory]
    [InlineData(0x10u)]
    [InlineData(0x02u)]
    [InlineData(0x12u)]
    public void ShouldIgnore_ShouldReturnTrue_WhenKeyboardEventIsInjected(uint flags)
    {
        WpsHookKeyboardInjectionPolicy.ShouldIgnore(flags).Should().BeTrue();
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x20u)]
    public void ShouldIgnore_ShouldReturnFalse_WhenKeyboardEventIsNotInjected(uint flags)
    {
        WpsHookKeyboardInjectionPolicy.ShouldIgnore(flags).Should().BeFalse();
    }
}

