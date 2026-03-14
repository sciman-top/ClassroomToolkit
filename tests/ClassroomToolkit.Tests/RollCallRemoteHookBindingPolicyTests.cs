using ClassroomToolkit.App.RollCall;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallRemoteHookBindingPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnF5Variants_WhenConfiguredAsF5()
    {
        var bindings = RollCallRemoteHookBindingPolicy.ResolveTokens("f5", "tab");

        bindings.Should().HaveCount(3);
        bindings[0].Should().Be("f5");
        bindings[1].Should().Be("shift+f5");
        bindings[2].Should().Be("escape");
    }

    [Fact]
    public void Resolve_ShouldFallback_WhenConfiguredBindingIsUnsupportedW()
    {
        var bindings = RollCallRemoteHookBindingPolicy.ResolveTokens("w", "b");

        bindings.Should().ContainSingle();
        bindings[0].Should().Be("b");
    }

    [Fact]
    public void Resolve_ShouldParseRegularBinding()
    {
        var bindings = RollCallRemoteHookBindingPolicy.ResolveTokens("shift+b", "tab");

        bindings.Should().ContainSingle();
        bindings[0].Should().Be("shift+b");
    }
}
