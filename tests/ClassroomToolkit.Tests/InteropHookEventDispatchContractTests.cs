using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InteropHookEventDispatchContractTests
{
    [Fact]
    public void KeyboardHook_ShouldUseInteropEventDispatchPolicy_ForBindingTriggered()
    {
        var source = File.ReadAllText(GetSourcePath("KeyboardHook.cs"));

        source.Should().Contain("InteropEventDispatchPolicy.InvokeSafely(");
        source.Should().Contain("\"KeyboardHook.BindingTriggered\"");
        source.Should().NotContain("BindingTriggered?.Invoke(");
    }

    [Fact]
    public void WpsSlideshowNavigationHook_ShouldUseInteropEventDispatchPolicy_ForNavigationRequested()
    {
        var source = File.ReadAllText(GetSourcePath("WpsSlideshowNavigationHook.cs"));

        source.Should().Contain("InteropEventDispatchPolicy.InvokeSafely(");
        source.Should().Contain("\"WpsSlideshowNavigationHook.NavigationRequested\"");
        source.Should().NotContain("NavigationRequested?.Invoke(");
    }

    private static string GetSourcePath(string fileName)
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.Interop",
            "Presentation",
            fileName);
    }
}
