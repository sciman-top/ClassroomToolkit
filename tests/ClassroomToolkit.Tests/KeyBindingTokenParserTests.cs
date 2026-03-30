using ClassroomToolkit.Services.Input;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class KeyBindingTokenParserTests
{
    [Fact]
    public void TryNormalize_ShouldReturnFalse_WhenInputInvalid()
    {
        var result = KeyBindingTokenParser.TryNormalize("invalid-key", out var normalized);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
    }

    [Fact]
    public void TryNormalize_ShouldReturnNormalizedToken_WhenInputValid()
    {
        var result = KeyBindingTokenParser.TryNormalize("  SHIFT+B ", out var normalized);

        result.Should().BeTrue();
        normalized.Should().Be("shift+b");
    }
}
