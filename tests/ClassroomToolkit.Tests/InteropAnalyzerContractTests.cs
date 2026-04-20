using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InteropAnalyzerContractTests
{
    [Fact]
    public void KeyboardHookFiles_ShouldDeclareInteropPresentationNamespace()
    {
        var callbackSource = File.ReadAllText(GetInteropPath("Presentation", "KeyboardHook.Callback.cs"));
        var suppressionSource = File.ReadAllText(GetInteropPath("Presentation", "KeyboardHookSuppressionPolicy.cs"));

        callbackSource.Should().Contain("namespace ClassroomToolkit.Interop.Presentation;");
        suppressionSource.Should().Contain("namespace ClassroomToolkit.Interop.Presentation;");
    }

    [Fact]
    public void KeyboardHookCallback_ShouldUseGenericEnumIsDefinedOverload()
    {
        var source = File.ReadAllText(GetInteropPath("Presentation", "KeyboardHook.Callback.cs"));

        source.Should().Contain("Enum.IsDefined(key)");
        source.Should().NotContain("Enum.IsDefined(typeof(VirtualKey), key)");
    }

    [Fact]
    public void Win32PresentationResolver_ShouldCheckThreadLookupReturnValue()
    {
        var source = File.ReadAllText(GetInteropPath("Presentation", "Win32PresentationResolver.Native.cs"));

        source.Should().Contain("var threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);");
        source.Should().Contain("return threadId == 0 ? 0u : pid;");
    }

    [Theory]
    [InlineData("Presentation", "KeyboardHook.Interop.cs")]
    [InlineData("Presentation", "WpsSlideshowNavigationHook.Interop.cs")]
    public void HookInteropFiles_ShouldUseExplicitUnicodeMarshalling(string folder, string fileName)
    {
        var source = File.ReadAllText(GetInteropPath(folder, fileName));

        source.Should().Contain("CharSet = CharSet.Unicode");
    }

    private static string GetInteropPath(params string[] segments)
    {
        var fullSegments = new string[segments.Length + 2];
        fullSegments[0] = "src";
        fullSegments[1] = "ClassroomToolkit.Interop";
        Array.Copy(segments, 0, fullSegments, 2, segments.Length);
        return TestPathHelper.ResolveRepoPath(fullSegments);
    }
}
