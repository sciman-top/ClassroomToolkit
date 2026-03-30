using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;
using System.Text.Json;

namespace ClassroomToolkit.Tests;

public sealed class PresentationClassifierTests
{
    private readonly PresentationClassifier _sut = new();
    private static readonly CompatibilityMatrix CompatibilityCases = LoadCompatibilityMatrix();

    // ── Classify: WPS by class name ──

    [Theory]
    [InlineData("kwppshowframeclass")]
    [InlineData("KWPPShowFrameClass")]
    [InlineData("kwpsshowframe")]
    [InlineData("wpsshowframe")]
    [InlineData("wpsshowwndclass")]
    public void Classify_WpsSlideshowClassName_ReturnsWps(string className)
    {
        var info = new PresentationWindowInfo(100, "wpp", new[] { className });
        _sut.Classify(info).Should().Be(PresentationType.Wps);
    }

    [Theory]
    [InlineData("kwppSomethingElse")]
    [InlineData("wpsshowCustom")]
    public void Classify_WpsLikeClassName_ReturnsWps(string className)
    {
        var info = new PresentationWindowInfo(100, "wpp", new[] { className });
        _sut.Classify(info).Should().Be(PresentationType.Wps);
    }

    // ── Classify: Office by class name ──

    [Theory]
    [InlineData("screenclass")]
    [InlineData("pptviewwndclass")]
    [InlineData("powerpntframeclass")]
    public void Classify_OfficeSlideshowClassName_ReturnsOffice(string className)
    {
        var info = new PresentationWindowInfo(100, "POWERPNT", new[] { className });
        _sut.Classify(info).Should().Be(PresentationType.Office);
    }

    // ── Classify: WPS by process name ──

    [Theory]
    [InlineData("wpp")]
    [InlineData("WPP")]
    [InlineData("wppt")]
    [InlineData("wpspresentationhost")]
    public void Classify_WpsProcessName_ReturnsWps(string processName)
    {
        var info = new PresentationWindowInfo(100, processName, Array.Empty<string>());
        _sut.Classify(info).Should().Be(PresentationType.Wps);
    }

    // ── Classify: Office by process name ──

    [Theory]
    [InlineData("POWERPNT")]
    [InlineData("powerpnt")]
    [InlineData("pptview")]
    public void Classify_OfficeProcessName_ReturnsOffice(string processName)
    {
        var info = new PresentationWindowInfo(100, processName, Array.Empty<string>());
        _sut.Classify(info).Should().Be(PresentationType.Office);
    }

    // ── Classify: screenclass + WPS-like process should prefer WPS ──

    [Fact]
    public void Classify_ScreenClassWithWpsLikeProcess_ReturnsWps()
    {
        var info = new PresentationWindowInfo(100, "wpsoffice", new[] { "screenclass" });
        _sut.Classify(info).Should().Be(PresentationType.Wps);
    }

    // ── Classify: screenclass + Office process → Office ──

    [Fact]
    public void Classify_ScreenClassWithOfficeProcess_ReturnsOffice()
    {
        var info = new PresentationWindowInfo(100, "POWERPNT", new[] { "screenclass" });
        _sut.Classify(info).Should().Be(PresentationType.Office);
    }

    // ── Classify: unknown ──

    [Fact]
    public void Classify_UnknownClassAndProcess_ReturnsOther()
    {
        var info = new PresentationWindowInfo(100, "notepad", new[] { "notepad" });
        _sut.Classify(info).Should().Be(PresentationType.Other);
    }

    [Fact]
    public void Classify_NullInfo_ReturnsNone()
    {
        _sut.Classify(null!).Should().Be(PresentationType.None);
    }

    [Fact]
    public void Classify_EmptyProcessName_ReturnsOther()
    {
        var info = new PresentationWindowInfo(0, "", Array.Empty<string>());
        _sut.Classify(info).Should().Be(PresentationType.Other);
    }

    // ── IsSlideshowWindow ──

    [Theory]
    [InlineData("kwppshowframeclass")]
    [InlineData("kwpsshowframe")]
    [InlineData("wpsshowframe")]
    public void IsSlideshowWindow_WpsClassNames_ReturnsTrue(string className)
    {
        var info = new PresentationWindowInfo(100, "wpp", new[] { className });
        _sut.IsSlideshowWindow(info).Should().BeTrue();
    }

    [Fact]
    public void IsSlideshowWindow_ScreenClassWithOfficeProcess_ReturnsTrue()
    {
        var info = new PresentationWindowInfo(100, "POWERPNT", new[] { "screenclass" });
        _sut.IsSlideshowWindow(info).Should().BeTrue();
    }

    [Fact]
    public void IsSlideshowWindow_ScreenClassWithUnknownProcess_ReturnsFalse()
    {
        var info = new PresentationWindowInfo(100, "notepad", new[] { "screenclass" });
        _sut.IsSlideshowWindow(info).Should().BeFalse();
    }

    [Fact]
    public void IsSlideshowWindow_NullInfo_ReturnsFalse()
    {
        _sut.IsSlideshowWindow(null!).Should().BeFalse();
    }

    [Fact]
    public void IsSlideshowWindow_NonSlideshowClass_ReturnsFalse()
    {
        var info = new PresentationWindowInfo(100, "POWERPNT", new[] { "powerpntframeclass" });
        _sut.IsSlideshowWindow(info).Should().BeFalse();
    }

    // ── Case insensitivity ──

    [Fact]
    public void Classify_CaseInsensitive_WpsClassName()
    {
        var info = new PresentationWindowInfo(100, "wpp", new[] { "KWPPSHOWFRAMECLASS" });
        _sut.Classify(info).Should().Be(PresentationType.Wps);
    }

    [Fact]
    public void Classify_CaseInsensitive_OfficeProcessName()
    {
        var info = new PresentationWindowInfo(100, "PowerPnt", Array.Empty<string>());
        _sut.Classify(info).Should().Be(PresentationType.Office);
    }

    // ── Class name priority over process name ──

    [Fact]
    public void Classify_WpsClassNameWithOfficeProcess_ReturnsWps()
    {
        // WPS 类名优先级高于进程名
        var info = new PresentationWindowInfo(100, "POWERPNT", new[] { "kwppshowframeclass" });
        _sut.Classify(info).Should().Be(PresentationType.Wps);
    }

    // ── Compatibility matrix: common version/process/class signatures ──

    [Theory]
    [MemberData(nameof(ClassificationCompatibilityCases))]
    public void Classify_CompatibilityMatrix_ReturnsExpected(
        string processName,
        string className,
        PresentationType expected)
    {
        var info = new PresentationWindowInfo(100, processName, new[] { className });

        _sut.Classify(info).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(SlideshowCompatibilityCases))]
    public void IsSlideshowWindow_CompatibilityMatrix_ReturnsExpected(
        string processName,
        string className,
        bool expected)
    {
        var info = new PresentationWindowInfo(100, processName, new[] { className });

        _sut.IsSlideshowWindow(info).Should().Be(expected);
    }

    public static IEnumerable<object[]> ClassificationCompatibilityCases()
    {
        return CompatibilityCases.Classification.Select(testCase => new object[]
        {
            testCase.ProcessName,
            testCase.ClassName,
            Enum.Parse<PresentationType>(testCase.Expected, ignoreCase: true)
        });
    }

    public static IEnumerable<object[]> SlideshowCompatibilityCases()
    {
        return CompatibilityCases.Slideshow.Select(testCase => new object[]
        {
            testCase.ProcessName,
            testCase.ClassName,
            testCase.Expected
        });
    }

    private static CompatibilityMatrix LoadCompatibilityMatrix()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "presentation-classifier-compatibility-matrix.json");
        var json = File.ReadAllText(path);
        var matrix = JsonSerializer.Deserialize<CompatibilityMatrix>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        if (matrix is null)
        {
            throw new InvalidOperationException("Failed to deserialize compatibility matrix.");
        }

        return matrix with
        {
            Classification = matrix.Classification ?? Array.Empty<ClassificationCase>(),
            Slideshow = matrix.Slideshow ?? Array.Empty<SlideshowCase>()
        };
    }

    private sealed record CompatibilityMatrix(
        IReadOnlyList<ClassificationCase> Classification,
        IReadOnlyList<SlideshowCase> Slideshow);

    private sealed record ClassificationCase(
        string ProcessName,
        string ClassName,
        string Expected);

    private sealed record SlideshowCase(
        string ProcessName,
        string ClassName,
        bool Expected);
}
