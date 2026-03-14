using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class KeyboardHookSuppressionPolicyTests
{
    [Fact]
    public void Resolve_ShouldSuppressKeyDownAndRememberPendingKey_WhenBindingMatches()
    {
        var decision = KeyboardHookSuppressionPolicy.Resolve(
            suppressWhenMatched: true,
            bindingMatched: true,
            isDown: true,
            isUp: false,
            key: VirtualKey.Enter,
            pendingSuppressedKey: null);

        decision.ShouldSuppress.Should().BeTrue();
        decision.PendingSuppressedKey.Should().Be(VirtualKey.Enter);
    }

    [Fact]
    public void Resolve_ShouldSuppressMatchingKeyUpAndClearPending()
    {
        var decision = KeyboardHookSuppressionPolicy.Resolve(
            suppressWhenMatched: true,
            bindingMatched: false,
            isDown: false,
            isUp: true,
            key: VirtualKey.Enter,
            pendingSuppressedKey: VirtualKey.Enter);

        decision.ShouldSuppress.Should().BeTrue();
        decision.PendingSuppressedKey.Should().BeNull();
    }

    [Fact]
    public void Resolve_ShouldKeepPending_WhenDifferentKeyIsReleased()
    {
        var decision = KeyboardHookSuppressionPolicy.Resolve(
            suppressWhenMatched: true,
            bindingMatched: false,
            isDown: false,
            isUp: true,
            key: VirtualKey.Space,
            pendingSuppressedKey: VirtualKey.Enter);

        decision.ShouldSuppress.Should().BeFalse();
        decision.PendingSuppressedKey.Should().Be(VirtualKey.Enter);
    }
}
