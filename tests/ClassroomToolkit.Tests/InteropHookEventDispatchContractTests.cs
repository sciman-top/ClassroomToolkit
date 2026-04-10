using FluentAssertions;

namespace ClassroomToolkit.Tests;

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
    public void WpsSlideshowNavigationHook_ShouldUseInteropEventDispatchPolicy_ForNavigationRequested()
    {
        var source = ReadInteropSources("WpsSlideshowNavigationHook*.cs");

        source.Should().Contain("InteropEventDispatchPolicy.InvokeSafely(");
        source.Should().Contain("\"WpsSlideshowNavigationHook.NavigationRequested\"");
        source.Should().NotContain("NavigationRequested?.Invoke(");
    }

    private static string ReadInteropSources(string pattern)
    {
        return ContractSourceAggregationHelper.ReadSourcesInDirectory(
            ["src", "ClassroomToolkit.Interop", "Presentation"],
            pattern);
    }
}
