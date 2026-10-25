using AwesomeAssertions;

namespace ClassroomToolkit.Tests.Interop;

public sealed class InteropMarshallingContractTests
{
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
