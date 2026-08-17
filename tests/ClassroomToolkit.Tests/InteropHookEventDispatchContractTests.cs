using FluentAssertions;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Tests;

[Trait("Gate", "CoreContract")]
public sealed class InteropHookEventDispatchContractTests
{
    [Fact]
    public void KeyboardHook_ShouldUseInteropEventDispatchPolicy_ForBindingTriggered()
    {
        var source = ReadInteropSources("KeyboardHook*.cs");

        source.Should().Contain("InteropEventDispatchPolicy.InvokeSafely(");
        source.Should().Contain("\"KeyboardHook.BindingTriggered\"");
        source.Should().NotContain("BindingTriggered?.Invoke(");
    }

    [Fact]
    public void WpsSlideshowNavigationHook_ShouldIsolateSubscriberFailure()
    {
        Action? pending = null;
        using var hook = new WpsSlideshowNavigationHook((_, action, _) => pending = action);
        var successfulSubscriberCount = 0;
        hook.NavigationRequested += (_, _) => throw new InvalidOperationException("subscriber-failure");
        hook.NavigationRequested += (_, _) => successfulSubscriberCount++;
        hook.SetInterceptEnabled(true);

        hook.QueueNavigationRequest(1, "test");
        pending.Should().NotBeNull();
        pending!();

        successfulSubscriberCount.Should().Be(1);
    }

    private static string ReadInteropSources(string pattern)
    {
        return ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.Interop",
            "Presentation",
            pattern);
    }
}
