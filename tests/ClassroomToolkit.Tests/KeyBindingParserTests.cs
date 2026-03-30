using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class KeyBindingParserTests
{
    [Fact]
    public void ParseTab_ShouldWork()
    {
        var ok = KeyBindingParser.TryParse("tab", out var binding);
        ok.Should().BeTrue();
        binding!.Key.Should().Be(VirtualKey.Tab);
        binding.Modifiers.Should().Be(KeyModifiers.None);
    }

    [Fact]
    public void ParseShiftB_ShouldWork()
    {
        var ok = KeyBindingParser.TryParse("shift+b", out var binding);
        ok.Should().BeTrue();
        binding!.Key.Should().Be(VirtualKey.B);
        binding.Modifiers.Should().Be(KeyModifiers.Shift);
    }

    [Fact]
    public void ParseInvalid_ShouldFail()
    {
        var ok = KeyBindingParser.TryParse("unknown", out var binding);
        ok.Should().BeFalse();
        binding.Should().BeNull();
    }
}
