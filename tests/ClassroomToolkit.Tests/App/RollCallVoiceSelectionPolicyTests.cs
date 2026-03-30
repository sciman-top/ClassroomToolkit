using ClassroomToolkit.App;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallVoiceSelectionPolicyTests
{
    [Fact]
    public void Resolve_ShouldDisableSelection_WhenVoiceListIsEmpty()
    {
        var decision = RollCallVoiceSelectionPolicy.Resolve(
            Array.Empty<string>(),
            preferredVoiceId: "voice-a",
            fallbackVoiceId: "voice-b");

        decision.IsVoiceSelectionEnabled.Should().BeFalse();
        decision.SelectedVoiceId.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ShouldDisableSelection_WhenVoiceListHasOnlyPlaceholders()
    {
        var decision = RollCallVoiceSelectionPolicy.Resolve(
            new[] { string.Empty, "  " },
            preferredVoiceId: "voice-a",
            fallbackVoiceId: "voice-b");

        decision.IsVoiceSelectionEnabled.Should().BeFalse();
        decision.SelectedVoiceId.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ShouldSelectPreferredVoice_WhenPreferredExists()
    {
        var decision = RollCallVoiceSelectionPolicy.Resolve(
            new[] { "voice-a", "voice-b" },
            preferredVoiceId: "voice-b",
            fallbackVoiceId: "voice-a");

        decision.IsVoiceSelectionEnabled.Should().BeTrue();
        decision.SelectedVoiceId.Should().Be("voice-b");
    }

    [Fact]
    public void Resolve_ShouldSelectFallbackVoice_WhenPreferredMissingAndFallbackExists()
    {
        var decision = RollCallVoiceSelectionPolicy.Resolve(
            new[] { "voice-a", "voice-b" },
            preferredVoiceId: "voice-c",
            fallbackVoiceId: "voice-a");

        decision.IsVoiceSelectionEnabled.Should().BeTrue();
        decision.SelectedVoiceId.Should().Be("voice-a");
    }

    [Fact]
    public void Resolve_ShouldSelectFirstUsableVoice_WhenPreferredAndFallbackMissing()
    {
        var decision = RollCallVoiceSelectionPolicy.Resolve(
            new[] { "voice-a", "voice-b" },
            preferredVoiceId: "voice-c",
            fallbackVoiceId: "voice-d");

        decision.IsVoiceSelectionEnabled.Should().BeTrue();
        decision.SelectedVoiceId.Should().Be("voice-a");
    }
}
